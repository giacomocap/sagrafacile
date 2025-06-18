using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SagraFacile.NET.API.DTOs;
using SagraFacile.NET.API.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SagraFacile.NET.API.Controllers
{
    [Route("api/")]
    [ApiController]
    public class AdAreaAssignmentsController : ControllerBase
    {
        private readonly IAdAreaAssignmentService _adAreaAssignmentService;
        private readonly ILogger<AdAreaAssignmentsController> _logger;

        public AdAreaAssignmentsController(IAdAreaAssignmentService adAreaAssignmentService, ILogger<AdAreaAssignmentsController> logger)
        {
            _adAreaAssignmentService = adAreaAssignmentService;
            _logger = logger;
        }

        [HttpGet("admin/areas/{areaId}/ad-assignments")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<ActionResult<IEnumerable<AdAreaAssignmentDto>>> GetAssignments(int areaId)
        {
            _logger.LogInformation("Received request to get ad area assignments for AreaId: {AreaId}", areaId);
            try
            {
                var assignments = await _adAreaAssignmentService.GetAssignmentsForAreaAsync(areaId);
                _logger.LogInformation("Successfully retrieved {Count} ad area assignments for AreaId: {AreaId}", ((List<AdAreaAssignmentDto>)assignments).Count, areaId);
                return Ok(assignments);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized access attempt to get ad area assignments for AreaId: {AreaId}", areaId);
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while getting ad area assignments for AreaId: {AreaId}", areaId);
                return StatusCode(500, "An error occurred while getting ad area assignments.");
            }
        }

        [HttpPost("admin/ad-assignments")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<ActionResult<AdAreaAssignmentDto>> PostAssignment(AdAreaAssignmentUpsertDto assignmentDto)
        {
            _logger.LogInformation("Received request to create ad area assignment for AreaId: {AreaId}, AdMediaItemId: {AdMediaItemId}", assignmentDto.AreaId, assignmentDto.AdMediaItemId);
            try
            {
                var result = await _adAreaAssignmentService.CreateAssignmentAsync(assignmentDto);

                if (!result.Success)
                {
                    _logger.LogWarning("Failed to create ad area assignment for AreaId: {AreaId}, AdMediaItemId: {AdMediaItemId}. Error: {Error}", assignmentDto.AreaId, assignmentDto.AdMediaItemId, result.Error);
                    return BadRequest(new { message = result.Error });
                }

                _logger.LogInformation("Successfully created ad area assignment with Id: {AssignmentId} for AreaId: {AreaId}", result.Data.Id, result.Data.AreaId);
                return CreatedAtAction(nameof(GetAssignments), new { areaId = result.Data.AreaId }, result.Data);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized access attempt to create ad area assignment for AreaId: {AreaId}", assignmentDto.AreaId);
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while creating ad area assignment for AreaId: {AreaId}", assignmentDto.AreaId);
                return StatusCode(500, "An error occurred while creating ad area assignment.");
            }
        }

        [HttpPut("admin/ad-assignments/{assignmentId}")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> PutAssignment(Guid assignmentId, AdAreaAssignmentUpsertDto assignmentDto)
        {
            _logger.LogInformation("Received request to update ad area assignment with Id: {AssignmentId}", assignmentId);
            try
            {
                var result = await _adAreaAssignmentService.UpdateAssignmentAsync(assignmentId, assignmentDto);

                if (!result.Success)
                {
                    _logger.LogWarning("Failed to update ad area assignment with Id: {AssignmentId}. Error: {Error}", assignmentId, result.Error);
                    if (result.Error.Contains("not found"))
                    {
                        return NotFound(new { message = result.Error });
                    }
                    return BadRequest(new { message = result.Error });
                }

                _logger.LogInformation("Successfully updated ad area assignment with Id: {AssignmentId}", assignmentId);
                return NoContent();
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized access attempt to update ad area assignment with Id: {AssignmentId}", assignmentId);
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while updating ad area assignment with Id: {AssignmentId}", assignmentId);
                return StatusCode(500, "An error occurred while updating ad area assignment.");
            }
        }

        [HttpDelete("admin/ad-assignments/{assignmentId}")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> DeleteAssignment(Guid assignmentId)
        {
            _logger.LogInformation("Received request to delete ad area assignment with Id: {AssignmentId}", assignmentId);
            try
            {
                var result = await _adAreaAssignmentService.DeleteAssignmentAsync(assignmentId);

                if (!result.Success)
                {
                    _logger.LogWarning("Failed to delete ad area assignment with Id: {AssignmentId}. Error: {Error}", assignmentId, result.Error);
                    if (result.Error.Contains("not found"))
                    {
                        return NotFound(new { message = result.Error });
                    }
                    return BadRequest(new { message = result.Error });
                }

                _logger.LogInformation("Successfully deleted ad area assignment with Id: {AssignmentId}", assignmentId);
                return NoContent();
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized access attempt to delete ad area assignment with Id: {AssignmentId}", assignmentId);
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while deleting ad area assignment with Id: {AssignmentId}", assignmentId);
                return StatusCode(500, "An error occurred while deleting ad area assignment.");
            }
        }
    }
}
