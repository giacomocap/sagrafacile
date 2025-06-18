using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SagraFacile.NET.API.Data;
using SagraFacile.NET.API.DTOs; // Add DTO using
using SagraFacile.NET.API.Models;
using SagraFacile.NET.API.Services.Interfaces;

namespace SagraFacile.NET.API.Services
{
    // Inherit from BaseService
    public class MenuCategoryService : BaseService, IMenuCategoryService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<MenuCategoryService> _logger; // Added for logging
        // IHttpContextAccessor is now inherited from BaseService

        public MenuCategoryService(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor, ILogger<MenuCategoryService> logger)
            : base(httpContextAccessor) // Call base constructor
        {
            _context = context;
            _logger = logger; // Initialize logger
        }

        // GetUserContext helper is now inherited from BaseService

        public async Task<IEnumerable<MenuCategoryDto>?> GetCategoriesByAreaAsync(int areaId) // DTO return type, nullable
        {
            _logger.LogInformation("Attempting to retrieve menu categories for Area ID: {AreaId}.", areaId);
            // Check if the call is from an authenticated context
            bool isAuthenticated = _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;

            var query = _context.MenuCategories
                                .Where(mc => mc.AreaId == areaId);

            if (isAuthenticated)
            {
                // If authenticated, perform authorization checks
                var (userOrganizationId, isSuperAdmin) = GetUserContext(); // Safe to call now

                if (!isSuperAdmin)
                {
                    // Verify the requested area exists and belongs to the user's organization
                    var area = await _context.Areas
                                             .AsNoTracking() // Read-only check
                                             .FirstOrDefaultAsync(a => a.Id == areaId);
                    if (area == null)
                    {
                        _logger.LogWarning("GetCategoriesByAreaAsync: Area with ID {AreaId} not found for authenticated user.", areaId);
                        // Area doesn't exist at all - return null to indicate NotFound
                        return null;
                    }
                    if (area.OrganizationId != userOrganizationId)
                    {
                        _logger.LogWarning("GetCategoriesByAreaAsync: User {UserId} denied access to area {AreaId} belonging to organization {OrganizationId}.", GetUserId(), areaId, area.OrganizationId);
                        // Area exists, but doesn't belong to user's org - throw Forbidden
                        throw new UnauthorizedAccessException($"Access denied to area with ID {areaId}.");
                    }
                    // If we reach here, the area exists and belongs to the user's organization.
                }
                // SuperAdmin gets all categories for the given areaId without org check.
            }
            else
            {
                _logger.LogInformation("GetCategoriesByAreaAsync: Anonymous access for Area ID: {AreaId}.", areaId);
                // Anonymous access: Check if the area itself exists. If not, return empty list.
                // This prevents leaking information about non-existent areas.
                var areaExists = await _context.Areas.AnyAsync(a => a.Id == areaId);
                if (!areaExists)
                {
                    _logger.LogWarning("GetCategoriesByAreaAsync: Area with ID {AreaId} not found for anonymous request.", areaId);
                    // Area not found for anonymous request - return null
                    return null;
                }
                // If area exists, proceed to fetch categories without organization check.
            }

            var categories = await query.Select(mc => new MenuCategoryDto
            {
                Id = mc.Id,
                Name = mc.Name,
                AreaId = mc.AreaId
            }).ToListAsync();

            _logger.LogInformation("Successfully retrieved {CategoryCount} menu categories for Area ID: {AreaId}.", categories.Count, areaId);
            return categories;
        }

        public async Task<MenuCategoryDto?> GetCategoryByIdAsync(int id) // DTO return type
        {
            _logger.LogInformation("Attempting to retrieve menu category by ID: {CategoryId}.", id);
            var (userOrganizationId, isSuperAdmin) = GetUserContext();

            var category = await _context.MenuCategories
                                         .Include(mc => mc.Area) // Include Area for organization check
                                         .FirstOrDefaultAsync(mc => mc.Id == id);

            if (category == null)
            {
                _logger.LogWarning("Menu category with ID {CategoryId} not found.", id);
                return null; // Not found
            }

            if (!isSuperAdmin && category.Area?.OrganizationId != userOrganizationId)
            {
                _logger.LogWarning("User {UserId} denied access to menu category {CategoryId} belonging to organization {OrganizationId}.", GetUserId(), id, category.Area?.OrganizationId);
                // Found, but doesn't belong to user's organization
                return null; // Treat as not found from user's perspective
            }

            _logger.LogInformation("Successfully retrieved menu category {CategoryId}.", id);
            // Project to DTO
            return new MenuCategoryDto
            {
                Id = category.Id,
                Name = category.Name,
                AreaId = category.AreaId
            };
        }

        public async Task<MenuCategoryDto> CreateCategoryAsync(MenuCategory category) // DTO return type
        {
            _logger.LogInformation("Attempting to create menu category '{CategoryName}' for Area ID: {AreaId}.", category.Name, category.AreaId);
            var (userOrganizationId, isSuperAdmin) = GetUserContext();

            // Verify the target Area exists and belongs to the user's organization (if not SuperAdmin)
            var targetArea = await _context.Areas.FindAsync(category.AreaId);
            if (targetArea == null)
            {
                _logger.LogWarning("CreateCategoryAsync failed: Target Area with ID {AreaId} not found.", category.AreaId);
                throw new KeyNotFoundException($"Area with ID {category.AreaId} not found.");
            }

            if (!isSuperAdmin && targetArea.OrganizationId != userOrganizationId)
            {
                _logger.LogWarning("User {UserId} denied access to create category in area {AreaId} belonging to another organization {OrganizationId}.", GetUserId(), category.AreaId, targetArea.OrganizationId);
                throw new UnauthorizedAccessException($"Cannot create category in area belonging to another organization (Area ID: {category.AreaId}).");
            }

            _context.MenuCategories.Add(category);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Menu category '{CategoryName}' (ID: {CategoryId}) created successfully for Area ID: {AreaId}.", category.Name, category.Id, category.AreaId);
            // Return DTO
            return new MenuCategoryDto
            {
                Id = category.Id,
                Name = category.Name,
                AreaId = category.AreaId
            };
        }

