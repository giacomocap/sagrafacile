using SagraFacile.NET.API.Models;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace SagraFacile.NET.API.Services.Interfaces
{
    public interface IKdsStationService
    {
        Task<IEnumerable<KdsStation>> GetKdsStationsByAreaAsync(int organizationId, int areaId, ClaimsPrincipal user);
        Task<KdsStation?> GetKdsStationByIdAsync(int organizationId, int areaId, int kdsStationId, ClaimsPrincipal user);
        Task<KdsStation> CreateKdsStationAsync(int organizationId, int areaId, KdsStation newKdsStation, ClaimsPrincipal user);
        Task<bool> UpdateKdsStationAsync(int organizationId, int areaId, int kdsStationId, KdsStation updatedKdsStation, ClaimsPrincipal user);
        Task<bool> DeleteKdsStationAsync(int organizationId, int areaId, int kdsStationId, ClaimsPrincipal user);

        Task<IEnumerable<MenuCategory>> GetAssignedCategoriesAsync(int organizationId, int areaId, int kdsStationId, ClaimsPrincipal user);
        Task<bool> AssignCategoryAsync(int organizationId, int areaId, int kdsStationId, int menuCategoryId, ClaimsPrincipal user);
        Task<bool> UnassignCategoryAsync(int organizationId, int areaId, int kdsStationId, int menuCategoryId, ClaimsPrincipal user);
    }
}
