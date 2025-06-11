using SagraFacile.NET.API.DTOs; // Added for MenuItemDto
using SagraFacile.NET.API.Models;
using SagraFacile.NET.API.Models.Results; // Added for ServiceResult
using System.Collections.Generic;
// using System.Security.Claims; // Removed as ClaimsPrincipal is no longer passed
using System.Threading.Tasks;

namespace SagraFacile.NET.API.Services.Interfaces
{
    public interface IMenuItemService
    {
        // Filter by CategoryId is essential
        Task<IEnumerable<MenuItemDto>?> GetItemsByCategoryAsync(int categoryId); // Return DTO?, nullable
        Task<MenuItemDto?> GetItemByIdAsync(int id); // Return DTO? Check accessibility later
        Task<MenuItemDto> CreateItemAsync(MenuItemUpsertDto itemDto); // Return DTO, Check category accessibility later
        Task<bool> UpdateItemAsync(int id, MenuItemUpsertDto itemDto); // Accept DTO, Check category accessibility later
        Task<bool> DeleteItemAsync(int id); // Check accessibility later
        Task<bool> ItemExistsAsync(int id); // Check accessibility later

        // Stock Management Methods
        Task<ServiceResult> UpdateStockAsync(int menuItemId, int? newScorta);
        Task<ServiceResult> ResetStockAsync(int menuItemId);
        Task<ServiceResult> ResetAllStockForAreaAsync(int areaId);
    }
}
