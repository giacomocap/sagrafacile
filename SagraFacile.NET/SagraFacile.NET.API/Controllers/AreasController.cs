using Microsoft.AspNetCore.Authorization; // Now used
using Microsoft.AspNetCore.Mvc;
using SagraFacile.NET.API.DTOs; // Ensure this includes AreaUpsertDto
using SagraFacile.NET.API.Models;
using SagraFacile.NET.API.Services.Interfaces;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SagraFacile.NET.API.Controllers
{
[Route("api/[controller]")]
[ApiController]
// Controller-level authorization removed; apply per-method as needed.
// TODO: Implement multi-tenancy checks based on user's OrganizationId claim
public class AreasController : ControllerBase
{
    private readonly IAreaService _areaService;
        // We will inject IUserService or similar later to get OrganizationId from user claims

        public AreasController(IAreaService areaService)
        {
            _areaService = areaService;
        }

        // GET: api/Areas
        // Organization filtering is now handled by the service based on user context
        [HttpGet]
        [Authorize(Roles = "SuperAdmin, Admin, Cashier, Waiter")] // Allow Cashiers/Waiters to list areas for their org
        [ProducesResponseType(typeof(IEnumerable<AreaDto>), StatusCodes.Status200OK)] // Update response type
        public async Task<ActionResult<IEnumerable<AreaDto>>> GetAreas() // Return AreaDto
        {
            // Service layer now handles filtering based on user's organization claim
            var areas = await _areaService.GetAllAreasAsync(); // Service returns DTOs
            return Ok(areas);
        }

        // GET: api/Areas/5
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(AreaDto), StatusCodes.Status200OK)] // Update response type
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<AreaDto>> GetArea(int id) // Return AreaDto
        {
            // Basic implementation - Later, verify user has access to this area's organization
            var areaDto = await _areaService.GetAreaByIdAsync(id); // Service returns DTO

            if (areaDto == null)
            {
                return NotFound($"Area with ID {id} not found.");
            }

            return Ok(areaDto);
        }

        // POST: api/Areas
        // Consider DTO
        [HttpPost]
        [Authorize(Roles = "Admin, SuperAdmin")] // Only Admins can create
        public async Task<ActionResult<Area>> PostArea([FromBody] Area area)
        {
             // Basic implementation - Later, verify user is Admin for area.OrganizationId
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var createdArea = await _areaService.CreateAreaAsync(area);
                return CreatedAtAction(nameof(GetArea), new { id = createdArea.Id }, createdArea);
            }
            catch (KeyNotFoundException ex) // Catch specific exception from service
            {
                // If OrganizationId provided doesn't exist
                return BadRequest(ex.Message);
            }
            catch
            {
                // General error
                 return StatusCode(500, "An error occurred while creating the area.");
            }
        }

        // PUT: api/Areas/5
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin, SuperAdmin")] // Only Admins can update
        // Accept AreaUpsertDto instead of the full Area model
        public async Task<IActionResult> PutArea(int id, [FromBody] AreaUpsertDto areaDto)
        {
            // Basic implementation - Later, verify user is Admin for the organization
            // ID mismatch check removed as DTO doesn't contain Id

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                // Pass the DTO to the service layer
                var updateResult = await _areaService.UpdateAreaAsync(id, areaDto);

                if (!updateResult)
                {
                    if (!await _areaService.AreaExistsAsync(id))
                    {
                        return NotFound($"Area with ID {id} not found.");
                    }
                    else
                    {
                        return StatusCode(500, "An error occurred while updating the area.");
                    }
                }
                 return NoContent();
            }
            catch (KeyNotFoundException ex) // Catch specific exception from service
            {
                 // If OrganizationId provided doesn't exist
                return BadRequest(ex.Message);
            }
             catch
            {
                // General error
                 return StatusCode(500, "An error occurred while updating the area.");
            }
        }

        // DELETE: api/Areas/5
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin, SuperAdmin")] // Only Admins can delete
        public async Task<IActionResult> DeleteArea(int id)
        {
            try
            {
                // Basic implementation - Later, verify user is Admin for the area's organization
                var deleteResult = await _areaService.DeleteAreaAsync(id);

                if (!deleteResult)
                {
                    if (!await _areaService.AreaExistsAsync(id))
                    {
                        return NotFound($"Area with ID {id} not found.");
                    }
                    else
                    {
                        // Could be due to constraints (e.g., Orders exist)
                        return StatusCode(500, "An error occurred while deleting the area. It might be in use.");
                    }
                }

                return NoContent();
            }
            catch (UnauthorizedAccessException) // Catch specific exception for authorization issues
            {
                return Forbid(); // Return 403 Forbidden
            }
            catch (KeyNotFoundException ex) // Catch specific exception from service (e.g., if AreaExists check failed unexpectedly)
            {
                return NotFound(ex.Message); // Or BadRequest depending on context
            }
            catch // General error
            {
                if (!await _areaService.AreaExistsAsync(id))
                {
                    return NotFound($"Area with ID {id} not found.");
                }
                else
                {
                    // Could be due to constraints (e.g., Orders exist)
                    return StatusCode(500, "An error occurred while deleting the area. It might be in use.");
                }
            }

            // This line was unreachable because all paths above return.
            // return NoContent();
        }
    }
}
