using Microsoft.AspNetCore.Authorization; // Now used
using Microsoft.AspNetCore.Mvc;
using SagraFacile.NET.API.Services.Interfaces;
using SagraFacile.NET.API.DTOs; // Added DTO namespace

namespace SagraFacile.NET.API.Controllers
{
[Route("api/[controller]")]
[ApiController]
// Controller-level authorization removed; apply per-method as needed.
// TODO: Implement multi-tenancy checks based on user's access to the CategoryId/AreaId
public class MenuItemsController : ControllerBase
{
    private readonly IMenuItemService _menuItemService;
    private readonly ILogger<MenuItemsController> _logger; // Added
        // Inject IMenuCategoryService or user context later to verify category access

        public MenuItemsController(IMenuItemService menuItemService, ILogger<MenuItemsController> logger) // Added ILogger
        {
            _menuItemService = menuItemService ?? throw new ArgumentNullException(nameof(menuItemService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger)); // Added
        }

        // GET: api/MenuItems?categoryId=456
        [HttpGet]
        [Authorize(Roles = "SuperAdmin, Admin, AreaAdmin, Cashier, Waiter")] // Allow Cashiers/Waiters to list items for a category
        public async Task<ActionResult<IEnumerable<MenuItemDto>>> GetMenuItems([FromQuery] int categoryId) // Return DTO list
        {
            _logger.LogInformation("Received request to get menu items for CategoryId: {CategoryId}", categoryId);
             if (categoryId <= 0)
            {
                _logger.LogWarning("Bad request: Invalid CategoryId {CategoryId} provided for getting menu items.", categoryId);
                return BadRequest("Valid CategoryId query parameter is required.");
            }
            try
            {
                var items = await _menuItemService.GetItemsByCategoryAsync(categoryId);
                if (items == null)
                {
                    // Service returns null if category not found or not accessible
                    _logger.LogInformation("MenuCategory with ID {CategoryId} not found or access denied when getting menu items.", categoryId);
                    return NotFound($"MenuCategory with ID {categoryId} not found or access denied.");
                }
                // Service already returns DTOs
                _logger.LogInformation("Successfully retrieved {Count} menu items for CategoryId: {CategoryId}", ((List<MenuItemDto>)items).Count, categoryId);
                return Ok(items);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized access attempt to get menu items for CategoryId: {CategoryId}", categoryId);
                return Forbid(); // User cannot access this category
            }
            catch (KeyNotFoundException ex) // Category itself not found
            {
                _logger.LogWarning(ex, "MenuCategory with ID {CategoryId} not found or not accessible when getting menu items.", categoryId);
                // Depending on requirements, could be NotFound() or Forbidden() if they shouldn't know it exists
                return NotFound($"MenuCategory with ID {categoryId} not found or not accessible.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while retrieving menu items for CategoryId: {CategoryId}", categoryId);
                return StatusCode(500, "An error occurred while retrieving menu items.");
            }
        }

        // GET: api/MenuItems/5
        [HttpGet("{id}")]
        [Authorize(Roles = "SuperAdmin, Admin, AreaAdmin, Cashier, Waiter")] // Allow Cashiers/Waiters to get a specific item
        public async Task<ActionResult<MenuItemDto>> GetMenuItem(int id) // Return DTO
        {
            _logger.LogInformation("Received request to get menu item with ID: {MenuItemId}", id);
            try
            {
                var item = await _menuItemService.GetItemByIdAsync(id);

                if (item == null)
                {
                    _logger.LogWarning("MenuItem with ID {MenuItemId} not found or not accessible.", id);
                    return NotFound($"MenuItem with ID {id} not found or not accessible.");
                }

                // Service already returns DTO
                _logger.LogInformation("Successfully retrieved menu item with ID: {MenuItemId}", id);
                return Ok(item);
            }
            // KeyNotFoundException is not thrown by GetItemByIdAsync in the service for not found cases
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized access attempt to get menu item with ID: {MenuItemId}", id);
                // This could still be thrown by GetUserContext
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while retrieving menu item with ID: {MenuItemId}", id);
                return StatusCode(500, "An error occurred while retrieving the menu item.");
            }
        }

        // POST: api/MenuItems
        // Input changed to MenuItemUpsertDto
        [HttpPost]
        [Authorize(Roles = "SuperAdmin, Admin, AreaAdmin")] // Only Admins can create
        public async Task<ActionResult<MenuItemDto>> PostMenuItem([FromBody] MenuItemUpsertDto itemDto) // Accept DTO
        {
            _logger.LogInformation("Received request to create menu item '{MenuItemName}' for CategoryId: {CategoryId}", itemDto.Name, itemDto.MenuCategoryId);
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid model state for creating menu item '{MenuItemName}'. Errors: {@Errors}", itemDto.Name, ModelState);
                return BadRequest(ModelState);
            }

