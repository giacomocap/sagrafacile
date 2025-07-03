using SagraFacile.NET.API.Models;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace SagraFacile.NET.API.Services.Interfaces
{
    public interface IKdsStationService
    {
        Task<IEnumerable<KdsStation>> GetKdsStationsByAreaAsync(Guid organizationId, int areaId, ClaimsPrincipal user);
        Task<KdsStation?> GetKdsStationByIdAsync(Guid organizationId, int areaId, int kdsStationId, ClaimsPrincipal user);
        Task<KdsStation> CreateKdsStationAsync(Guid organizationId, int areaId, KdsStation newKdsStation, ClaimsPrincipal user);
        Task<bool> UpdateKdsStationAsync(Guid organizationId, int areaId, int kdsStationId, KdsStation updatedKdsStation, ClaimsPrincipal user);
        Task<bool> DeleteKdsStationAsync(Guid organizationId, int areaId, int kdsStationId, ClaimsPrincipal user);

        Task<IEnumerable<MenuCategory>> GetAssignedCategoriesAsync(Guid organizationId, int areaId, int kdsStationId, ClaimsPrincipal user);
        Task<bool> AssignCategoryAsync(Guid organizationId, int areaId, int kdsStationId, int menuCategoryId, ClaimsPrincipal user);
        Task<bool> UnassignCategoryAsync(Guid organizationId, int areaId, int kdsStationId, int menuCategoryId, ClaimsPrincipal user);
    }
}
