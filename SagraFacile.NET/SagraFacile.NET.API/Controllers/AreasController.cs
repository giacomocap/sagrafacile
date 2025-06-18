using Microsoft.AspNetCore.Authorization; // Now used
using Microsoft.AspNetCore.Mvc;
using SagraFacile.NET.API.DTOs; // Ensure this includes AreaUpsertDto
using SagraFacile.NET.API.Models;
using SagraFacile.NET.API.Services.Interfaces;
using Microsoft.AspNetCore.Authorization; // Now used
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging; // Added for logging
using SagraFacile.NET.API.DTOs; // Ensure this includes AreaUpsertDto
using SagraFacile.NET.API.Models;
using SagraFacile.NET.API.Services.Interfaces;
using System; // Added for Exception handling
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SagraFacile.NET.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AreasController : ControllerBase
    {
        private readonly IAreaService _areaService;
        private readonly ILogger<AreasController> _logger; // Added for logging

        public AreasController(IAreaService areaService, ILogger<AreasController> logger)
        {
            _areaService = areaService;
            _logger = logger;
        }

        // GET: api/Areas
        [HttpGet]
        [Authorize(Roles = "SuperAdmin, Admin, Cashier, Waiter")]
        [ProducesResponseType(typeof(IEnumerable<AreaDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<AreaDto>>> GetAreas()
        {
            _logger.LogInformation("Received request to get all areas.");
            try
            {
                var areas = await _areaService.GetAllAreasAsync();
                _logger.LogInformation("Successfully retrieved {AreaCount} areas.", areas.Count());
                return Ok(areas);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized access attempt in GetAreas.");
                return Forbid();
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Invalid operation in GetAreas, possibly missing user context.");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving user context.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while getting areas.");
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        // GET: api/Areas/5
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(AreaDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<AreaDto>> GetArea(int id)
        {
            _logger.LogInformation("Received request to get area by ID: {AreaId}.", id);
            try
            {
                var areaDto = await _areaService.GetAreaByIdAsync(id);

                if (areaDto == null)
                {
                    _logger.LogWarning("Area with ID {AreaId} not found or access denied.", id);
                    return NotFound($"Area with ID {id} not found or access denied.");
                }

                _logger.LogInformation("Successfully retrieved area {AreaId}.", id);
                return Ok(areaDto);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized access attempt during GetArea for {AreaId}.", id);
                return Forbid();
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Invalid operation in GetArea for {AreaId}, possibly missing user context.", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving user context.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while getting area {AreaId}.", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        // POST: api/Areas
        [HttpPost]
        [Authorize(Roles = "Admin, SuperAdmin")]
        [ProducesResponseType(typeof(Area), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<Area>> PostArea([FromBody] Area area)
        {
            _logger.LogInformation("Received request to create area: {AreaName}.", area.Name);
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Create area request for {AreaName} failed due to invalid model state.", area.Name);
                return BadRequest(ModelState);
            }

            try
            {
                var createdArea = await _areaService.CreateAreaAsync(area);
                _logger.LogInformation("Area '{AreaName}' (ID: {AreaId}) created successfully.", createdArea.Name, createdArea.Id);
                return CreatedAtAction(nameof(GetArea), new { id = createdArea.Id }, createdArea);
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogWarning(ex, "Create area failed: {ErrorMessage}", ex.Message);
                return BadRequest(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized access attempt during PostArea.");
                return Forbid();
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Invalid operation in PostArea, possibly missing user context.");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving user context.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while creating the area {AreaName}.", area.Name);
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        // PUT: api/Areas/5
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin, SuperAdmin")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> PutArea(int id, [FromBody] AreaUpsertDto areaDto)
        {
            _logger.LogInformation("Received request to update area {AreaId}.", id);
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Update area request for {AreaId} failed due to invalid model state.", id);
                return BadRequest(ModelState);
            }

            try
            {
                var updateResult = await _areaService.UpdateAreaAsync(id, areaDto);

                if (!updateResult)
                {
                    if (!await _areaService.AreaExistsAsync(id))
                    {
                        _logger.LogWarning("Update area failed: Area with ID {AreaId} not found.", id);
                        return NotFound($"Area with ID {id} not found.");
                    }
                    else
                    {
                        _logger.LogError("An unknown error occurred while updating area {AreaId}.", id);
                        return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while updating the area.");
                    }
                }
                _logger.LogInformation("Area {AreaId} updated successfully.", id);
                return NoContent();
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogWarning(ex, "Update area failed: {ErrorMessage}", ex.Message);
                return BadRequest(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized access attempt during PutArea for {AreaId}.", id);
                return Forbid();
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Invalid operation in PutArea for {AreaId}, possibly missing user context.", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving user context.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while updating area {AreaId}.", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        // DELETE: api/Areas/5
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin, SuperAdmin")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteArea(int id)
        {
            _logger.LogInformation("Received request to delete area {AreaId}.", id);
            try
            {
                var deleteResult = await _areaService.DeleteAreaAsync(id);

                if (!deleteResult)
                {
                    if (!await _areaService.AreaExistsAsync(id))
                    {
                        _logger.LogWarning("Delete area failed: Area with ID {AreaId} not found.", id);
                        return NotFound($"Area with ID {id} not found.");
                    }
                    else
                    {
                        _logger.LogError("An unknown error occurred while deleting area {AreaId}. It might be in use.", id);
                        return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while deleting the area. It might be in use.");
                    }
                }
                _logger.LogInformation("Area {AreaId} deleted successfully.", id);
                return NoContent();
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized access attempt during DeleteArea for {AreaId}.", id);
                return Forbid();
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogWarning(ex, "Delete area failed: {ErrorMessage}", ex.Message);
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while deleting area {AreaId}.", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }
    }
}
