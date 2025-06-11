using Microsoft.AspNetCore.Authorization; // Now used
using Microsoft.AspNetCore.Mvc;
using SagraFacile.NET.API.DTOs; // Add DTO using
using SagraFacile.NET.API.Models;
using SagraFacile.NET.API.Services.Interfaces;
using System; // Add for Exception types
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SagraFacile.NET.API.Controllers
{
[Route("api/[controller]")]
[ApiController]
// Controller-level authorization removed; apply per-method as needed.
// TODO: Implement multi-tenancy checks based on user's access to the AreaId
public class MenuCategoriesController : ControllerBase
{
    private readonly IMenuCategoryService _menuCategoryService;
        // Inject IAreaService or user context later to verify area access

        public MenuCategoriesController(IMenuCategoryService menuCategoryService)
        {
            _menuCategoryService = menuCategoryService;
        }

        // GET: api/MenuCategories?areaId=123
        [HttpGet]
        [Authorize(Roles = "SuperAdmin, Admin, AreaAdmin, Cashier, Waiter")] // Allow Cashiers/Waiters to list categories for an area
        public async Task<ActionResult<IEnumerable<MenuCategoryDto>>> GetMenuCategories([FromQuery] int areaId) // DTO return type
        {
            // Basic: Get categories for a specific area.
            // Service layer now handles access check.
            if (areaId <= 0)
            {
                return BadRequest("Valid AreaId query parameter is required.");
            }
            try
            {
                var categories = await _menuCategoryService.GetCategoriesByAreaAsync(areaId);
                if (categories == null)
                {
                    // Service returns null if area not found (for this user)
                    return NotFound($"Area with ID {areaId} not found or access denied.");
                }
                return Ok(categories); // Returns DTOs now
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid(); // User doesn't have access to the specified area
            }
            catch
            {
                return StatusCode(500, "An error occurred while retrieving menu categories.");
            }
        }

        // GET: api/MenuCategories/5
        [HttpGet("{id}")]
        [Authorize(Roles = "SuperAdmin, Admin, AreaAdmin, Cashier, Waiter")] // Allow Cashiers/Waiters to get a specific category
        public async Task<ActionResult<MenuCategoryDto>> GetMenuCategory(int id) // DTO return type
        {
            // Basic: Get specific category.
            // Service layer now handles access check.
            var category = await _menuCategoryService.GetCategoryByIdAsync(id); // Returns DTO now

            if (category == null)
            {
                // Service returns null if not found OR user doesn't have access
                return NotFound($"MenuCategory with ID {id} not found or access denied.");
            }

            return Ok(category); // Returns DTO
        }

        // POST: api/MenuCategories
        [HttpPost]
        [Authorize(Roles = "SuperAdmin, Admin, AreaAdmin")] // Only Admins can create
        public async Task<ActionResult<MenuCategoryDto>> PostMenuCategory([FromBody] MenuCategory category) // DTO return type
        {
            // Basic: Create category.
            // Service layer now handles access check.
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var createdCategoryDto = await _menuCategoryService.CreateCategoryAsync(category); // Returns DTO now
                // Pass the DTO to CreatedAtAction, but the route needs the ID
                return CreatedAtAction(nameof(GetMenuCategory), new { id = createdCategoryDto.Id }, createdCategoryDto);
            }
            catch (KeyNotFoundException ex) // Target Area not found
            {
                return NotFound(ex.Message); // Return 404 Not Found if the Area doesn't exist
            }
            catch (UnauthorizedAccessException) // No need for ex variable if not used
            {
                return Forbid(); // Return 403 Forbidden without message as scheme
            }
            catch
            {
                return StatusCode(500, "An error occurred while creating the menu category.");
            }
        }

        // PUT: api/MenuCategories/5
        // Input should ideally be a DTO as well, but keeping MenuCategory for now
        [HttpPut("{id}")]
        [Authorize(Roles = "SuperAdmin, Admin, AreaAdmin")] // Only Admins can update
        public async Task<IActionResult> PutMenuCategory(int id, [FromBody] MenuCategory category)
        {
            // Basic: Update category.
            // Service layer now handles access checks.
            if (id != category.Id)
            {
                return BadRequest("ID mismatch.");
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var updateResult = await _menuCategoryService.UpdateCategoryAsync(id, category);

                if (!updateResult)
                {
                    if (!await _menuCategoryService.CategoryExistsAsync(id))
                    {
                        return NotFound($"MenuCategory with ID {id} not found.");
                    }
                    else
                    {
                        return StatusCode(500, "An error occurred while updating the menu category.");
                    }
                }
                return NoContent();
            }
            catch (KeyNotFoundException ex) // Category or Target Area not found
            {
                // Service throws specific messages
                return NotFound(ex.Message);
            }
            catch (UnauthorizedAccessException) // No need for ex variable if not used
            {
                // Service throws specific messages, but we just return 403
                return Forbid(); // Return 403 Forbidden without message as scheme
            }
            catch
            {
                return StatusCode(500, "An error occurred while updating the menu category.");
            }
        }

        // DELETE: api/MenuCategories/5
        [HttpDelete("{id}")]
        [Authorize(Roles = "SuperAdmin, Admin, AreaAdmin")] // Only Admins can delete
        public async Task<IActionResult> DeleteMenuCategory(int id)
        {
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
                        return NotFound($"MenuCategory with ID {id} not found.");
                    }
                    else
                    {
                        // Category exists but couldn't be deleted (likely FK constraint)
                        return BadRequest($"Could not delete MenuCategory with ID {id}. It might be referenced by other items.");
                    }
                }

                return NoContent();
            }
            catch (UnauthorizedAccessException) // No need for ex variable if not used
            {
                return Forbid(); // Return 403 Forbidden without message as scheme
            }
            catch
            {
                return StatusCode(500, "An error occurred while deleting the menu category.");
            }
        }
    }
}
