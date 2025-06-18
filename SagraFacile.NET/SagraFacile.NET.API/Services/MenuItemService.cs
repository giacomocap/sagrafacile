using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR; // Added for SignalR
using SagraFacile.NET.API.Data;
using SagraFacile.NET.API.DTOs; // Added for MenuItemDto
using SagraFacile.NET.API.Hubs; // Added for OrderHub
using SagraFacile.NET.API.Models;
using SagraFacile.NET.API.Models.Results; // Added for ServiceResult
using SagraFacile.NET.API.Services.Interfaces;

namespace SagraFacile.NET.API.Services
{
    // Inherit from BaseService
    public class MenuItemService : BaseService, IMenuItemService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<OrderHub> _hubContext; // Added for SignalR
        private readonly ILogger<MenuItemService> _logger; // Added for logging
        // IHttpContextAccessor is now inherited from BaseService

        public MenuItemService(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor, IHubContext<OrderHub> hubContext, ILogger<MenuItemService> logger) // Injected IHubContext and ILogger
            : base(httpContextAccessor) // Call base constructor
        {
            _context = context;
            _hubContext = hubContext; // Store injected IHubContext
            _logger = logger; // Initialize logger
        }

        // GetUserContext helper is now inherited from BaseService

        public async Task<IEnumerable<MenuItemDto>?> GetItemsByCategoryAsync(int categoryId) // Return DTO?, nullable
        {
            _logger.LogInformation("Attempting to retrieve menu items for Category ID: {CategoryId}.", categoryId);
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
                    _logger.LogWarning("GetItemsByCategoryAsync: Menu category with ID {CategoryId} not found for authenticated user.", categoryId);
                    // Category doesn't exist - return null to indicate NotFound
                    return null;
                }

