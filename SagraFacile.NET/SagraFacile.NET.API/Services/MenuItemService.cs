using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR; // Added for SignalR
using SagraFacile.NET.API.Data;
using SagraFacile.NET.API.DTOs; // Added for MenuItemDto
using SagraFacile.NET.API.Hubs; // Added for OrderHub
using SagraFacile.NET.API.Models;
using SagraFacile.NET.API.Models.Results; // Added for ServiceResult
using SagraFacile.NET.API.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace SagraFacile.NET.API.Services
{
    // Inherit from BaseService
    public class MenuItemService : BaseService, IMenuItemService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<OrderHub> _hubContext; // Added for SignalR
        // IHttpContextAccessor is now inherited from BaseService

        public MenuItemService(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor, IHubContext<OrderHub> hubContext) // Injected IHubContext
            : base(httpContextAccessor) // Call base constructor
        {
            _context = context;
            _hubContext = hubContext; // Store injected IHubContext
        }

        // GetUserContext helper is now inherited from BaseService

        public async Task<IEnumerable<MenuItemDto>?> GetItemsByCategoryAsync(int categoryId) // Return DTO?, nullable
        {
            // Check if the call is from an authenticated context
            bool isAuthenticated = _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;

            if (isAuthenticated)
            {
                // Authenticated: Perform authorization checks
                var (userOrganizationId, isSuperAdmin) = GetUserContext(); // Safe to call now

                // Verify the user has access to the category itself
                var category = await _context.MenuCategories
                                             .Include(mc => mc.Area) // Include Area for org check
                                             .AsNoTracking() // Read-only check
                                             .FirstOrDefaultAsync(mc => mc.Id == categoryId);

                if (category == null)
                {
                    // Category doesn't exist - return null to indicate NotFound
                    return null;
                }

                if (!isSuperAdmin && category.Area?.OrganizationId != userOrganizationId)
                {
                    // User doesn't have access to this category's organization - throw Forbidden
                    throw new UnauthorizedAccessException($"Access denied to menu category with ID {categoryId}.");
                }
                // If we reach here, the category exists and belongs to the user's organization (or user is SuperAdmin).
            }
            else
            {
                // Anonymous access: Check if the category exists publicly.
                var categoryExists = await _context.MenuCategories.AnyAsync(mc => mc.Id == categoryId);
                if (!categoryExists)
                {
                    // Category not found for anonymous request - return null
                    return null;
                }
                // Category exists, proceed to fetch items without organization check.
            }

            // Fetch the items for the validated categoryId
            var items = await _context.MenuItems
                                      .Where(mi => mi.MenuCategoryId == categoryId)
                                      .ToListAsync();

            // Map to DTO
            return items.Select(item => new MenuItemDto
            {
                Id = item.Id,
                Name = item.Name,
                Price = item.Price,
                MenuCategoryId = item.MenuCategoryId,
                IsNoteRequired = item.IsNoteRequired,
                NoteSuggestion = item.NoteSuggestion,
                Scorta = item.Scorta
                // Description = item.Description // Add if Description exists in MenuItem model
            });
        }

        public async Task<MenuItemDto?> GetItemByIdAsync(int id) // Return DTO?
        {
            var (userOrganizationId, isSuperAdmin) = GetUserContext();

            var item = await _context.MenuItems
                                     .Include(mi => mi.MenuCategory)
                                        .ThenInclude(mc => mc.Area) // Include Category -> Area for org check
                                     .FirstOrDefaultAsync(mi => mi.Id == id);

            if (item == null)
            {
                return null; // Not found
            }

            // Add explicit null checks for MenuCategory and Area before accessing OrganizationId
            if (!isSuperAdmin && (item.MenuCategory == null || item.MenuCategory.Area == null || item.MenuCategory.Area.OrganizationId != userOrganizationId))
            {
                // Found, but doesn't belong to user's organization (or data is inconsistent)
                return null; // Treat as not found/inaccessible from user's perspective
            }

            // Map to DTO
            return new MenuItemDto
            {
                Id = item.Id,
                Name = item.Name,
                Price = item.Price,
                MenuCategoryId = item.MenuCategoryId,
                IsNoteRequired = item.IsNoteRequired,
                NoteSuggestion = item.NoteSuggestion,
                Scorta = item.Scorta
                // Description = item.Description // Add if Description exists in MenuItem model
            };
        }

        public async Task<MenuItemDto> CreateItemAsync(MenuItemUpsertDto itemDto) // Return DTO, changed to accept DTO
        {
            var (userOrganizationId, isSuperAdmin) = GetUserContext();

            // Verify the target Category exists and belongs to the user's organization (if not SuperAdmin)
            var targetCategory = await _context.MenuCategories
                                               .Include(mc => mc.Area) // Include Area for org check
                                               .FirstOrDefaultAsync(mc => mc.Id == itemDto.MenuCategoryId);

            if (targetCategory == null)
            {
                throw new KeyNotFoundException($"Menu Category with ID {itemDto.MenuCategoryId} not found.");
            }

            if (!isSuperAdmin && targetCategory.Area?.OrganizationId != userOrganizationId)
            {
                throw new UnauthorizedAccessException($"Cannot create item in category belonging to another organization (Category ID: {itemDto.MenuCategoryId}).");
            }

            var newItem = new MenuItem
            {
                Name = itemDto.Name,
                Description = itemDto.Description,
                Price = itemDto.Price,
                MenuCategoryId = itemDto.MenuCategoryId,
                IsNoteRequired = itemDto.IsNoteRequired,
                NoteSuggestion = itemDto.NoteSuggestion,
                Scorta = itemDto.Scorta // Set Scorta
            };

            _context.MenuItems.Add(newItem);
            await _context.SaveChangesAsync();

            // Map to DTO before returning
            return new MenuItemDto(newItem); // Use constructor for mapping
        }

        // Updated to accept MenuItemUpsertDto
        public async Task<bool> UpdateItemAsync(int id, MenuItemUpsertDto itemDto)
        {
            // ID mismatch check removed as DTO doesn't contain ID

            var (userOrganizationId, isSuperAdmin) = GetUserContext();

            // Fetch the existing item, including its Category -> Area for checks
            var existingItem = await _context.MenuItems
                                             .Include(mi => mi.MenuCategory)
                                                .ThenInclude(mc => mc.Area)
                                             .FirstOrDefaultAsync(mi => mi.Id == id);

            if (existingItem == null)
            {
                throw new KeyNotFoundException($"Menu Item with ID {id} not found.");
            }

            // Verify user has access to the *existing* item's organization
            // Add explicit null checks for MenuCategory and Area
            if (!isSuperAdmin && (existingItem.MenuCategory == null || existingItem.MenuCategory.Area == null || existingItem.MenuCategory.Area.OrganizationId != userOrganizationId))
            {
                // Treat inconsistent data or access denial the same way
                throw new UnauthorizedAccessException($"Access denied to update menu item with ID {id}.");
            }

            // If MenuCategoryId is being changed, verify access to the *new* category's organization
            if (existingItem.MenuCategoryId != itemDto.MenuCategoryId)
            {
                var newCategory = await _context.MenuCategories
                                                .Include(mc => mc.Area)
                                                .FirstOrDefaultAsync(mc => mc.Id == itemDto.MenuCategoryId);
                if (newCategory == null)
                {
                    throw new KeyNotFoundException($"Target Menu Category with ID {itemDto.MenuCategoryId} not found.");
                }
                if (!isSuperAdmin && newCategory.Area?.OrganizationId != userOrganizationId)
                {
                    throw new UnauthorizedAccessException($"Cannot move item to category belonging to another organization (Category ID: {itemDto.MenuCategoryId}).");
                }
                existingItem.MenuCategoryId = itemDto.MenuCategoryId; // Update CategoryId
            }

            // Update other properties from DTO
            existingItem.Name = itemDto.Name;
            existingItem.Price = itemDto.Price; // Assuming MenuItem.Price is decimal
            existingItem.Description = itemDto.Description; // Update Description if it exists in MenuItem model
            existingItem.IsNoteRequired = itemDto.IsNoteRequired;
            existingItem.NoteSuggestion = itemDto.NoteSuggestion;
            existingItem.Scorta = itemDto.Scorta; // Update Scorta

            try
            {
                await _context.SaveChangesAsync();
                return true;
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _context.MenuItems.AnyAsync(e => e.Id == id))
                {
                    throw new KeyNotFoundException($"Menu Item with ID {id} not found during update.");
                }
                else
                {
                    throw;
                }
            }
            catch (DbUpdateException ex)
            {
                Console.WriteLine($"DbUpdateException during UpdateItemAsync: {ex}"); // Replace with proper logging
                return false;
            }
        }

        public async Task<bool> DeleteItemAsync(int id)
        {
            var (userOrganizationId, isSuperAdmin) = GetUserContext();

            // Fetch the item including Category -> Area for organization check
            var item = await _context.MenuItems
                                     .Include(mi => mi.MenuCategory)
                                        .ThenInclude(mc => mc.Area)
                                     .FirstOrDefaultAsync(mi => mi.Id == id);

            if (item == null)
            {
                return false; // Not found
            }

            // Verify user has access to the item's organization
            // Add explicit null checks for MenuCategory and Area
            if (!isSuperAdmin && (item.MenuCategory == null || item.MenuCategory.Area == null || item.MenuCategory.Area.OrganizationId != userOrganizationId))
            {
                // Treat inconsistent data or access denial the same way
                throw new UnauthorizedAccessException($"Access denied to delete menu item with ID {id}.");
            }

            try
            {
                _context.MenuItems.Remove(item);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (DbUpdateException ex)
            {
                // Log the exception details for debugging
                Console.WriteLine($"DbUpdateException during DeleteItemAsync: {ex}"); // Replace with proper logging
                // Could fail due to FK constraints (e.g., OrderItems referencing it)
                return false;
            }
        }

        // This internal check doesn't need tenancy context as it's only used after existence is confirmed
        // by other methods that *do* check tenancy.
        public async Task<bool> ItemExistsAsync(int id)
        {
            // Note: If this were public or used differently, it *would* need tenancy checks.
            return await _context.MenuItems.AnyAsync(e => e.Id == id);
        }

        public async Task<ServiceResult> UpdateStockAsync(int menuItemId, int? newScorta) // Removed ClaimsPrincipal user
        {
            var (userOrganizationId, isSuperAdmin) = GetUserContext(); 

            var menuItem = await _context.MenuItems
                                         .Include(mi => mi.MenuCategory)
                                         .ThenInclude(mc => mc.Area)
                                         .FirstOrDefaultAsync(mi => mi.Id == menuItemId);

            if (menuItem == null)
            {
                return ServiceResult.Fail($"Menu Item with ID {menuItemId} not found.");
            }

            if (menuItem.MenuCategory == null || menuItem.MenuCategory.Area == null)
            {
                // This case should ideally not happen with well-structured data.
                // Log this as a data integrity issue.
                // For now, treat as an error preventing stock update.
                return ServiceResult.Fail($"Menu Item with ID {menuItemId} has inconsistent category or area data.");
            }
            
            var itemOrganizationId = menuItem.MenuCategory.Area.OrganizationId;

            if (!isSuperAdmin && itemOrganizationId != userOrganizationId)
            {
                return ServiceResult.Fail("Access denied to update stock for this menu item.");
            }

            menuItem.Scorta = newScorta;

            try
            {
                await _context.SaveChangesAsync();

                // Broadcast stock update via SignalR
                var stockUpdateDto = new StockUpdateBroadcastDto
                {
                    MenuItemId = menuItem.Id,
                    AreaId = menuItem.MenuCategory.AreaId, // Assuming MenuCategory has AreaId
                    NewScorta = menuItem.Scorta,
                    Timestamp = DateTime.UtcNow
                };
                await _hubContext.Clients.Group($"Area-{menuItem.MenuCategory.AreaId}").SendAsync("ReceiveStockUpdate", stockUpdateDto);
                
                return ServiceResult.Ok(); // Corrected to use Ok() method
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await ItemExistsAsync(menuItemId))
                {
                    return ServiceResult.Fail($"Menu Item with ID {menuItemId} not found during stock update.");
                }
                throw; // Re-throw if it's a genuine concurrency issue on an existing item
            }
            catch (Exception ex)
            {
                // Log the exception (ex)
                return ServiceResult.Fail($"An error occurred while updating stock for menu item {menuItemId}: {ex.Message}");
            }
        }

        public async Task<ServiceResult> ResetStockAsync(int menuItemId) // Removed ClaimsPrincipal user
        {
            return await UpdateStockAsync(menuItemId, null); // Resetting is updating to null, removed user argument
        }

        public async Task<ServiceResult> ResetAllStockForAreaAsync(int areaId) // Removed ClaimsPrincipal user
        {
            var (userOrganizationId, isSuperAdmin) = GetUserContext(); 

            var area = await _context.Areas.FindAsync(areaId);
            if (area == null)
            {
                return ServiceResult.Fail($"Area with ID {areaId} not found.");
            }

            if (!isSuperAdmin && area.OrganizationId != userOrganizationId)
            {
                return ServiceResult.Fail("Access denied to reset stock for this area.");
            }

            var menuItemsInArea = await _context.MenuItems
                                                .Include(mi => mi.MenuCategory)
                                                .Where(mi => mi.MenuCategory != null && mi.MenuCategory.AreaId == areaId)
                                                .ToListAsync();
            
            if (!menuItemsInArea.Any())
            {
                // If no items, it's still a successful operation in terms of not failing.
                // The original architecture doc implies returning success.
                return ServiceResult.Ok(); 
            }

            foreach (var item in menuItemsInArea)
            {
                item.Scorta = null;
            }

            try
            {
                await _context.SaveChangesAsync();

                // Broadcast multiple stock updates
                foreach (var item in menuItemsInArea)
                {
                    var stockUpdateDto = new StockUpdateBroadcastDto
                    {
                        MenuItemId = item.Id,
                        AreaId = areaId,
                        NewScorta = null, // Reset to null
                        Timestamp = DateTime.UtcNow
                    };
                    // Ensure item.MenuCategory is not null before accessing AreaId, though the query implies it.
                    // The group name should be consistent with how clients join.
                    await _hubContext.Clients.Group($"Area-{areaId}").SendAsync("ReceiveStockUpdate", stockUpdateDto);
                }
                
                return ServiceResult.Ok(); // Corrected to use Ok() method
            }
            catch (Exception ex)
            {
                // Log the exception (ex)
                return ServiceResult.Fail($"An error occurred while resetting all stock for area {areaId}: {ex.Message}");
            }
        }
    }
}
