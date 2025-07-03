using SagraFacile.NET.API.DTOs; // Add DTO namespace
using SagraFacile.NET.API.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SagraFacile.NET.API.Services.Interfaces
{
    public interface IOrganizationService
    {
        Task<IEnumerable<OrganizationDto>> GetAllOrganizationsAsync(); // Use DTO
        Task<Organization?> GetOrganizationByIdAsync(Guid id);
        Task<OrganizationDto?> GetOrganizationBySlugAsync(string slug); // New method
        Task<Organization> CreateOrganizationAsync(Organization organization);
        Task<bool> UpdateOrganizationAsync(Guid id, Organization organization);
        Task<bool> DeleteOrganizationAsync(Guid id);
        Task<bool> OrganizationExistsAsync(Guid id); // Optional helper
    }
}
