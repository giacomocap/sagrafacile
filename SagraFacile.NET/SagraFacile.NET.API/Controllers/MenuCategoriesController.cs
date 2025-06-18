using Microsoft.AspNetCore.Authorization; // Now used
using Microsoft.AspNetCore.Mvc;
using SagraFacile.NET.API.DTOs; // Add DTO using
using SagraFacile.NET.API.Models;
using SagraFacile.NET.API.Services.Interfaces;

namespace SagraFacile.NET.API.Controllers
{
[Route("api/[controller]")]
[ApiController]
// Controller-level authorization removed; apply per-method as needed.
// TODO: Implement multi-tenancy checks based on user's access to the AreaId
public class MenuCategoriesController : ControllerBase
{
    private readonly IMenuCategoryService _menuCategoryService;
    private readonly ILogger<MenuCategoriesController> _logger; // Added
        // Inject IAreaService or user context later to verify area access

        public MenuCategoriesController(IMenuCategoryService menuCategoryService, ILogger<MenuCategoriesController> logger) // Added ILogger
        {
            _menuCategoryService = menuCategoryService ?? throw new ArgumentNullException(nameof(menuCategoryService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger)); // Added
        }

        // GET: api/MenuCategories?areaId=123
        [HttpGet]
        [Authorize(Roles = "SuperAdmin, Admin, AreaAdmin, Cashier, Waiter")] // Allow Cashiers/Waiters to list categories for an area
        public async Task<ActionResult<IEnumerable<MenuCategoryDto>>> GetMenuCategories([FromQuery] int areaId) // DTO return type
        {
            _logger.LogInformation("Received request to get menu categories for AreaId: {AreaId}", areaId);
            // Basic: Get categories for a specific area.
            // Service layer now handles access check.
            if (areaId <= 0)
            {
                _logger.LogWarning("Bad request: Invalid AreaId {AreaId} provided for getting menu categories.", areaId);
                return BadRequest("Valid AreaId query parameter is required.");
            }
            try
            {
                var categories = await _menuCategoryService.GetCategoriesByAreaAsync(areaId);
                if (categories == null)
                {
                    // Service returns null if area not found (for this user)
                    _logger.LogInformation("Area with ID {AreaId} not found or access denied when getting menu categories.", areaId);
                    return NotFound($"Area with ID {areaId} not found or access denied.");
                }
                _logger.LogInformation("Successfully retrieved {Count} menu categories for AreaId: {AreaId}", ((List<MenuCategoryDto>)categories).Count, areaId);
                return Ok(categories); // Returns DTOs now
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized access attempt to get menu categories for AreaId: {AreaId}", areaId);
                return Forbid(); // User doesn't have access to the specified area
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while retrieving menu categories for AreaId: {AreaId}", areaId);
                return StatusCode(500, "An error occurred while retrieving menu categories.");
            }
        }

        // GET: api/MenuCategories/5
        [HttpGet("{id}")]
        [Authorize(Roles = "SuperAdmin, Admin, AreaAdmin, Cashier, Waiter")] // Allow Cashiers/Waiters to get a specific category
        public async Task<ActionResult<MenuCategoryDto>> GetMenuCategory(int id) // DTO return type
        {
            _logger.LogInformation("Received request to get menu category with ID: {MenuCategoryId}", id);
            // Basic: Get specific category.
            // Service layer now handles access check.
            try
            {
                var category = await _menuCategoryService.GetCategoryByIdAsync(id); // Returns DTO now

                if (category == null)
                {
                    // Service returns null if not found OR user doesn't have access
                    _logger.LogWarning("MenuCategory with ID {MenuCategoryId} not found or access denied.", id);
                    return NotFound($"MenuCategory with ID {id} not found or access denied.");
                }

                _logger.LogInformation("Successfully retrieved menu category with ID: {MenuCategoryId}", id);
                return Ok(category); // Returns DTO
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized access attempt to get menu category with ID: {MenuCategoryId}", id);
                return Forbid(); // User doesn't have access to the specified area
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while retrieving menu category with ID: {MenuCategoryId}", id);
                return StatusCode(500, "An error occurred while retrieving the menu category.");
            }
        }

        // POST: api/MenuCategories
        [HttpPost]
        [Authorize(Roles = "SuperAdmin, Admin, AreaAdmin")] // Only Admins can create
        public async Task<ActionResult<MenuCategoryDto>> PostMenuCategory([FromBody] MenuCategory category) // DTO return type
        {
            _logger.LogInformation("Received request to create menu category '{MenuCategoryName}' for AreaId: {AreaId}", category.Name, category.AreaId);
            // Basic: Create category.
            // Service layer now handles access check.
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid model state for creating menu category '{MenuCategoryName}'. Errors: {@Errors}", category.Name, ModelState);
                return BadRequest(ModelState);
            }

