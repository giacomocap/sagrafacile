using SagraFacile.NET.API.DTOs; // Added for AreaDto
using SagraFacile.NET.API.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SagraFacile.NET.API.Services.Interfaces
{
    public interface IAreaService
    {
        Task<IEnumerable<AreaDto>> GetAllAreasAsync(); // Return DTO
        Task<AreaDto?> GetAreaByIdAsync(int id); // Return DTO?
        Task<AreaDto?> GetAreaBySlugsAsync(string orgSlug, string areaSlug); // New method
        Task<Area> CreateAreaAsync(Area area); // OrganizationId within Area will be Guid
        Task<bool> UpdateAreaAsync(int id, AreaUpsertDto areaDto); // Changed to AreaUpsertDto
        Task<bool> DeleteAreaAsync(int id);
        Task<bool> AreaExistsAsync(int id); // Optional helper
    }
}
