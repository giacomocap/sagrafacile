using SagraFacile.NET.API.Models;
using SagraFacile.NET.API.DTOs;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SagraFacile.NET.API.Services.Interfaces
{
    public interface ICashierStationService
    {
        Task<CashierStationDto?> GetStationByIdAsync(int stationId, User currentUser);
        Task<IEnumerable<CashierStationDto>> GetStationsByOrganizationAsync(int organizationId, User currentUser);
        Task<IEnumerable<CashierStationDto>> GetStationsByAreaAsync(int areaId, User currentUser);
        Task<(CashierStationDto? Station, string? Error)> CreateStationAsync(int organizationId, CashierStationUpsertDto dto, User currentUser);
        Task<(CashierStationDto? Station, string? Error)> UpdateStationAsync(int stationId, CashierStationUpsertDto dto, User currentUser);
        Task<(bool Success, string? Error)> DeleteStationAsync(int stationId, User currentUser);
    }
} 