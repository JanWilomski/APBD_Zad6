# APBD — ściąga na kolokwium (WebAPI + EF Core, Code First)

## 0. Przebieg krok po kroku (kolejność na kolokwium)

**1. Projekt + paczki**

```bash
dotnet new webapi --use-controllers -o NazwaProjektu
cd NazwaProjektu
dotnet add package Microsoft.EntityFrameworkCore.SqlServer
dotnet add package Microsoft.EntityFrameworkCore.Design
dotnet tool install --global dotnet-ef   # jeśli brak
```

**2. `.gitignore` od razu** (zanim cokolwiek zacommitujesz)

```bash
dotnet new gitignore
```

**3. Foldery:** `Entities`, `Data`, `Controllers`, `Services`, `DTOs`, `Exceptions`
(`Migrations` zrobi się samo).

**4. Encje** (`Entities/`) — po jednej klasie na tabelę z diagramu. Znaki:
`[Table("...")]`, `[Key] Id`, `[ForeignKey(nameof(Nav))] + int FK + nawigacja`,
kolekcje `= null!`, klucz złożony → `[PrimaryKey(...)]` na klasie.

**5. Kontekst** (`Data/DatabaseContext.cs`): `: DbContext`, `DbSet<T>` na każdą encję,
konstruktor `(DbContextOptions options) : base(options)`, `OnModelCreating` z `HasData`
(pamiętaj o FK w seedzie!).

**6. `appsettings.json`:** dodaj `ConnectionStrings: { "Default": "..." }`.

**7. `Program.cs`:** trzy linijki — `AddControllers()`,
`AddDbContext<DatabaseContext>(o => o.UseSqlServer(...GetConnectionString("Default")))`,
`AddScoped<IDbService, DbService>()`.

**8. Migracja + baza**

```bash
dotnet ef migrations add Init
dotnet ef database update
```

**9. DTO** (`DTOs/`) — klasy w kształcie z `GET.json` (odpowiedź) i `POST.json`
(wejście, tu `[Required]`).

**10. Wyjątki** (`Exceptions/`): `NotFoundException`, `ConflictException` —
każdy `: Exception` z trzema konstruktorami.

**11. Serwis** (`Services/`): interfejs `IDbService` + klasa `DbService`
z `private readonly DatabaseContext`. Metody:

- GET: `.Select(...)` projekcja do DTO + `FirstOrDefaultAsync` → `null` rzuca `NotFoundException`.
- PUT: `BeginTransactionAsync` → `try` { sprawdzenia → modyfikacja → `SaveChangesAsync`
  → `CommitAsync` } `catch` { `RollbackAsync`; `throw;` }.

**12. Kontroler** (`Controllers/`): `[ApiController]`, `[Route("api/[controller]")]`,
wstrzyknięty `IDbService`, akcje `[HttpGet("{id}")]` i `[HttpPut("{id:int}")]` —
`async Task<IActionResult>`, `await` serwis, `try/catch` mapujący wyjątki
na `Ok`/`NotFound`/`Conflict`.

**13. Test:** uruchom, sprawdź GET (200 + 404) i PUT (200 / 409 / 404 / 400 dla pustego ciała).

**14. Git**

```bash
git init
git add .
git status        # sprawdź: brak bin/ obj/, jest Migrations/
git commit -m "solution"
git remote add origin <URL>
git push -u origin main
```

Kolejność warstw (4→12) zawsze ta sama: **encje → kontekst → migracja → DTO →
wyjątki → serwis → kontroler**, bo każda następna korzysta z poprzedniej.

-----

## 1. Droga żądania przez warstwy (request flow)

Tak płynie pojedyncze żądanie HTTP od kliknięcia do bazy i z powrotem:

```
HTTP request
   │
   ▼
[Routing ASP.NET]  ── dopasowuje URL + metodę (GET/PUT) do akcji w kontrolerze
   │
   ▼
[Controller]  ── [ApiController] sprawdza walidację ([Required] itp.) → jak błąd: 400 i koniec
   │            ── woła serwis przez wstrzyknięty interfejs IDbService
   ▼
[Service (DbService)]  ── logika + reguły biznesowe; mówi językiem bazy, NIE zna HTTP
   │                    ── rzuca wyjątki (NotFoundException, ConflictException...)
   ▼
[DatabaseContext (DbContext)]  ── DbSet-y + LINQ; EF tłumaczy LINQ na SQL
   │
   ▼
[Baza danych SQL Server]
```

