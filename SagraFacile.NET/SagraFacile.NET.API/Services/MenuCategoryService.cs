using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SagraFacile.NET.API.Data;
using SagraFacile.NET.API.DTOs; // Add DTO using
using SagraFacile.NET.API.Models;
using SagraFacile.NET.API.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace SagraFacile.NET.API.Services
{
    // Inherit from BaseService
    public class MenuCategoryService : BaseService, IMenuCategoryService
    {
        private readonly ApplicationDbContext _context;
        // IHttpContextAccessor is now inherited from BaseService

        public MenuCategoryService(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor)
            : base(httpContextAccessor) // Call base constructor
        {
            _context = context;
        }

        // GetUserContext helper is now inherited from BaseService

        public async Task<IEnumerable<MenuCategoryDto>?> GetCategoriesByAreaAsync(int areaId) // DTO return type, nullable
        {
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
                        // Area doesn't exist at all - return null to indicate NotFound
                        return null;
                    }
                    if (area.OrganizationId != userOrganizationId)
                    {
                        // Area exists, but doesn't belong to user's org - throw Forbidden
                        throw new UnauthorizedAccessException($"Access denied to area with ID {areaId}.");
                    }
                    // If we reach here, the area exists and belongs to the user's organization.
                }
                // SuperAdmin gets all categories for the given areaId without org check.
            }
            else
            {
                // Anonymous access: Check if the area itself exists. If not, return empty list.
                // This prevents leaking information about non-existent areas.
                var areaExists = await _context.Areas.AnyAsync(a => a.Id == areaId);
                if (!areaExists)
                {
                    // Area not found for anonymous request - return null
                    return null;
                }
                // If area exists, proceed to fetch categories without organization check.
            }

            // Project to DTO
            return await query.Select(mc => new MenuCategoryDto
            {
                Id = mc.Id,
                Name = mc.Name,
                AreaId = mc.AreaId
            }).ToListAsync();
        }

        public async Task<MenuCategoryDto?> GetCategoryByIdAsync(int id) // DTO return type
        {
            var (userOrganizationId, isSuperAdmin) = GetUserContext();

            var category = await _context.MenuCategories
                                         .Include(mc => mc.Area) // Include Area for organization check
                                         .FirstOrDefaultAsync(mc => mc.Id == id);

            if (category == null)
            {
                return null; // Not found
            }

            if (!isSuperAdmin && category.Area?.OrganizationId != userOrganizationId)
            {
                // Found, but doesn't belong to user's organization
                return null; // Treat as not found from user's perspective
            }

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
            var (userOrganizationId, isSuperAdmin) = GetUserContext();

            // Verify the target Area exists and belongs to the user's organization (if not SuperAdmin)
            var targetArea = await _context.Areas.FindAsync(category.AreaId);
            if (targetArea == null)
            {
                throw new KeyNotFoundException($"Area with ID {category.AreaId} not found.");
            }

            if (!isSuperAdmin && targetArea.OrganizationId != userOrganizationId)
            {
                throw new UnauthorizedAccessException($"Cannot create category in area belonging to another organization (Area ID: {category.AreaId}).");
            }

            _context.MenuCategories.Add(category);
            await _context.SaveChangesAsync();
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
            if (id != categoryUpdateData.Id)
            {
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
                throw new KeyNotFoundException($"Menu Category with ID {id} not found.");
            }

            // Verify user has access to the *existing* category's organization
            if (!isSuperAdmin && existingCategory.Area?.OrganizationId != userOrganizationId)
            {
                throw new UnauthorizedAccessException($"Access denied to update menu category with ID {id}.");
            }

            // If AreaId is being changed, verify access to the *new* area's organization
            if (existingCategory.AreaId != categoryUpdateData.AreaId)
            {
                var newArea = await _context.Areas.FindAsync(categoryUpdateData.AreaId);
                if (newArea == null)
                {
                    throw new KeyNotFoundException($"Target Area with ID {categoryUpdateData.AreaId} not found.");
                }
                if (!isSuperAdmin && newArea.OrganizationId != userOrganizationId)
                {
                    throw new UnauthorizedAccessException($"Cannot move category to area belonging to another organization (Area ID: {categoryUpdateData.AreaId}).");
                }
                existingCategory.AreaId = categoryUpdateData.AreaId; // Update AreaId
            }

            // Update other properties
            existingCategory.Name = categoryUpdateData.Name;
            // Add other updatable properties here if any

            // _context.Entry(existingCategory).State = EntityState.Modified; // Not needed when fetching and modifying tracked entity

            try
            {
                await _context.SaveChangesAsync();
                return true;
            }
            catch (DbUpdateConcurrencyException)
            {
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
                // Log the exception details for debugging
                Console.WriteLine($"DbUpdateException during UpdateCategoryAsync: {ex}"); // Replace with proper logging
                return false; // Or re-throw specific exceptions if needed
            }
        }

        public async Task<bool> DeleteCategoryAsync(int id)
        {
            var (userOrganizationId, isSuperAdmin) = GetUserContext();

            // Fetch the category including Area for organization check
            var category = await _context.MenuCategories
                                         .Include(mc => mc.Area)
                                         .FirstOrDefaultAsync(mc => mc.Id == id);

            if (category == null)
            {
                return false; // Not found
            }

            // Verify user has access to the category's organization
            if (!isSuperAdmin && category.Area?.OrganizationId != userOrganizationId)
            {
                throw new UnauthorizedAccessException($"Access denied to delete menu category with ID {id}.");
            }

            try
            {
                _context.MenuCategories.Remove(category);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (DbUpdateException ex)
            {
                // Log the exception details for debugging
                Console.WriteLine($"DbUpdateException during DeleteCategoryAsync: {ex}"); // Replace with proper logging
                // Could fail due to FK constraints (e.g., MenuItems referencing it if cascade delete isn't set up correctly)
                return false;
            }
        }

        // This internal check doesn't need tenancy context as it's only used after existence is confirmed
        // by other methods that *do* check tenancy. If exposed publicly or used differently, it would need context.
        public async Task<bool> CategoryExistsAsync(int id)
        {
            return await _context.MenuCategories.AnyAsync(e => e.Id == id);
        }
    }
}
