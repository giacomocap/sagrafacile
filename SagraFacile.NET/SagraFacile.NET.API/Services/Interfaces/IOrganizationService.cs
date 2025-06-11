using SagraFacile.NET.API.DTOs; // Add DTO namespace
using SagraFacile.NET.API.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SagraFacile.NET.API.Services.Interfaces
{
    public interface IOrganizationService
    {
        Task<IEnumerable<OrganizationDto>> GetAllOrganizationsAsync(); // Use DTO
        Task<Organization?> GetOrganizationByIdAsync(int id);
        Task<OrganizationDto?> GetOrganizationBySlugAsync(string slug); // New method
        Task<Organization> CreateOrganizationAsync(Organization organization);
        Task<bool> UpdateOrganizationAsync(int id, Organization organization);
        Task<bool> DeleteOrganizationAsync(int id);
        Task<bool> OrganizationExistsAsync(int id); // Optional helper
    }
}
