using APBD_Zad6.DTOs;
using APBD_Zad6.Exceptions;
using APBD_Zad6.Services;
using Microsoft.AspNetCore.Mvc;

namespace APBD_Zad6.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PatientsController : ControllerBase
{
    private readonly IDbService _dbService;

    public PatientsController(IDbService dbService)
    {
        _dbService = dbService;
    }

    [HttpGet]
    public async Task<IActionResult> GetPatients([FromQuery] string? search)
    {
        var patients = await _dbService.GetPatients(search);
        return Ok(patients);
    }

    [HttpPost("{pesel}/bedassignments")]
    public async Task<IActionResult> AssignBed(string pesel, CreateBedAssignmentDto dto)
    {
        try
        {
            var id = await _dbService.AssignBed(pesel, dto);
            return Created($"/api/patients/{pesel}/bedassignments/{id}", new { id });
        }
        catch (NotFoundException e)
        {
            return NotFound(e.Message);
        }
    }
}