Odpowiedź wraca tą samą drogą w drugą stronę:

```
Baza → encje → (projekcja .Select) → DTO → Controller
Controller zamienia wynik/wyjątek na kod HTTP:
   wynik       → Ok(dto)          (200)
   NotFound    → NotFound(msg)    (404)
   Conflict    → Conflict(msg)    (409)
   walidacja   → automatycznie    (400, robi to [ApiController])
→ ASP.NET serializuje DTO do JSON → HTTP response
```

**Jak to spina Dependency Injection (DI):** `Program.cs` rejestruje `DatabaseContext`
i `IDbService` w kontenerze → przy każdym żądaniu kontener tworzy ich instancje
i wstrzykuje przez konstruktory (`Controller` dostaje `IDbService`,
`DbService` dostaje `DatabaseContext`). Nigdzie nie piszesz `new DbService(...)`.

Łańcuch zależności: **Controller → IDbService → DatabaseContext → baza**

-----

## 2. Charakterystyczne fragmenty każdej warstwy

Po tych elementach poznajesz (i piszesz) daną warstwę.

### Entities/ — encje (= tabele)

```csharp
[Table("Repair")]                          // jawna nazwa tabeli (l. poj.)
public class Repair
{
    [Key]                                  // klucz główny
    public int Id { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }   // ? = kolumna NULL

    [MaxLength(50)]                        // nvarchar(50)
    public string Name { get; set; } = null!;

    [Column(TypeName = "numeric")]
    [Precision(10, 2)]                     // numeric(10,2)
    public double Cost { get; set; }

    [ForeignKey(nameof(Customer))]         // FK: argument = nazwa NAWIGACJI
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;   // właściwość nawigacyjna

    public ICollection<Repair> Repairs { get; set; } = null!;  // strona "wiele"
}
```

Klucz złożony (tabela łącząca) — atrybut **na klasie**:

```csharp
[Table("Product_Order")]
[PrimaryKey(nameof(ProductId), nameof(OrderId))]
public class ProductOrder { ... }
```

Charakterystyczne: `[Table]`, `[Key]`, `[MaxLength]`, `[Column]+[Precision]`,
`[ForeignKey(nameof(...))]`, pary “klucz + nawigacja”, kolekcje `= null!`.

### Data/ — DatabaseContext

```csharp
public class DatabaseContext : DbContext           // dziedziczy po DbContext
{
    public DbSet<Customer> Customers { get; set; }  // DbSet = tabela (l. mnoga)
    public DbSet<Repair> Repairs { get; set; }
    public DbSet<Status> Statuses { get; set; }

    protected DatabaseContext() { }

    public DatabaseContext(DbContextOptions options) // <-- przez ten ctor wchodzi
        : base(options) { }                          //     connection string (DI)

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Status>().HasData(        // dane startowe (seed)
            new Status { Id = 1, Name = "Ongoing" },
            new Status { Id = 2, Name = "Completed" }
        );
        // pamiętaj o FK w seedzie! np. Repair musi mieć CustomerId
    }
}
```

Charakterystyczne: `: DbContext`, `DbSet<T>`, konstruktor z `DbContextOptions`,
`OnModelCreating` + `HasData`.

### DTOs/ — kształt danych na zewnątrz

```csharp
public class RepairDto                  // odpowiedź — czysta klasa, bez atrybutów
{
    public int Id { get; set; }
    public string Status { get; set; } = null!;       // string, nie obiekt Status!
    public CustomerInfoDto Customer { get; set; } = null!;
}

public class CompleteRepairDto          // wejście (ciało PUT/POST) — TU walidacja
{
    [Required] public string StatusName { get; set; } = null!;
}
```

Charakterystyczne: zwykłe klasy, `= null!`, na DTO wejściowym `[Required]`/`[Range]` itd.
Nazwy PascalCase w C# → camelCase w JSON (ASP.NET konwertuje automatycznie).

### Exceptions/ — własne wyjątki