            try
            {
                var createdCategoryDto = await _menuCategoryService.CreateCategoryAsync(category); // Returns DTO now
                _logger.LogInformation("Successfully created menu category with ID: {MenuCategoryId}, Name: '{MenuCategoryName}' for AreaId: {AreaId}", createdCategoryDto.Id, createdCategoryDto.Name, createdCategoryDto.AreaId);
                // Pass the DTO to CreatedAtAction, but the route needs the ID
                return CreatedAtAction(nameof(GetMenuCategory), new { id = createdCategoryDto.Id }, createdCategoryDto);
            }
            catch (KeyNotFoundException ex) // Target Area not found
            {
                _logger.LogWarning(ex, "Failed to create menu category '{MenuCategoryName}'. Area with ID {AreaId} not found.", category.Name, category.AreaId);
                return NotFound(ex.Message); // Return 404 Not Found if the Area doesn't exist
            }
            catch (UnauthorizedAccessException ex) // No need for ex variable if not used
            {
                _logger.LogWarning(ex, "Unauthorized access attempt to create menu category '{MenuCategoryName}' for AreaId: {AreaId}", category.Name, category.AreaId);
                return Forbid(); // Return 403 Forbidden without message as scheme
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while creating menu category '{MenuCategoryName}' for AreaId: {AreaId}", category.Name, category.AreaId);
                return StatusCode(500, "An error occurred while creating the menu category.");
            }
        }

        // PUT: api/MenuCategories/5
        // Input should ideally be a DTO as well, but keeping MenuCategory for now
        [HttpPut("{id}")]
        [Authorize(Roles = "SuperAdmin, Admin, AreaAdmin")] // Only Admins can update
        public async Task<IActionResult> PutMenuCategory(int id, [FromBody] MenuCategory category)
        {
            _logger.LogInformation("Received request to update menu category with ID: {MenuCategoryId}, Name: '{MenuCategoryName}'", id, category.Name);
            // Basic: Update category.
            // Service layer now handles access checks.
            if (id != category.Id)
            {
                _logger.LogWarning("Bad request: ID mismatch for updating menu category. Route ID: {RouteId}, Body ID: {BodyId}", id, category.Id);
                return BadRequest("ID mismatch.");
            }

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid model state for updating menu category with ID: {MenuCategoryId}. Errors: {@Errors}", id, ModelState);
                return BadRequest(ModelState);
            }

            try
            {
                var updateResult = await _menuCategoryService.UpdateCategoryAsync(id, category);

                if (!updateResult)
                {
                    if (!await _menuCategoryService.CategoryExistsAsync(id))
                    {
                        _logger.LogWarning("MenuCategory with ID {MenuCategoryId} not found during update.", id);
                        return NotFound($"MenuCategory with ID {id} not found.");
                    }
                    else
                    {
                        _logger.LogError("An unknown error occurred while updating menu category with ID: {MenuCategoryId}. Update result was false but category exists.", id);
                        return StatusCode(500, "An error occurred while updating the menu category.");
                    }
                }
                _logger.LogInformation("Successfully updated menu category with ID: {MenuCategoryId}", id);
                return NoContent();
            }
            catch (KeyNotFoundException ex) // Category or Target Area not found
            {
                _logger.LogWarning(ex, "Failed to update menu category with ID: {MenuCategoryId}. Error: {Error}", id, ex.Message);
                // Service throws specific messages
                return NotFound(ex.Message);
            }
            catch (UnauthorizedAccessException ex) // No need for ex variable if not used
            {
                _logger.LogWarning(ex, "Unauthorized access attempt to update menu category with ID: {MenuCategoryId}", id);
                // Service throws specific messages, but we just return 403
                return Forbid(); // Return 403 Forbidden without message as scheme
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while updating menu category with ID: {MenuCategoryId}", id);
                return StatusCode(500, "An error occurred while updating the menu category.");
            }
        }

        // DELETE: api/MenuCategories/5
        [HttpDelete("{id}")]
        [Authorize(Roles = "SuperAdmin, Admin, AreaAdmin")] // Only Admins can delete
        public async Task<IActionResult> DeleteMenuCategory(int id)
        {
            _logger.LogInformation("Received request to delete menu category with ID: {MenuCategoryId}", id);
            // Basic: Delete category.
            // Service layer now handles access checks.

            try
            {
                var deleteResult = await _menuCategoryService.DeleteCategoryAsync(id);

                if (!deleteResult)
                {
                    // If deleteResult is false, it could be not found or a DbUpdateException (like FK constraint)
                    if (!await _menuCategoryService.CategoryExistsAsync(id)) // Check if it actually exists first
                {
                        _logger.LogWarning("MenuCategory with ID {MenuCategoryId} not found during deletion.", id);
                        return NotFound($"MenuCategory with ID {id} not found.");
                    }
                    else
                    {
                        // Category exists but couldn't be deleted (likely FK constraint)
                        _logger.LogWarning("Could not delete MenuCategory with ID {MenuCategoryId}. It might be referenced by other items.", id);
                        return BadRequest($"Could not delete MenuCategory with ID {id}. It might be referenced by other items.");
                    }
                }

                _logger.LogInformation("Successfully deleted menu category with ID: {MenuCategoryId}", id);
                return NoContent();
            }
            catch (UnauthorizedAccessException ex) // No need for ex variable if not used
            {
                _logger.LogWarning(ex, "Unauthorized access attempt to delete menu category with ID: {MenuCategoryId}", id);
                return Forbid(); // Return 403 Forbidden without message as scheme
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while deleting menu category with ID: {MenuCategoryId}", id);
                return StatusCode(500, "An error occurred while deleting the menu category.");
            }
        }
    }
}
