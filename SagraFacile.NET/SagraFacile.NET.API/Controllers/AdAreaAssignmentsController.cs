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
            var assignments = await _adAreaAssignmentService.GetAssignmentsForAreaAsync(areaId);
            return Ok(assignments);
        }

        [HttpPost("admin/ad-assignments")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<ActionResult<AdAreaAssignmentDto>> PostAssignment(AdAreaAssignmentUpsertDto assignmentDto)
        {
            var result = await _adAreaAssignmentService.CreateAssignmentAsync(assignmentDto);

            if (!result.Success)
            {
                return BadRequest(new { message = result.Error });
            }

            return CreatedAtAction(nameof(GetAssignments), new { areaId = result.Data.AreaId }, result.Data);
        }

        [HttpPut("admin/ad-assignments/{assignmentId}")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> PutAssignment(Guid assignmentId, AdAreaAssignmentUpsertDto assignmentDto)
        {
            var result = await _adAreaAssignmentService.UpdateAssignmentAsync(assignmentId, assignmentDto);

            if (!result.Success)
            {
                if (result.Error.Contains("not found"))
                {
                    return NotFound(new { message = result.Error });
                }
                return BadRequest(new { message = result.Error });
            }

            return NoContent();
        }

        [HttpDelete("admin/ad-assignments/{assignmentId}")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> DeleteAssignment(Guid assignmentId)
        {
            var result = await _adAreaAssignmentService.DeleteAssignmentAsync(assignmentId);

            if (!result.Success)
            {
                if (result.Error.Contains("not found"))
                {
                    return NotFound(new { message = result.Error });
                }
                return BadRequest(new { message = result.Error });
            }

            return NoContent();
        }
    }
}