```csharp
public class NotFoundException : Exception
{
    public NotFoundException() { }
    public NotFoundException(string? message) : base(message) { }
    public NotFoundException(string? message, Exception? inner) : base(message, inner) { }
}
```

Po to, by serwis mógł “powiedzieć co się stało” po TYPIE, a kontroler zmapował
typ na kod HTTP. (np. dodatkowo `ConflictException` → 409.)

### Services/ — logika + komunikacja z bazą

Interfejs (kontrakt):

```csharp
public interface IDbService
{
    Task<RepairDto> GetRepair(int id);             // async = Task<...>
    Task CompleteRepair(int id, CompleteRepairDto dto);
}
```

Implementacja — GET (projekcja do DTO + 404):

```csharp
public class DbService : IDbService
{
    private readonly DatabaseContext _context;     // private readonly!
    public DbService(DatabaseContext context) { _context = context; }

    public async Task<RepairDto> GetRepair(int id)
    {
        var repair = await _context.Repairs
            .Select(r => new RepairDto                 // projekcja: ciągnie tylko potrzebne
            {
                Id = r.Id,
                Status = r.Status.Name,                // JOIN po nawigacji, bez Include
                Customer = new CustomerInfoDto
                {
                    FirstName = r.Customer.FirstName,
                    LastName = r.Customer.LastName
                }
            })
            .FirstOrDefaultAsync(r => r.Id == id);

        if (repair is null) throw new NotFoundException("No repair found");
        return repair;
    }
}
```

Implementacja — PUT z transakcją (wzorzec do zapamiętania):

```csharp
public async Task CompleteRepair(int id, CompleteRepairDto dto)
{
    await using var transaction = await _context.Database.BeginTransactionAsync();
    try
    {
        var repair = await _context.Repairs.FirstOrDefaultAsync(r => r.Id == id);
        if (repair is null)        throw new NotFoundException("Repair not found");

        var status = await _context.Statuses
            .FirstOrDefaultAsync(s => s.Name == dto.StatusName);
        if (status is null)        throw new NotFoundException("Status not found");

        if (repair.CompletedAt != null)
            throw new ConflictException("Repair already completed");

        repair.StatusId = status.Id;          // ustaw KLUCZ (czyściej niż nawigację)
        repair.CompletedAt = DateTime.Now;
        // (w oryginale dochodziło: RemoveRange powiązanych ProductOrder)

        await _context.SaveChangesAsync();    // async!
        await transaction.CommitAsync();
    }
    catch (Exception)
    {
        await transaction.RollbackAsync();    // wszystko albo nic
        throw;                                // rzuć dalej -> kontroler złapie typ
    }
}
```

Charakterystyczne: `private readonly DatabaseContext`, wszystko `async`/`await`,
`.Select` + `FirstOrDefaultAsync`, rzucanie wyjątków, wzorzec transakcji.
**Kolejność w PUT:** najpierw walidacja/sprawdzenia, dopiero potem modyfikacja.

### Controllers/ — warstwa HTTP

```csharp
[ApiController]                          // auto-walidacja modelu (400), auto-binding
[Route("api/[controller]")]              // [controller] -> "Repairs" -> /api/repairs
public class RepairsController : ControllerBase
{
    private readonly IDbService _dbService;          // private readonly!
    public RepairsController(IDbService dbService) { _dbService = dbService; }

    [HttpGet("{id}")]                                // GET /api/repairs/{id}
    public async Task<IActionResult> GetRepair(int id)
    {
        try { return Ok(await _dbService.GetRepair(id)); }   // PAMIĘTAJ o await!
        catch (NotFoundException e) { return NotFound(e.Message); }
    }

    [HttpPut("{id:int}")]                            // PUT /api/repairs/{id}
    public async Task<IActionResult> CompleteRepair(int id, CompleteRepairDto dto)
    {
        try
        {
            await _dbService.CompleteRepair(id, dto);
            return Ok();
        }
        catch (NotFoundException e)        { return NotFound(e.Message); }   // 404
        catch (ConflictException e)        { return Conflict(e.Message); }   // 409
    }
}
```

Charakterystyczne: `[ApiController]`, `[Route("api/[controller]")]`,
`private readonly IDbService`, `[HttpGet]/[HttpPut]`, `async Task<IActionResult>`,
`await` przy serwisie, `try/catch` mapujący wyjątki na kody HTTP.

