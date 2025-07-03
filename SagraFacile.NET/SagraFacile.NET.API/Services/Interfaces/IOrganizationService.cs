using SagraFacile.NET.API.DTOs;
using SagraFacile.NET.API.Models.Results;
using SagraFacile.NET.API.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SagraFacile.NET.API.Services.Interfaces
{
    public interface IOrganizationService
    {
        Task<IEnumerable<OrganizationDto>> GetAllOrganizationsAsync();
        Task<OrganizationDto?> GetOrganizationByIdAsync(Guid id);
        Task<OrganizationDto?> GetOrganizationBySlugAsync(string slug);
        Task<Organization> CreateOrganizationAsync(Organization organization);
        Task<bool> UpdateOrganizationAsync(Guid id, Organization organization);
        Task<ServiceResult<OrganizationDto>> ProvisionOrganizationAsync(OrganizationProvisionRequestDto provisionDto, string userId);
        Task<bool> DeleteOrganizationAsync(Guid id);
        Task<bool> OrganizationExistsAsync(Guid id);
    }
}