            try
            {
                var createdItemDto = await _menuItemService.CreateItemAsync(itemDto); // Service accepts DTO
                _logger.LogInformation("Successfully created menu item with ID: {MenuItemId}, Name: '{MenuItemName}' for CategoryId: {CategoryId}", createdItemDto.Id, createdItemDto.Name, createdItemDto.MenuCategoryId);
                return CreatedAtAction(nameof(GetMenuItem), new { id = createdItemDto.Id }, createdItemDto);
            }
            catch (KeyNotFoundException ex) // Category not found or inaccessible by ID check in service
            {
                _logger.LogWarning(ex, "Failed to create menu item '{MenuItemName}'. Category with ID {CategoryId} not found or inaccessible.", itemDto.Name, itemDto.MenuCategoryId);
                // Return NotFound as the referenced category resource was not found/accessible
                return NotFound(ex.Message);
            }
            catch (UnauthorizedAccessException ex) // User cannot create items in this category/org
            {
                _logger.LogWarning(ex, "Unauthorized access attempt to create menu item '{MenuItemName}' for CategoryId: {CategoryId}", itemDto.Name, itemDto.MenuCategoryId);
                return Forbid();
            }
            catch (Exception ex)
            {
                 _logger.LogError(ex, "An error occurred while creating menu item '{MenuItemName}' for CategoryId: {CategoryId}", itemDto.Name, itemDto.MenuCategoryId);
                 return StatusCode(500, "An error occurred while creating the menu item.");
            }
        }

        // PUT: api/MenuItems/5
        // Input is now MenuItemUpsertDto
        [HttpPut("{id}")]
        [Authorize(Roles = "SuperAdmin, Admin, AreaAdmin")] // Only Admins can update
        public async Task<IActionResult> PutMenuItem(int id, [FromBody] MenuItemUpsertDto itemDto) // Accept DTO
        {
            _logger.LogInformation("Received request to update menu item with ID: {MenuItemId}, Name: '{MenuItemName}'", id, itemDto.Name);
            // Basic: Update item.
            // Later: Verify user has access to itemDto.MenuCategoryId.
            // ID check removed as DTO doesn't contain ID. ID from URL is used.

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid model state for updating menu item with ID: {MenuItemId}. Errors: {@Errors}", id, ModelState);
                return BadRequest(ModelState);
            }

            // TODO: Consider mapping DTO to MenuItem entity here if the service requires it,
            // or update the service to accept the DTO directly.
            // Service now accepts the DTO directly.

            try
            {
                // Pass the DTO directly to the service
                await _menuItemService.UpdateItemAsync(id, itemDto);
                _logger.LogInformation("Successfully updated menu item with ID: {MenuItemId}", id);
                return NoContent();
            }
            catch (KeyNotFoundException ex) // Item or target Category not found/accessible
            {
                _logger.LogWarning(ex, "Failed to update menu item with ID: {MenuItemId}. Error: {Error}", id, ex.Message);
                // Distinguish between item not found and category not found/accessible if needed
                // For now, treat both as potentially bad input or not found
                 if (ex.Message.Contains("category", StringComparison.OrdinalIgnoreCase))
                 {
                     return BadRequest(ex.Message); // Target category invalid/inaccessible
                 }
                 return NotFound(ex.Message); // Item itself not found
            }
            catch (UnauthorizedAccessException ex) // User cannot modify this item or move to target category
            {
                _logger.LogWarning(ex, "Unauthorized access attempt to update menu item with ID: {MenuItemId}", id);
                return Forbid();
            }
            catch (Exception ex)
            {
                 _logger.LogError(ex, "An error occurred while updating menu item with ID: {MenuItemId}", id);
                 return StatusCode(500, "An error occurred while updating the menu item.");
            }
        }

        // DELETE: api/MenuItems/5
        [HttpDelete("{id}")]
        [Authorize(Roles = "SuperAdmin, Admin, AreaAdmin")] // Only Admins can delete
        public async Task<IActionResult> DeleteMenuItem(int id)
        {
            _logger.LogInformation("Received request to delete menu item with ID: {MenuItemId}", id);
            // Basic: Delete item.
            // Later: Verify user has access to the item's category.

            try
            {
                var success = await _menuItemService.DeleteItemAsync(id);
                if (!success)
                {
                    _logger.LogWarning("MenuItem with ID {MenuItemId} not found or could not be deleted.", id);
                    // Item not found or delete failed (e.g., FK constraint)
                    return NotFound($"MenuItem with ID {id} not found or could not be deleted.");
                }
                _logger.LogInformation("Successfully deleted menu item with ID: {MenuItemId}", id);
                return NoContent();
            }
            // KeyNotFoundException is not thrown by DeleteItemAsync
            catch (UnauthorizedAccessException ex) // User cannot delete this item
            {
                _logger.LogWarning(ex, "Unauthorized access attempt to delete menu item with ID: {MenuItemId}", id);
                return Forbid();
            }
            catch (InvalidOperationException ex) // Handle potential FK constraint issues if service throws it
            {
                _logger.LogWarning(ex, "Failed to delete menu item with ID: {MenuItemId} due to invalid operation (e.g., FK constraint).", id);
                 return Conflict(ex.Message); // e.g., "Cannot delete item as it is part of an order."
            }
            catch (Exception ex)
            {
                 _logger.LogError(ex, "An error occurred while deleting menu item with ID: {MenuItemId}", id);
                 return StatusCode(500, "An error occurred while deleting the menu item.");
            }
        }

        // STOCK MANAGEMENT ENDPOINTS

        // PUT: api/menuitems/{menuItemId}/stock
        [HttpPut("{menuItemId}/stock")]
        [Authorize(Roles = "SuperAdmin, Admin, AreaAdmin")]
        public async Task<IActionResult> UpdateStock(int menuItemId, [FromBody] UpdateStockRequestDto requestDto)
        {
            _logger.LogInformation("Received request to update stock for MenuItemId: {MenuItemId} to NewScorta: {NewScorta}", menuItemId, requestDto.NewScorta);
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid model state for updating stock for MenuItemId: {MenuItemId}. Errors: {@Errors}", menuItemId, ModelState);
                return BadRequest(ModelState);
            }

            try
            {
                var result = await _menuItemService.UpdateStockAsync(menuItemId, requestDto.NewScorta);
                if (result.IsFailure)
                {
                    _logger.LogWarning("Failed to update stock for MenuItemId: {MenuItemId}. Errors: {@Errors}", menuItemId, result.Errors);
                    // Consider specific error handling based on result.Errors if needed
                    return BadRequest(result.Errors);
                }
                _logger.LogInformation("Successfully updated stock for MenuItemId: {MenuItemId}", menuItemId);
                return NoContent();
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized access attempt to update stock for MenuItemId: {MenuItemId}", menuItemId);
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while updating stock for MenuItemId: {MenuItemId}", menuItemId);
                return StatusCode(500, "An error occurred while updating stock.");
            }
        }

        // POST: api/menuitems/{menuItemId}/stock/reset
        [HttpPost("{menuItemId}/stock/reset")]
        [Authorize(Roles = "SuperAdmin, Admin, AreaAdmin")]
        public async Task<IActionResult> ResetStock(int menuItemId)
        {
            _logger.LogInformation("Received request to reset stock for MenuItemId: {MenuItemId}", menuItemId);
            try
            {
                var result = await _menuItemService.ResetStockAsync(menuItemId);
                if (result.IsFailure)
                {
                    _logger.LogWarning("Failed to reset stock for MenuItemId: {MenuItemId}. Errors: {@Errors}", menuItemId, result.Errors);
                    return BadRequest(result.Errors);
                }
                _logger.LogInformation("Successfully reset stock for MenuItemId: {MenuItemId}", menuItemId);
                return Ok(); // Or NoContent() if preferred for reset actions
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized access attempt to reset stock for MenuItemId: {MenuItemId}", menuItemId);
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while resetting stock for MenuItemId: {MenuItemId}", menuItemId);
                return StatusCode(500, "An error occurred while resetting stock.");
            }
        }

        // POST: api/areas/{areaId}/stock/reset-all
        // Note: This endpoint is placed here as per StockArchitecture.md,
        // but could be considered for AreasController in a future refactor.
        [HttpPost("/api/areas/{areaId}/stock/reset-all")] // Full path override to avoid menuitems prefix
        [Authorize(Roles = "SuperAdmin, Admin, AreaAdmin")]
        public async Task<IActionResult> ResetAllStockForArea(int areaId)
        {
            _logger.LogInformation("Received request to reset all stock for AreaId: {AreaId}", areaId);
            try
            {
                var result = await _menuItemService.ResetAllStockForAreaAsync(areaId);
                if (result.IsFailure)
                {
                    _logger.LogWarning("Failed to reset all stock for AreaId: {AreaId}. Errors: {@Errors}", areaId, result.Errors);
                    return BadRequest(result.Errors);
                }
                _logger.LogInformation("Successfully reset all stock for AreaId: {AreaId}", areaId);
                return Ok(); // Or NoContent()
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized access attempt to reset all stock for AreaId: {AreaId}", areaId);
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while resetting all stock for AreaId: {AreaId}", areaId);
                return StatusCode(500, "An error occurred while resetting all stock.");
            }
        }
    }

    // Helper DTO for the UpdateStock endpoint body
    public class UpdateStockRequestDto
    {
        public int? NewScorta { get; set; }
    }
}