### POST — tworzenie zasobu (kod HTTP: 201 Created)

DTO wejściowe (walidacja):

```csharp
public class CreateRepairDto
{
    [Range(1, int.MaxValue)] public int CustomerId { get; set; }  // dla int: Range, nie Required
    [Range(1, int.MaxValue)] public int StatusId { get; set; }
    public double Cost { get; set; }
}
```

> Uwaga: `[Required]` na `int` nic nie daje (typ wartościowy ma domyślnie 0).
> Do ID-ków użyj `[Range(1, int.MaxValue)]`. `[Required]` ma sens na `string`/typach nullable.

Serwis — sprawdź FK, utwórz encję, zapisz, zwróć Id:

```csharp
public async Task<int> CreateRepair(CreateRepairDto dto)
{
    if (!await _context.Customers.AnyAsync(c => c.Id == dto.CustomerId))
        throw new NotFoundException("Customer not found");
    if (!await _context.Statuses.AnyAsync(s => s.Id == dto.StatusId))
        throw new NotFoundException("Status not found");

    var repair = new Repair
    {
        CreatedAt = DateTime.Now,
        Cost = dto.Cost,
        CustomerId = dto.CustomerId,
        StatusId = dto.StatusId
    };

    _context.Repairs.Add(repair);
    await _context.SaveChangesAsync();   // po Save repair.Id jest już uzupełnione
    return repair.Id;
}
```

Kontroler:

```csharp
[HttpPost]
public async Task<IActionResult> CreateRepair(CreateRepairDto dto)
{
    try
    {
        var id = await _dbService.CreateRepair(dto);
        return Created($"/api/repairs/{id}", new { id });   // 201 + nagłówek Location
    }
    catch (NotFoundException e) { return NotFound(e.Message); }
}
```

Wariant z wierszami powiązanymi (np. zamówienie + pozycje) — **w transakcji**:

```csharp
await using var transaction = await _context.Database.BeginTransactionAsync();
try
{
    _context.Orders.Add(order);
    await _context.SaveChangesAsync();              // teraz order.Id istnieje
    foreach (var p in dto.Products)
        _context.ProductOrders.Add(new ProductOrder { OrderId = order.Id, ProductId = p.Id, Amount = p.Amount });
    await _context.SaveChangesAsync();
    await transaction.CommitAsync();
}
catch (Exception) { await transaction.RollbackAsync(); throw; }
```

Charakterystyczne: `[HttpPost]`, `_context.Add(...)`, `SaveChangesAsync` (Id po zapisie),
`Created(...)`/`CreatedAtAction(...)` → 201, walidacja FK przez `AnyAsync`.

### DELETE — usuwanie zasobu (kod HTTP: 204 No Content)

Serwis — znajdź, jak `null` → 404, usuń, zapisz:

```csharp
public async Task DeleteRepair(int id)
{
    var repair = await _context.Repairs.FirstOrDefaultAsync(r => r.Id == id);
    if (repair is null) throw new NotFoundException("Repair not found");

    _context.Repairs.Remove(repair);
    await _context.SaveChangesAsync();
}
```

Kontroler:

```csharp
[HttpDelete("{id:int}")]
public async Task<IActionResult> DeleteRepair(int id)
{
    try
    {
        await _dbService.DeleteRepair(id);
        return NoContent();                 // 204 (albo Ok() = 200)
    }
    catch (NotFoundException e) { return NotFound(e.Message); }
}
```

> Jeśli usuwany rekord ma wiersze zależne (FK z innych tabel), samo `Remove` rzuci
> błędem klucza obcego. Wtedy najpierw usuń dzieci (`RemoveRange(...)`), a całość
> opakuj w transakcję — tak jak w PUT.

Charakterystyczne: `[HttpDelete("{id:int}")]`, `_context.Remove(...)`,
`NoContent()` → 204, najpierw sprawdzenie istnienia (404).

### Sygnatury w `IDbService` (dopisz, czego używasz)

```csharp
Task<int> CreateRepair(CreateRepairDto dto);
Task DeleteRepair(int id);
```

### Skrót kodów HTTP per metoda

