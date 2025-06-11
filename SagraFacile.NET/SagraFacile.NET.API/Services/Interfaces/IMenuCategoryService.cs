using SagraFacile.NET.API.DTOs; // Added
using SagraFacile.NET.API.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SagraFacile.NET.API.Services.Interfaces
{
    public interface IMenuCategoryService
    {
        // Filter by AreaId is essential here
        Task<IEnumerable<MenuCategoryDto>?> GetCategoriesByAreaAsync(int areaId); // Return nullable
        Task<MenuCategoryDto?> GetCategoryByIdAsync(int id); // Should also check if it belongs to user's accessible areas later
        Task<MenuCategoryDto> CreateCategoryAsync(MenuCategory category); // Ensure AreaId is valid and accessible, return DTO
        Task<bool> UpdateCategoryAsync(int id, MenuCategory category); // Ensure AreaId is valid and accessible
        Task<bool> DeleteCategoryAsync(int id); // Check accessibility
        Task<bool> CategoryExistsAsync(int id); // Check accessibility
    }
}
