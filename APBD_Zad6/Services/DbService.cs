using APBD_Zad6.Data;
using APBD_Zad6.DTOs;
using APBD_Zad6.Exceptions;
using APBD_Zad6.Models;
using Microsoft.EntityFrameworkCore;

namespace APBD_Zad6.Services;

public class DbService : IDbService
{
    private readonly HospitalContext _context;

    public DbService(HospitalContext context)
    {
        _context = context;
    }

    public async Task<List<PatientDto>> GetPatients(string? search)
    {
        var query = _context.Patients.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search}%";
            query = query.Where(p =>
                EF.Functions.Like(p.FirstName, pattern) ||
                EF.Functions.Like(p.LastName, pattern));
        }

        return await query
            .Select(p => new PatientDto
            {
                Pesel = p.Pesel,
                FirstName = p.FirstName,
                LastName = p.LastName,
                Age = p.Age,
                Sex = p.Sex ? "Male" : "Female",
                Admissions = p.Admissions.Select(a => new AdmissionDto
                {
                    Id = a.Id,
                    AdmissionDate = a.AdmissionDate,
                    DischargeDate = a.DischargeDate,
                    Ward = new WardDto
                    {
                        Id = a.Ward.Id,
                        Name = a.Ward.Name,
                        Description = a.Ward.Description
                    }
                }).ToList(),
                BedAssignments = p.BedAssignments.Select(ba => new BedAssignmentDto
                {
                    Id = ba.Id,
                    From = ba.From,
                    To = ba.To,
                    Bed = new BedDto
                    {
                        Id = ba.Bed.Id,
                        BedType = new BedTypeDto
                        {
                            Id = ba.Bed.BedType.Id,
                            Name = ba.Bed.BedType.Name,
                            Description = ba.Bed.BedType.Description
                        },
                        Room = new RoomDto
                        {
                            Id = ba.Bed.Room.Id,
                            HasTv = ba.Bed.Room.HasTv,
                            Ward = new WardDto
                            {
                                Id = ba.Bed.Room.Ward.Id,
                                Name = ba.Bed.Room.Ward.Name,
                                Description = ba.Bed.Room.Ward.Description
                            }
                        }
                    }
                }).ToList()
            })
            .ToListAsync();
    }

    public async Task<int> AssignBed(string pesel, CreateBedAssignmentDto dto)
    {
        var patient = await _context.Patients.FirstOrDefaultAsync(p => p.Pesel == pesel);
        if (patient is null)
            throw new NotFoundException("Patient not found.");

        var ward = await _context.Wards.FirstOrDefaultAsync(w => w.Name == dto.Ward);
        if (ward is null)
            throw new NotFoundException("Ward not found.");

        var bedType = await _context.BedTypes.FirstOrDefaultAsync(bt => bt.Name == dto.BedType);
        if (bedType is null)
            throw new NotFoundException("Bed type not found.");

        var freeBed = await _context.Beds
            .Where(b => b.BedTypeId == bedType.Id && b.Room.WardId == ward.Id)
            .Where(b => !b.BedAssignments.Any(ba =>
                (ba.To == null || ba.To > dto.From) &&
                (dto.To == null || ba.From < dto.To)))
            .FirstOrDefaultAsync();

        if (freeBed is null)
            throw new NotFoundException("No free bed of this type in this ward for the requested period.");

        var assignment = new BedAssignment
        {
            PatientPesel = pesel,
            BedId = freeBed.Id,
            From = dto.From,
            To = dto.To
        };

        _context.BedAssignments.Add(assignment);
        await _context.SaveChangesAsync();

        return assignment.Id;
    }
}