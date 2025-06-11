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
        public async Task<ActionResult<IEnumerable<KdsStationDto>>> GetKdsStations(int organizationId, int areaId)
        {
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
        public async Task<ActionResult<KdsStationDto>> GetKdsStation(int organizationId, int areaId, int kdsStationId)
        {
            try
            {
                var station = await _kdsStationService.GetKdsStationByIdAsync(organizationId, areaId, kdsStationId, User);
                if (station == null)
                {
                    return NotFound($"KDS Station with ID {kdsStationId} not found in Area {areaId}.");
                }
                var stationDto = new KdsStationDto
                {
                    Id = station.Id,
                    Name = station.Name,
                    AreaId = station.AreaId,
                    OrganizationId = station.OrganizationId
                };
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
        public async Task<ActionResult<KdsStationDto>> CreateKdsStation(int organizationId, int areaId, [FromBody] KdsStationUpsertDto kdsStationDto)
        {
            if (!ModelState.IsValid)
            {
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
        public async Task<IActionResult> UpdateKdsStation(int organizationId, int areaId, int kdsStationId, [FromBody] KdsStationUpsertDto kdsStationDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var stationToUpdate = new KdsStation { Name = kdsStationDto.Name }; // Map DTO to Model
                var success = await _kdsStationService.UpdateKdsStationAsync(organizationId, areaId, kdsStationId, stationToUpdate, User);

                if (!success)
                {
                    return NotFound($"KDS Station with ID {kdsStationId} not found in Area {areaId}.");
                }

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
        public async Task<IActionResult> DeleteKdsStation(int organizationId, int areaId, int kdsStationId)
        {
            try
            {
                var success = await _kdsStationService.DeleteKdsStationAsync(organizationId, areaId, kdsStationId, User);

                if (!success)
                {
                    return NotFound($"KDS Station with ID {kdsStationId} not found in Area {areaId}.");
                }

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
        public async Task<ActionResult<IEnumerable<MenuCategoryDto>>> GetAssignedCategories(int organizationId, int areaId, int kdsStationId)
        {
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
        public async Task<IActionResult> AssignCategoryToKdsStation(int organizationId, int areaId, int kdsStationId, int menuCategoryId)
        {
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
        public async Task<IActionResult> UnassignCategoryFromKdsStation(int organizationId, int areaId, int kdsStationId, int menuCategoryId)
        {
             try
            {
                var success = await _kdsStationService.UnassignCategoryAsync(organizationId, areaId, kdsStationId, menuCategoryId, User);
                if (!success)
                {
                    // Assignment didn't exist
                    return NotFound($"Assignment for Category {menuCategoryId} and KDS Station {kdsStationId} not found.");
                }
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
