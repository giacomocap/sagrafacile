using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SagraFacile.NET.API.DTOs;
using SagraFacile.NET.API.Models;
using SagraFacile.NET.API.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SagraFacile.NET.API.Controllers
{
    [Route("api/organizations/{organizationId}/areas/{areaId}/kds-stations")]
    [ApiController]
    [Authorize] // Requires authentication for all actions
    public class KdsStationsController : ControllerBase
    {
        private readonly IKdsStationService _kdsStationService;
        private readonly ILogger<KdsStationsController> _logger;

        public KdsStationsController(IKdsStationService kdsStationService, ILogger<KdsStationsController> logger)
        {
            _kdsStationService = kdsStationService;
            _logger = logger;
        }

        // GET: api/organizations/{organizationId}/areas/{areaId}/kds-stations
        [HttpGet]
        [Authorize(Roles = "SuperAdmin,Admin,AreaAdmin")] // Define roles allowed to list stations
        public async Task<ActionResult<IEnumerable<KdsStationDto>>> GetKdsStations(Guid organizationId, int areaId)
        {
            _logger.LogInformation("Received request to get KDS stations for OrganizationId: {OrganizationId}, AreaId: {AreaId}", organizationId, areaId);
            try
            {
                var stations = await _kdsStationService.GetKdsStationsByAreaAsync(organizationId, areaId, User);
                var stationDtos = stations.Select(s => new KdsStationDto
                {
                    Id = s.Id,
                    Name = s.Name,
                    AreaId = s.AreaId,
                    OrganizationId = s.OrganizationId
                });
                _logger.LogInformation("Successfully retrieved {Count} KDS stations for OrganizationId: {OrganizationId}, AreaId: {AreaId}", stationDtos.Count(), organizationId, areaId);
                return Ok(stationDtos);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized access attempt to list KDS stations for Org {OrgId}, Area {AreaId}.", organizationId, areaId);
                return Forbid();
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogWarning(ex, "Area not found when listing KDS stations for Org {OrgId}, Area {AreaId}.", organizationId, areaId);
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting KDS stations for Org {OrgId}, Area {AreaId}.", organizationId, areaId);
                return StatusCode(500, "An internal server error occurred.");
            }
        }

        // GET: api/organizations/{organizationId}/areas/{areaId}/kds-stations/{kdsStationId}
        [HttpGet("{kdsStationId}")]
        [Authorize(Roles = "SuperAdmin,Admin,AreaAdmin")] // Define roles allowed to get a specific station
        public async Task<ActionResult<KdsStationDto>> GetKdsStation(Guid organizationId, int areaId, int kdsStationId)
        {
            _logger.LogInformation("Received request to get KDS station {KdsStationId} for OrganizationId: {OrganizationId}, AreaId: {AreaId}", kdsStationId, organizationId, areaId);
            try
            {
                var station = await _kdsStationService.GetKdsStationByIdAsync(organizationId, areaId, kdsStationId, User);
                if (station == null)
                {
                    _logger.LogWarning("KDS Station with ID {KdsStationId} not found in Area {AreaId} for OrganizationId {OrganizationId}.", kdsStationId, areaId, organizationId);
                    return NotFound($"KDS Station with ID {kdsStationId} not found in Area {areaId}.");
                }
                var stationDto = new KdsStationDto
                {
                    Id = station.Id,
                    Name = station.Name,
                    AreaId = station.AreaId,
                    OrganizationId = station.OrganizationId
                };
                _logger.LogInformation("Successfully retrieved KDS station {KdsStationId} for OrganizationId: {OrganizationId}, AreaId: {AreaId}", kdsStationId, organizationId, areaId);
                return Ok(stationDto);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized access attempt to get KDS station {KdsStationId} for Org {OrgId}, Area {AreaId}.", kdsStationId, organizationId, areaId);
                return Forbid();
            }
             catch (KeyNotFoundException ex)
            {
                _logger.LogWarning(ex, "Area not found when getting KDS station {KdsStationId} for Org {OrgId}, Area {AreaId}.", kdsStationId, organizationId, areaId);
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting KDS station {KdsStationId} for Org {OrgId}, Area {AreaId}.", kdsStationId, organizationId, areaId);
                return StatusCode(500, "An internal server error occurred.");
            }
        }

        // POST: api/organizations/{organizationId}/areas/{areaId}/kds-stations
        [HttpPost]
        [Authorize(Roles = "SuperAdmin,Admin")] // Only higher admins can create
        public async Task<ActionResult<KdsStationDto>> CreateKdsStation(Guid organizationId, int areaId, [FromBody] KdsStationUpsertDto kdsStationDto)
        {
            _logger.LogInformation("Received request to create KDS station '{KdsStationName}' for OrganizationId: {OrganizationId}, AreaId: {AreaId}", kdsStationDto.Name, organizationId, areaId);
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid model state for creating KDS station '{KdsStationName}' for OrganizationId: {OrganizationId}, AreaId: {AreaId}. Errors: {@Errors}", kdsStationDto.Name, organizationId, areaId, ModelState);
                return BadRequest(ModelState);
            }

            try
            {
                var newStation = new KdsStation { Name = kdsStationDto.Name }; // Map DTO to Model
                var createdStation = await _kdsStationService.CreateKdsStationAsync(organizationId, areaId, newStation, User);

                var createdStationDto = new KdsStationDto
                {
                    Id = createdStation.Id,
                    Name = createdStation.Name,
                    AreaId = createdStation.AreaId,
                    OrganizationId = createdStation.OrganizationId
                };

                _logger.LogInformation("Successfully created KDS station with Id: {KdsStationId}, Name: '{KdsStationName}' for OrganizationId: {OrganizationId}, AreaId: {AreaId}", createdStation.Id, createdStation.Name, organizationId, areaId);
                // Return 201 Created with the location of the new resource and the resource itself
                return CreatedAtAction(nameof(GetKdsStation), new { organizationId, areaId, kdsStationId = createdStation.Id }, createdStationDto);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized attempt to create KDS station in Org {OrgId}, Area {AreaId}.", organizationId, areaId);
                return Forbid();
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogWarning(ex, "Area not found when creating KDS station for Org {OrgId}, Area {AreaId}.", organizationId, areaId);
                return NotFound(ex.Message); // Area not found
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating KDS station for Org {OrgId}, Area {AreaId}.", organizationId, areaId);
                return StatusCode(500, "An internal server error occurred.");
            }
        }

        // PUT: api/organizations/{organizationId}/areas/{areaId}/kds-stations/{kdsStationId}
        [HttpPut("{kdsStationId}")]
        [Authorize(Roles = "SuperAdmin,Admin")] // Only higher admins can update
        public async Task<IActionResult> UpdateKdsStation(Guid organizationId, int areaId, int kdsStationId, [FromBody] KdsStationUpsertDto kdsStationDto)
        {
            _logger.LogInformation("Received request to update KDS station {KdsStationId} with Name: '{KdsStationName}' for OrganizationId: {OrganizationId}, AreaId: {AreaId}", kdsStationId, kdsStationDto.Name, organizationId, areaId);
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid model state for updating KDS station {KdsStationId}. Errors: {@Errors}", kdsStationId, ModelState);
                return BadRequest(ModelState);
            }

            try
            {
                var stationToUpdate = new KdsStation { Name = kdsStationDto.Name }; // Map DTO to Model
                var success = await _kdsStationService.UpdateKdsStationAsync(organizationId, areaId, kdsStationId, stationToUpdate, User);

                if (!success)
                {
                    _logger.LogWarning("KDS Station with ID {KdsStationId} not found in Area {AreaId} for OrganizationId {OrganizationId} during update.", kdsStationId, areaId, organizationId);
                    return NotFound($"KDS Station with ID {kdsStationId} not found in Area {areaId}.");
                }

                _logger.LogInformation("Successfully updated KDS station {KdsStationId} for OrganizationId: {OrganizationId}, AreaId: {AreaId}", kdsStationId, organizationId, areaId);
                return NoContent(); // Standard response for successful PUT
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized attempt to update KDS station {KdsStationId} in Org {OrgId}, Area {AreaId}.", kdsStationId, organizationId, areaId);
                return Forbid();
            }
            catch (KeyNotFoundException ex)
            {
                 _logger.LogWarning(ex, "Resource not found during update for KDS station {KdsStationId} in Org {OrgId}, Area {AreaId}.", kdsStationId, organizationId, areaId);
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating KDS station {KdsStationId} for Org {OrgId}, Area {AreaId}.", kdsStationId, organizationId, areaId);
                return StatusCode(500, "An internal server error occurred.");
            }
        }

        // DELETE: api/organizations/{organizationId}/areas/{areaId}/kds-stations/{kdsStationId}
        [HttpDelete("{kdsStationId}")]
        [Authorize(Roles = "SuperAdmin,Admin")] // Only higher admins can delete
        public async Task<IActionResult> DeleteKdsStation(Guid organizationId, int areaId, int kdsStationId)
        {
            _logger.LogInformation("Received request to delete KDS station {KdsStationId} for OrganizationId: {OrganizationId}, AreaId: {AreaId}", kdsStationId, organizationId, areaId);
            try
            {
                var success = await _kdsStationService.DeleteKdsStationAsync(organizationId, areaId, kdsStationId, User);

                if (!success)
                {
                    _logger.LogWarning("KDS Station with ID {KdsStationId} not found in Area {AreaId} for OrganizationId {OrganizationId} during deletion.", kdsStationId, areaId, organizationId);
                    return NotFound($"KDS Station with ID {kdsStationId} not found in Area {areaId}.");
                }

                _logger.LogInformation("Successfully deleted KDS station {KdsStationId} for OrganizationId: {OrganizationId}, AreaId: {AreaId}", kdsStationId, organizationId, areaId);
                return NoContent(); // Standard response for successful DELETE
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized attempt to delete KDS station {KdsStationId} in Org {OrgId}, Area {AreaId}.", kdsStationId, organizationId, areaId);
                return Forbid();
            }
             catch (KeyNotFoundException ex)
            {
                 _logger.LogWarning(ex, "Resource not found during delete for KDS station {KdsStationId} in Org {OrgId}, Area {AreaId}.", kdsStationId, organizationId, areaId);
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting KDS station {KdsStationId} for Org {OrgId}, Area {AreaId}.", kdsStationId, organizationId, areaId);
                return StatusCode(500, "An internal server error occurred.");
            }
        }

        // --- Category Assignments ---

        // GET: api/organizations/{organizationId}/areas/{areaId}/kds-stations/{kdsStationId}/categories
        [HttpGet("{kdsStationId}/categories")]
        [Authorize(Roles = "SuperAdmin,Admin,AreaAdmin")] // Roles allowed to view assignments
        public async Task<ActionResult<IEnumerable<MenuCategoryDto>>> GetAssignedCategories(Guid organizationId, int areaId, int kdsStationId)
        {
            _logger.LogInformation("Received request to get assigned categories for KDS station {KdsStationId}, OrganizationId: {OrganizationId}, AreaId: {AreaId}", kdsStationId, organizationId, areaId);
             try
            {
                var categories = await _kdsStationService.GetAssignedCategoriesAsync(organizationId, areaId, kdsStationId, User);
                var categoryDtos = categories.Select(c => new MenuCategoryDto // Assuming MenuCategoryDto exists and is suitable
                {
                    Id = c.Id,
                    Name = c.Name,
                    AreaId = c.AreaId
                    // Add other relevant fields if needed
                });
                _logger.LogInformation("Successfully retrieved {Count} assigned categories for KDS station {KdsStationId}, OrganizationId: {OrganizationId}, AreaId: {AreaId}", categoryDtos.Count(), kdsStationId, organizationId, areaId);
                return Ok(categoryDtos);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized access attempt to list assigned categories for KDS station {KdsStationId}, Org {OrgId}, Area {AreaId}.", kdsStationId, organizationId, areaId);
                return Forbid();
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogWarning(ex, "Resource not found when listing assigned categories for KDS station {KdsStationId}, Org {OrgId}, Area {AreaId}.", kdsStationId, organizationId, areaId);
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting assigned categories for KDS station {KdsStationId}, Org {OrgId}, Area {AreaId}.", kdsStationId, organizationId, areaId);
                return StatusCode(500, "An internal server error occurred.");
            }
        }

        // POST: api/organizations/{organizationId}/areas/{areaId}/kds-stations/{kdsStationId}/categories/{menuCategoryId}
        [HttpPost("{kdsStationId}/categories/{menuCategoryId}")]
        [Authorize(Roles = "SuperAdmin,Admin")] // Only higher admins can assign
        public async Task<IActionResult> AssignCategoryToKdsStation(Guid organizationId, int areaId, int kdsStationId, int menuCategoryId)
        {
            _logger.LogInformation("Received request to assign category {MenuCategoryId} to KDS station {KdsStationId}, OrganizationId: {OrganizationId}, AreaId: {AreaId}", menuCategoryId, kdsStationId, organizationId, areaId);
            try
            {
                var success = await _kdsStationService.AssignCategoryAsync(organizationId, areaId, kdsStationId, menuCategoryId, User);
                 if (!success)
                {
                    // Could be that the assignment already exists, or station/category not found (handled by exceptions below)
                    // Return Conflict if it already exists? Or just OK? Let's return OK for idempotency.
                     _logger.LogInformation("Assign category {MenuCategoryId} to KDS {KdsStationId} resulted in no change (likely already assigned).", menuCategoryId, kdsStationId);
                     return Ok($"Category {menuCategoryId} already assigned or assignment failed."); // Or NoContent()
                }
                _logger.LogInformation("Successfully assigned category {MenuCategoryId} to KDS station {KdsStationId}, OrganizationId: {OrganizationId}, AreaId: {AreaId}", menuCategoryId, kdsStationId, organizationId, areaId);
                return Ok($"Category {menuCategoryId} assigned successfully to KDS Station {kdsStationId}."); // Or return NoContent()
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized attempt to assign category {MenuCategoryId} to KDS station {KdsStationId}, Org {OrgId}, Area {AreaId}.", menuCategoryId, kdsStationId, organizationId, areaId);
                return Forbid();
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogWarning(ex, "Resource not found when assigning category {MenuCategoryId} to KDS station {KdsStationId}, Org {OrgId}, Area {AreaId}.", menuCategoryId, kdsStationId, organizationId, areaId);
                return NotFound(ex.Message); // Station or Category not found
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error assigning category {MenuCategoryId} to KDS station {KdsStationId}, Org {OrgId}, Area {AreaId}.", menuCategoryId, kdsStationId, organizationId, areaId);
                return StatusCode(500, "An internal server error occurred.");
            }
        }

        // DELETE: api/organizations/{organizationId}/areas/{areaId}/kds-stations/{kdsStationId}/categories/{menuCategoryId}
        [HttpDelete("{kdsStationId}/categories/{menuCategoryId}")]
        [Authorize(Roles = "SuperAdmin,Admin")] // Only higher admins can unassign
        public async Task<IActionResult> UnassignCategoryFromKdsStation(Guid organizationId, int areaId, int kdsStationId, int menuCategoryId)
        {
            _logger.LogInformation("Received request to unassign category {MenuCategoryId} from KDS station {KdsStationId}, OrganizationId: {OrganizationId}, AreaId: {AreaId}", menuCategoryId, kdsStationId, organizationId, areaId);
             try
            {
                var success = await _kdsStationService.UnassignCategoryAsync(organizationId, areaId, kdsStationId, menuCategoryId, User);
                if (!success)
                {
                    _logger.LogWarning("Assignment for Category {MenuCategoryId} and KDS Station {KdsStationId} not found during unassignment.", menuCategoryId, kdsStationId);
                    // Assignment didn't exist
                    return NotFound($"Assignment for Category {menuCategoryId} and KDS Station {kdsStationId} not found.");
                }
                _logger.LogInformation("Successfully unassigned category {MenuCategoryId} from KDS station {KdsStationId}, OrganizationId: {OrganizationId}, AreaId: {AreaId}", menuCategoryId, kdsStationId, organizationId, areaId);
                return NoContent(); // Standard response for successful DELETE
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized attempt to unassign category {MenuCategoryId} from KDS station {KdsStationId}, Org {OrgId}, Area {AreaId}.", menuCategoryId, kdsStationId, organizationId, areaId);
                return Forbid();
            }
             catch (KeyNotFoundException ex) // Should not happen if service logic is correct, but good practice
            {
                 _logger.LogWarning(ex, "Resource not found during unassignment for KDS station {KdsStationId}, Org {OrgId}, Area {AreaId}.", kdsStationId, organizationId, areaId);
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unassigning category {MenuCategoryId} from KDS station {KdsStationId}, Org {OrgId}, Area {AreaId}.", menuCategoryId, kdsStationId, organizationId, areaId);
                return StatusCode(500, "An internal server error occurred.");
            }
        }
    }
}
