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
        // Inject IMenuCategoryService or user context later to verify category access

        public MenuItemsController(IMenuItemService menuItemService)
        {
            _menuItemService = menuItemService;
        }

        // GET: api/MenuItems?categoryId=456
        [HttpGet]
        [Authorize(Roles = "SuperAdmin, Admin, AreaAdmin, Cashier, Waiter")] // Allow Cashiers/Waiters to list items for a category
        public async Task<ActionResult<IEnumerable<MenuItemDto>>> GetMenuItems([FromQuery] int categoryId) // Return DTO list
        {
             if (categoryId <= 0)
            {
                return BadRequest("Valid CategoryId query parameter is required.");
            }
            try
            {
                var items = await _menuItemService.GetItemsByCategoryAsync(categoryId);
                if (items == null)
                {
                    // Service returns null if category not found or not accessible
                    return NotFound($"MenuCategory with ID {categoryId} not found or access denied.");
                }
                // Service already returns DTOs
                return Ok(items);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid(); // User cannot access this category
            }
            catch (KeyNotFoundException) // Category itself not found
            {
                // Depending on requirements, could be NotFound() or Forbidden() if they shouldn't know it exists
                return NotFound($"MenuCategory with ID {categoryId} not found or not accessible.");
            }
            catch
            {
                return StatusCode(500, "An error occurred while retrieving menu items.");
            }
        }

        // GET: api/MenuItems/5
        [HttpGet("{id}")]
        [Authorize(Roles = "SuperAdmin, Admin, AreaAdmin, Cashier, Waiter")] // Allow Cashiers/Waiters to get a specific item
        public async Task<ActionResult<MenuItemDto>> GetMenuItem(int id) // Return DTO
        {
            try
            {
                var item = await _menuItemService.GetItemByIdAsync(id);

                if (item == null)
                {
                    return NotFound($"MenuItem with ID {id} not found or not accessible.");
                }

                // Service already returns DTO
                return Ok(item);
            }
            // KeyNotFoundException is not thrown by GetItemByIdAsync in the service for not found cases
            catch (UnauthorizedAccessException)
            {
                // This could still be thrown by GetUserContext
                return Forbid();
            }
            catch
            {
                return StatusCode(500, "An error occurred while retrieving the menu item.");
            }
        }

        // POST: api/MenuItems
        // Input changed to MenuItemUpsertDto
        [HttpPost]
        [Authorize(Roles = "SuperAdmin, Admin, AreaAdmin")] // Only Admins can create
        public async Task<ActionResult<MenuItemDto>> PostMenuItem([FromBody] MenuItemUpsertDto itemDto) // Accept DTO
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var createdItemDto = await _menuItemService.CreateItemAsync(itemDto); // Service accepts DTO
                return CreatedAtAction(nameof(GetMenuItem), new { id = createdItemDto.Id }, createdItemDto);
            }
            catch (KeyNotFoundException ex) // Category not found or inaccessible by ID check in service
            {
                // Return NotFound as the referenced category resource was not found/accessible
                return NotFound(ex.Message);
            }
            catch (UnauthorizedAccessException) // User cannot create items in this category/org
            {
                return Forbid();
            }
            catch
            {
                 return StatusCode(500, "An error occurred while creating the menu item.");
            }
        }

        // PUT: api/MenuItems/5
        // Input is now MenuItemUpsertDto
        [HttpPut("{id}")]
        [Authorize(Roles = "SuperAdmin, Admin, AreaAdmin")] // Only Admins can update
        public async Task<IActionResult> PutMenuItem(int id, [FromBody] MenuItemUpsertDto itemDto) // Accept DTO
        {
            // Basic: Update item.
            // Later: Verify user has access to itemDto.MenuCategoryId.
            // ID check removed as DTO doesn't contain ID. ID from URL is used.

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // TODO: Consider mapping DTO to MenuItem entity here if the service requires it,
            // or update the service to accept the DTO directly.
            // Service now accepts the DTO directly.

            try
            {
                // Pass the DTO directly to the service
                await _menuItemService.UpdateItemAsync(id, itemDto);
                return NoContent();
            }
            catch (KeyNotFoundException ex) // Item or target Category not found/accessible
            {
                // Distinguish between item not found and category not found/accessible if needed
                // For now, treat both as potentially bad input or not found
                 if (ex.Message.Contains("category", StringComparison.OrdinalIgnoreCase))
                 {
                     return BadRequest(ex.Message); // Target category invalid/inaccessible
                 }
                 return NotFound(ex.Message); // Item itself not found
            }
            catch (UnauthorizedAccessException) // User cannot modify this item or move to target category
            {
                return Forbid();
            }
            catch
            {
                 return StatusCode(500, "An error occurred while updating the menu item.");
            }
        }

        // DELETE: api/MenuItems/5
        [HttpDelete("{id}")]
        [Authorize(Roles = "SuperAdmin, Admin, AreaAdmin")] // Only Admins can delete
        public async Task<IActionResult> DeleteMenuItem(int id)
        {
            // Basic: Delete item.
            // Later: Verify user has access to the item's category.

            try
            {
                var success = await _menuItemService.DeleteItemAsync(id);
                if (!success)
                {
                    // Item not found or delete failed (e.g., FK constraint)
                    return NotFound($"MenuItem with ID {id} not found or could not be deleted.");
                }
                return NoContent();
            }
            // KeyNotFoundException is not thrown by DeleteItemAsync
            catch (UnauthorizedAccessException) // User cannot delete this item
            {
                return Forbid();
            }
            catch (InvalidOperationException ex) // Handle potential FK constraint issues if service throws it
            {
                 return Conflict(ex.Message); // e.g., "Cannot delete item as it is part of an order."
            }
            catch
            {
                 return StatusCode(500, "An error occurred while deleting the menu item.");
            }
        }

        // STOCK MANAGEMENT ENDPOINTS

        // PUT: api/menuitems/{menuItemId}/stock
        [HttpPut("{menuItemId}/stock")]
        [Authorize(Roles = "SuperAdmin, Admin, AreaAdmin")]
        public async Task<IActionResult> UpdateStock(int menuItemId, [FromBody] UpdateStockRequestDto requestDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = await _menuItemService.UpdateStockAsync(menuItemId, requestDto.NewScorta);
            if (result.IsFailure)
            {
                // Consider specific error handling based on result.Errors if needed
                return BadRequest(result.Errors);
            }
            return NoContent();
        }

        // POST: api/menuitems/{menuItemId}/stock/reset
        [HttpPost("{menuItemId}/stock/reset")]
        [Authorize(Roles = "SuperAdmin, Admin, AreaAdmin")]
        public async Task<IActionResult> ResetStock(int menuItemId)
        {
            var result = await _menuItemService.ResetStockAsync(menuItemId);
            if (result.IsFailure)
            {
                return BadRequest(result.Errors);
            }
            return Ok(); // Or NoContent() if preferred for reset actions
        }

        // POST: api/areas/{areaId}/stock/reset-all
        // Note: This endpoint is placed here as per StockArchitecture.md,
        // but could be considered for AreasController in a future refactor.
        [HttpPost("/api/areas/{areaId}/stock/reset-all")] // Full path override to avoid menuitems prefix
        [Authorize(Roles = "SuperAdmin, Admin, AreaAdmin")]
        public async Task<IActionResult> ResetAllStockForArea(int areaId)
        {
            var result = await _menuItemService.ResetAllStockForAreaAsync(areaId);
            if (result.IsFailure)
            {
                return BadRequest(result.Errors);
            }
            return Ok(); // Or NoContent()
        }
    }

    // Helper DTO for the UpdateStock endpoint body
    public class UpdateStockRequestDto
    {
        public int? NewScorta { get; set; }
    }
}
