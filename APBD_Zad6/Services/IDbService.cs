using APBD_Zad6.DTOs;

namespace APBD_Zad6.Services;

public interface IDbService
{
    Task<List<PatientDto>> GetPatients(string? search);
    Task<int> AssignBed(string pesel, CreateBedAssignmentDto dto);
}