        public async Task<bool> UpdateCategoryAsync(int id, MenuCategory categoryUpdateData)
        {
            _logger.LogInformation("Attempting to update menu category ID: {CategoryId}.", id);
            if (id != categoryUpdateData.Id)
            {
                _logger.LogWarning("UpdateCategoryAsync failed: ID mismatch in route ({RouteId}) vs body ({BodyId}).", id, categoryUpdateData.Id);
                // ID mismatch in route vs body
                return false;
            }

            var (userOrganizationId, isSuperAdmin) = GetUserContext();

            // Fetch the existing category, including its Area for checks
            var existingCategory = await _context.MenuCategories
                                                 .Include(mc => mc.Area)
                                                 .FirstOrDefaultAsync(mc => mc.Id == id);

            if (existingCategory == null)
            {
                _logger.LogWarning("UpdateCategoryAsync failed: Menu category with ID {CategoryId} not found.", id);
                throw new KeyNotFoundException($"Menu Category with ID {id} not found.");
            }

            // Verify user has access to the *existing* category's organization
            if (!isSuperAdmin && existingCategory.Area?.OrganizationId != userOrganizationId)
            {
                _logger.LogWarning("User {UserId} denied access to update menu category {CategoryId} belonging to organization {OrganizationId}.", GetUserId(), id, existingCategory.Area?.OrganizationId);
                throw new UnauthorizedAccessException($"Access denied to update menu category with ID {id}.");
            }

            // If AreaId is being changed, verify access to the *new* area's organization
            if (existingCategory.AreaId != categoryUpdateData.AreaId)
            {
                _logger.LogInformation("Menu category {CategoryId} is being moved from Area {OldAreaId} to Area {NewAreaId}.", id, existingCategory.AreaId, categoryUpdateData.AreaId);
                var newArea = await _context.Areas.FindAsync(categoryUpdateData.AreaId);
                if (newArea == null)
                {
                    _logger.LogWarning("UpdateCategoryAsync failed: Target Area with ID {AreaId} not found for category move.", categoryUpdateData.AreaId);
                    throw new KeyNotFoundException($"Target Area with ID {categoryUpdateData.AreaId} not found.");
                }
                if (!isSuperAdmin && newArea.OrganizationId != userOrganizationId)
                {
                    _logger.LogWarning("User {UserId} denied access to move category {CategoryId} to area {NewAreaId} belonging to another organization {NewOrgId}.", GetUserId(), id, categoryUpdateData.AreaId, newArea.OrganizationId);
                    throw new UnauthorizedAccessException($"Cannot move category to area belonging to another organization (Area ID: {categoryUpdateData.AreaId}).");
                }
                existingCategory.AreaId = categoryUpdateData.AreaId; // Update AreaId
            }

            // Update other properties
            existingCategory.Name = categoryUpdateData.Name;
            // Add other updatable properties here if any

            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Menu category {CategoryId} updated successfully.", id);
                return true;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogError(ex, "DbUpdateConcurrencyException during UpdateCategoryAsync for category {CategoryId}.", id);
                // Check if it was deleted concurrently
                if (!await _context.MenuCategories.AnyAsync(e => e.Id == id))
                {
                    throw new KeyNotFoundException($"Menu Category with ID {id} not found during update.");
                }
                else
                {
                    throw; // Re-throw other concurrency issues
                }
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "DbUpdateException during UpdateCategoryAsync for category {CategoryId}.", id);
                return false; // Or re-throw specific exceptions if needed
            }
        }

        public async Task<bool> DeleteCategoryAsync(int id)
        {
            _logger.LogInformation("Attempting to delete menu category ID: {CategoryId}.", id);
            var (userOrganizationId, isSuperAdmin) = GetUserContext();

            // Fetch the category including Area for organization check
            var category = await _context.MenuCategories
                                         .Include(mc => mc.Area)
                                         .FirstOrDefaultAsync(mc => mc.Id == id);

            if (category == null)
            {
                _logger.LogWarning("DeleteCategoryAsync failed: Menu category with ID {CategoryId} not found.", id);
                return false; // Not found
            }

            // Verify user has access to the category's organization
            if (!isSuperAdmin && category.Area?.OrganizationId != userOrganizationId)
            {
                _logger.LogWarning("User {UserId} denied access to delete menu category {CategoryId} belonging to organization {OrganizationId}.", GetUserId(), id, category.Area?.OrganizationId);
                throw new UnauthorizedAccessException($"Access denied to delete menu category with ID {id}.");
            }

            try
            {
                _context.MenuCategories.Remove(category);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Menu category {CategoryId} deleted successfully.", id);
                return true;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "DbUpdateException during DeleteCategoryAsync for category {CategoryId}. It might be in use.", id);
                // Could fail due to FK constraints (e.g., MenuItems referencing it if cascade delete isn't set up correctly)
                return false;
            }
        }

        // This internal check doesn't need tenancy context as it's only used after existence is confirmed
        // by other methods that *do* check tenancy. If exposed publicly or used differently, it would need context.
        public async Task<bool> CategoryExistsAsync(int id)
        {
            _logger.LogDebug("Checking if menu category {CategoryId} exists.", id);
            return await _context.MenuCategories.AnyAsync(e => e.Id == id);
        }
    }
}