|Metoda|Sukces       |Brak zasobu  |Konflikt      |Zła walidacja|
|------|-------------|-------------|--------------|-------------|
|GET   |200 Ok       |404 NotFound |–             |–            |
|POST  |201 Created  |404 (brak FK)|409 (duplikat)|400          |
|PUT   |200 Ok       |404 NotFound |409 Conflict  |400          |
|DELETE|204 NoContent|404 NotFound |–             |–            |

-----

## 3. Co dać w `Program.cs`

```csharp
using <Namespace>.Data;
using <Namespace>.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();                          // 1. kontrolery

builder.Services.AddDbContext<DatabaseContext>(options =>   // 2. kontekst + baza
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddScoped<IDbService, DbService>();        // 3. serwis (DI)

var app = builder.Build();

app.UseAuthorization();
app.MapControllers();                                       // mapuje endpointy
app.Run();
```

Trzy linijki, które MUSZĄ być (i każda chroni inne punkty):

- `AddControllers()` — bez tego endpointy nie działają.
- `AddDbContext<DatabaseContext>(UseSqlServer(...))` — wpina bazę; klucz
  `"Default"` musi się zgadzać z `appsettings.json`.
- `AddScoped<IDbService, DbService>()` — to jest **dependency injection**
  (jego brak: do -10 pkt). `Scoped` = jedna instancja na żądanie HTTP.

Opcjonalnie (nowe szablony): `AddOpenApi()` + `app.MapOpenApi()` dla podglądu API.

-----

## 4. Co dać w `appsettings.json`

Dodaj sekcję `ConnectionStrings` z kluczem `"Default"`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "Default": "Server=(localdb)\\mssqllocaldb;Database=AppDb;Trusted_Connection=True;TrustServerCertificate=True;"
  }
}
```

- Wariant **LocalDB** (Windows, nic nie trzeba stawiać) — jak wyżej.
- Wariant **SQL Server w Dockerze** (jeśli taki masz na maszynie):
  
  ```
  "Server=localhost,1433;Database=AppDb;User Id=SA;Password=YourStrong!Pass;TrustServerCertificate=True;"
  ```
- Klucz `"Default"` = ten z `GetConnectionString("Default")` w `Program.cs`.
- W JSON `\\` to jeden prawdziwy backslash (`\` jest znakiem ucieczki).

-----

## 5. Komendy EF Core + checklista końcowa

```bash
dotnet tool install --global dotnet-ef     # raz na maszynę (jeśli brak)
dotnet ef migrations add Init              # tworzy "przepis" w Migrations/
dotnet ef database update                  # wykonuje go na bazie
```

Repozytorium (decyduje o zaliczeniu):

```bash
dotnet new gitignore     # NAJPIERW, zanim pierwszy commit
git init
git add .
git status               # sprawdź: NIE MA bin/ ani obj/, JEST Migrations/
git commit -m "APBD solution"
# repo na GitHub robisz ręcznie, potem:
git remote add origin <URL>
git branch -M main
git push -u origin main
```

Pułapki, które kosztują punkty:

- [ ] brak `await` przy wywołaniu serwisu → crash w runtime (Task się nie serializuje)
- [ ] `public` zamiast `private readonly` na wstrzykniętych polach (do -8 pkt)
- [ ] synchroniczna komunikacja z bazą (brak async) (do -10 pkt)
- [ ] złe kody HTTP / puste komunikaty (do -10 pkt)
- [ ] brak warstwy serwisu (logika w kontrolerze) (do -20 pkt)
- [ ] brak DI (do -10 pkt)
- [ ] `bin/`/`obj/` w repo lub brak `.gitignore` (-50%)
- [ ] brak folderu `Migrations/` w repo (-10 pkt)
- [ ] nazwy folderów dokładnie wg treści (Entities/Data/Migrations)
- [ ] w seedzie pamiętaj o kluczach obcych (inaczej migracja nie wstanie)
- [ ] nie kompiluje się = 0 punktów

Pojęcia na część ustną: czym jest DI · Scoped vs Singleton vs Transient ·
po co transakcja (“wszystko albo nic”) · czemu DTO zamiast encji ·
po co interfejs serwisu · różnica `migrations add` vs `database update`.