                if (!isSuperAdmin && category.Area?.OrganizationId != userOrganizationId)
                {
                    _logger.LogWarning("GetItemsByCategoryAsync: User {UserId} denied access to menu category {CategoryId} belonging to organization {OrganizationId}.", GetUserId(), categoryId, category.Area?.OrganizationId);
                    // User doesn't have access to this category's organization - throw Forbidden
                    throw new UnauthorizedAccessException($"Access denied to menu category with ID {categoryId}.");
                }
                // If we reach here, the category exists and belongs to the user's organization (or user is SuperAdmin).
            }
            else
            {
                _logger.LogInformation("GetItemsByCategoryAsync: Anonymous access for Category ID: {CategoryId}.", categoryId);
                // Anonymous access: Check if the category exists publicly.
                var categoryExists = await _context.MenuCategories.AnyAsync(mc => mc.Id == categoryId);
                if (!categoryExists)
                {
                    _logger.LogWarning("GetItemsByCategoryAsync: Menu category with ID {CategoryId} not found for anonymous request.", categoryId);
                    // Category not found for anonymous request - return null
                    return null;
                }
                // Category exists, proceed to fetch items without organization check.
            }

            // Fetch the items for the validated categoryId
            var items = await _context.MenuItems
                                      .Where(mi => mi.MenuCategoryId == categoryId)
                                      .ToListAsync();

            _logger.LogInformation("Successfully retrieved {ItemCount} menu items for Category ID: {CategoryId}.", items.Count, categoryId);
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
            _logger.LogInformation("Attempting to retrieve menu item by ID: {ItemId}.", id);
            var (userOrganizationId, isSuperAdmin) = GetUserContext();

            var item = await _context.MenuItems
                                     .Include(mi => mi.MenuCategory)
                                        .ThenInclude(mc => mc.Area) // Include Category -> Area for org check
                                     .FirstOrDefaultAsync(mi => mi.Id == id);

            if (item == null)
            {
                _logger.LogWarning("Menu item with ID {ItemId} not found.", id);
                return null; // Not found
            }

            // Add explicit null checks for MenuCategory and Area before accessing OrganizationId
            if (!isSuperAdmin && (item.MenuCategory == null || item.MenuCategory.Area == null || item.MenuCategory.Area.OrganizationId != userOrganizationId))
            {
                _logger.LogWarning("User {UserId} denied access to menu item {ItemId} belonging to organization {OrganizationId}.", GetUserId(), id, item.MenuCategory?.Area?.OrganizationId);
                // Treat inconsistent data or access denial the same way
                return null; // Treat as not found/inaccessible from user's perspective
            }

            _logger.LogInformation("Successfully retrieved menu item {ItemId}.", id);
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
            _logger.LogInformation("Attempting to create menu item '{ItemName}' for Category ID: {CategoryId}.", itemDto.Name, itemDto.MenuCategoryId);
            var (userOrganizationId, isSuperAdmin) = GetUserContext();

            // Verify the target Category exists and belongs to the user's organization (if not SuperAdmin)
            var targetCategory = await _context.MenuCategories
                                               .Include(mc => mc.Area) // Include Area for org check
                                               .FirstOrDefaultAsync(mc => mc.Id == itemDto.MenuCategoryId);

            if (targetCategory == null)
            {
                _logger.LogWarning("CreateItemAsync failed: Target Menu Category with ID {CategoryId} not found.", itemDto.MenuCategoryId);
                throw new KeyNotFoundException($"Menu Category with ID {itemDto.MenuCategoryId} not found.");
            }

            if (!isSuperAdmin && targetCategory.Area?.OrganizationId != userOrganizationId)
            {
                _logger.LogWarning("User {UserId} denied access to create item in category {CategoryId} belonging to another organization {OrganizationId}.", GetUserId(), itemDto.MenuCategoryId, targetCategory.Area?.OrganizationId);
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
            _logger.LogInformation("Menu item '{ItemName}' (ID: {ItemId}) created successfully for Category ID: {CategoryId}.", newItem.Name, newItem.Id, newItem.MenuCategoryId);

            // Map to DTO before returning
            return new MenuItemDto(newItem); // Use constructor for mapping
        }

        // Updated to accept MenuItemUpsertDto
        public async Task<bool> UpdateItemAsync(int id, MenuItemUpsertDto itemDto)
        {
            _logger.LogInformation("Attempting to update menu item ID: {ItemId}.", id);
            // ID mismatch check removed as DTO doesn't contain ID

            var (userOrganizationId, isSuperAdmin) = GetUserContext();

            // Fetch the existing item, including its Category -> Area for checks
            var existingItem = await _context.MenuItems
                                             .Include(mi => mi.MenuCategory)
                                                .ThenInclude(mc => mc.Area)
                                             .FirstOrDefaultAsync(mi => mi.Id == id);

            if (existingItem == null)
            {
                _logger.LogWarning("UpdateItemAsync failed: Menu item with ID {ItemId} not found.", id);
                throw new KeyNotFoundException($"Menu Item with ID {id} not found.");
            }

            // Verify user has access to the *existing* item's organization
            // Add explicit null checks for MenuCategory and Area
            if (!isSuperAdmin && (existingItem.MenuCategory == null || existingItem.MenuCategory.Area == null || existingItem.MenuCategory.Area.OrganizationId != userOrganizationId))
            {
                _logger.LogWarning("User {UserId} denied access to update menu item {ItemId} belonging to organization {OrganizationId}.", GetUserId(), id, existingItem.MenuCategory?.Area?.OrganizationId);
                // Treat inconsistent data or access denial the same way
                throw new UnauthorizedAccessException($"Access denied to update menu item with ID {id}.");
            }

            // If MenuCategoryId is being changed, verify access to the *new* category's organization
            if (existingItem.MenuCategoryId != itemDto.MenuCategoryId)
            {
                _logger.LogInformation("Menu item {ItemId} is being moved from Category {OldCategoryId} to Category {NewCategoryId}.", id, existingItem.MenuCategoryId, itemDto.MenuCategoryId);
                var newCategory = await _context.MenuCategories
                                                .Include(mc => mc.Area)
                                                .FirstOrDefaultAsync(mc => mc.Id == itemDto.MenuCategoryId);
                if (newCategory == null)
                {
                    _logger.LogWarning("UpdateItemAsync failed: Target Menu Category with ID {CategoryId} not found for item move.", itemDto.MenuCategoryId);
                    throw new KeyNotFoundException($"Target Menu Category with ID {itemDto.MenuCategoryId} not found.");
                }
                if (!isSuperAdmin && newCategory.Area?.OrganizationId != userOrganizationId)
                {
                    _logger.LogWarning("User {UserId} denied access to move item {ItemId} to category {NewCategoryId} belonging to another organization {NewOrgId}.", GetUserId(), id, itemDto.MenuCategoryId, newCategory.Area?.OrganizationId);
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
                _logger.LogInformation("Menu item {ItemId} updated successfully.", id);
                return true;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogError(ex, "DbUpdateConcurrencyException during UpdateItemAsync for item {ItemId}.", id);
                if (!await ItemExistsAsync(id))
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
                _logger.LogError(ex, "DbUpdateException during UpdateItemAsync for item {ItemId}.", id);
                return false;
            }
        }

        public async Task<bool> DeleteItemAsync(int id)
        {
            _logger.LogInformation("Attempting to delete menu item ID: {ItemId}.", id);
            var (userOrganizationId, isSuperAdmin) = GetUserContext();

            // Fetch the item including Category -> Area for organization check
            var item = await _context.MenuItems
                                     .Include(mi => mi.MenuCategory)
                                        .ThenInclude(mc => mc.Area)
                                     .FirstOrDefaultAsync(mi => mi.Id == id);

            if (item == null)
            {
                _logger.LogWarning("DeleteItemAsync failed: Menu item with ID {ItemId} not found.", id);
                return false; // Not found
            }

            // Verify user has access to the item's organization
            // Add explicit null checks for MenuCategory and Area
            if (!isSuperAdmin && (item.MenuCategory == null || item.MenuCategory.Area == null || item.MenuCategory.Area.OrganizationId != userOrganizationId))
            {
                _logger.LogWarning("User {UserId} denied access to delete menu item {ItemId} belonging to organization {OrganizationId}.", GetUserId(), id, item.MenuCategory?.Area?.OrganizationId);
                // Treat inconsistent data or access denial the same way
                throw new UnauthorizedAccessException($"Access denied to delete menu item with ID {id}.");
            }

            try
            {
                _context.MenuItems.Remove(item);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Menu item {ItemId} deleted successfully.", id);
                return true;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "DbUpdateException during DeleteItemAsync for item {ItemId}. It might be in use.", id);
                return false;
            }
        }

        // This internal check doesn't need tenancy context as it's only used after existence is confirmed
        // by other methods that *do* check tenancy.
        public async Task<bool> ItemExistsAsync(int id)
        {
            _logger.LogDebug("Checking if menu item {ItemId} exists.", id);
            // Note: If this were public or used differently, it *would* need tenancy checks.
            return await _context.MenuItems.AnyAsync(e => e.Id == id);
        }

        public async Task<ServiceResult> UpdateStockAsync(int menuItemId, int? newScorta) // Removed ClaimsPrincipal user
        {
            _logger.LogInformation("Attempting to update stock for menu item {MenuItemId} to {NewScorta}.", menuItemId, newScorta);
            var (userOrganizationId, isSuperAdmin) = GetUserContext(); 

            var menuItem = await _context.MenuItems
                                         .Include(mi => mi.MenuCategory)
                                         .ThenInclude(mc => mc.Area)
                                         .FirstOrDefaultAsync(mi => mi.Id == menuItemId);

            if (menuItem == null)
            {
                _logger.LogWarning("UpdateStockAsync failed: Menu Item with ID {MenuItemId} not found.", menuItemId);
                return ServiceResult.Fail($"Menu Item with ID {menuItemId} not found.");
            }

            if (menuItem.MenuCategory == null || menuItem.MenuCategory.Area == null)
            {
                _logger.LogError("UpdateStockAsync failed: Menu Item with ID {MenuItemId} has inconsistent category or area data.", menuItemId);
                // This case should ideally not happen with well-structured data.
                // Log this as a data integrity issue.
                // For now, treat as an error preventing stock update.
                return ServiceResult.Fail($"Menu Item with ID {menuItemId} has inconsistent category or area data.");
            }
            
            var itemOrganizationId = menuItem.MenuCategory.Area.OrganizationId;

            if (!isSuperAdmin && itemOrganizationId != userOrganizationId)
            {
                _logger.LogWarning("User {UserId} denied access to update stock for menu item {MenuItemId} belonging to organization {OrganizationId}.", GetUserId(), menuItemId, itemOrganizationId);
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
                
                _logger.LogInformation("Stock for menu item {MenuItemId} updated successfully to {NewScorta}.", menuItemId, newScorta);
                return ServiceResult.Ok(); // Corrected to use Ok() method
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogError(ex, "DbUpdateConcurrencyException during UpdateStockAsync for menu item {MenuItemId}.", menuItemId);
                if (!await ItemExistsAsync(menuItemId))
                {
                    return ServiceResult.Fail($"Menu Item with ID {menuItemId} not found during stock update.");
                }
                throw; // Re-throw if it's a genuine concurrency issue on an existing item
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while updating stock for menu item {MenuItemId}.", menuItemId);
                return ServiceResult.Fail($"An error occurred while updating stock for menu item {menuItemId}: {ex.Message}");
            }
        }

        public async Task<ServiceResult> ResetStockAsync(int menuItemId) // Removed ClaimsPrincipal user
        {
            _logger.LogInformation("Attempting to reset stock for menu item {MenuItemId}.", menuItemId);
            return await UpdateStockAsync(menuItemId, null); // Resetting is updating to null, removed user argument
        }

        public async Task<ServiceResult> ResetAllStockForAreaAsync(int areaId) // Removed ClaimsPrincipal user
        {
            _logger.LogInformation("Attempting to reset all stock for Area ID: {AreaId}.", areaId);
            var (userOrganizationId, isSuperAdmin) = GetUserContext(); 

            var area = await _context.Areas.FindAsync(areaId);
            if (area == null)
            {
                _logger.LogWarning("ResetAllStockForAreaAsync failed: Area with ID {AreaId} not found.", areaId);
                return ServiceResult.Fail($"Area with ID {areaId} not found.");
            }

            if (!isSuperAdmin && area.OrganizationId != userOrganizationId)
            {
                _logger.LogWarning("User {UserId} denied access to reset all stock for area {AreaId} belonging to organization {OrganizationId}.", GetUserId(), areaId, area.OrganizationId);
                return ServiceResult.Fail("Access denied to reset stock for this area.");
            }

            var menuItemsInArea = await _context.MenuItems
                                                .Include(mi => mi.MenuCategory)
                                                .Where(mi => mi.MenuCategory != null && mi.MenuCategory.AreaId == areaId)
                                                .ToListAsync();
            
            if (!menuItemsInArea.Any())
            {
                _logger.LogInformation("No menu items found in Area {AreaId} to reset stock for. Operation completed successfully.", areaId);
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
                
                _logger.LogInformation("All stock for Area {AreaId} reset successfully.", areaId);
                return ServiceResult.Ok(); // Corrected to use Ok() method
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while resetting all stock for area {AreaId}.", areaId);
                return ServiceResult.Fail($"An error occurred while resetting all stock for area {areaId}: {ex.Message}");
            }
        }
    }
}
