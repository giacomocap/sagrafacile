using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SagraFacile.NET.API.DTOs;
using SagraFacile.NET.API.Models; // For DayStatus if needed directly
using SagraFacile.NET.API.Services; // For BaseService
using SagraFacile.NET.API.Services.Interfaces;
using System.Security.Claims;

namespace SagraFacile.NET.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] // Require authentication for all day operations
    public class DaysController : ControllerBase // Correct: Inherit from ControllerBase
    {
        private readonly IDayService _dayService;
        private readonly ILogger<DaysController> _logger;

        // Remove IHttpContextAccessor injection and base() call
        public DaysController(
            IDayService dayService,
            ILogger<DaysController> logger)
        {
            _dayService = dayService;
            _logger = logger;
        }

        // GET: api/days/current
        [HttpGet("current")]
        [ProducesResponseType(typeof(DayDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        // Allow any authenticated user within the org to check the current day
        public async Task<ActionResult<DayDto>> GetCurrentOpenDay()
        {
            _logger.LogInformation("Received request to get current open day.");
            // Pass the User (ClaimsPrincipal) to the service layer
            // The service layer will handle extracting context and authorization
            try
            {
                var dayDto = await _dayService.GetCurrentOpenDayForUserAsync(User); // Pass User principal
                if (dayDto == null)
                {
                    _logger.LogInformation("No operational day is currently open for this organization, or access denied.");
                    return NotFound("No operational day (Giornata) is currently open for this organization, or access denied.");
                }
                _logger.LogInformation("Successfully retrieved current open day with ID: {DayId}", dayDto.Id);
                return Ok(dayDto);
            }
            catch (UnauthorizedAccessException ex)
            {
                 _logger.LogWarning(ex, "Unauthorized access attempt to get current open day: {Message}", ex.Message); // Log updated for clarity
                 return Forbid(); // Return Forbidden (403) instead of Unauthorized (401)
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while retrieving the current open day.");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving the current day.");
            }
        }

        // POST: api/days/open
        [HttpPost("open")]
        [Authorize(Roles = "Admin")] // Only Admins can open a day
        [ProducesResponseType(typeof(DayDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<DayDto>> OpenDay()
        {
            _logger.LogInformation("Received request to open a new operational day.");
            // Pass User principal to the service layer
            try
            {
                var openedDay = await _dayService.OpenDayAsync(User); // Pass User principal - Corrected method name
                _logger.LogInformation("Successfully opened new operational day with ID: {DayId}", openedDay.Id);
                // Return 201 Created with the location header and the created DTO
                return CreatedAtAction(nameof(GetDayById), new { id = openedDay.Id }, openedDay);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized access attempt to open a new operational day: {Message}", ex.Message);
                // Could be Unauthorized (no context) or Forbidden (SuperAdmin attempt)
                // Let's return Forbid for clarity as only Admins should hit this anyway.
                return Forbid();
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Failed to open new operational day: {Message}", ex.Message);
                return BadRequest(ex.Message); // e.g., "A day is already open." or other service validation
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while opening a new operational day.");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while opening the day.");
            }
        }

        // POST: api/days/{id}/close
        [HttpPost("{id}/close")]
        [Authorize(Roles = "Admin")] // Only Admins can close a day
        [ProducesResponseType(typeof(DayDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<DayDto>> CloseDay(int id)
        {
            _logger.LogInformation("Received request to close operational day with ID: {DayId}", id);
            // Pass User principal and day ID to the service layer
            try
            {
                var closedDay = await _dayService.CloseDayAsync(id, User); // Pass id and User principal - Corrected method name
                _logger.LogInformation("Successfully closed operational day with ID: {DayId}", closedDay.Id);
                return Ok(closedDay);
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogWarning(ex, "Failed to close operational day {DayId}: {Message}", id, ex.Message);
                return NotFound(ex.Message); // Day not found
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized access attempt to close operational day {DayId}: {Message}", id, ex.Message);
                // Could be Unauthorized (no context), Forbidden (SuperAdmin), or Forbidden (wrong org)
                return Forbid();
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Failed to close operational day {DayId}: {Message}", id, ex.Message);
                return BadRequest(ex.Message); // e.g., "Day is already closed." or other service validation
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while closing operational day {DayId}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while closing the day.");
            }
        }

        // GET: api/days
        [HttpGet]
        [Authorize(Roles = "Admin")] // Only Admins can list days
        [ProducesResponseType(typeof(IEnumerable<DayDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<IEnumerable<DayDto>>> GetDays()
        {
            _logger.LogInformation("Received request to get all operational days.");
            // Pass User principal to the service layer
            try
            {
                var days = await _dayService.GetDaysForUserAsync(User); // Pass User principal
                _logger.LogInformation("Successfully retrieved {Count} operational days.", ((List<DayDto>)days).Count);
                return Ok(days);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized access attempt to get all operational days: {Message}", ex.Message);
                // Could be Unauthorized (no context) or Forbidden (SuperAdmin attempt)
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while retrieving operational days.");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving days.");
            }
        }

        // GET: api/days/{id} - Added for CreatedAtAction in OpenDay
        [HttpGet("{id}")]
        [Authorize(Roles = "Admin")] // Only Admins can get a specific day by ID
        [ProducesResponseType(typeof(DayDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<DayDto>> GetDayById(int id)
        {
            _logger.LogInformation("Received request to get operational day with ID: {DayId}", id);
            // Pass User principal and day ID to the service layer
            try
            {
                var dayDto = await _dayService.GetDayByIdForUserAsync(id, User); // Pass id and User principal

                if (dayDto == null)
                {
                    // Service layer handles not found or unauthorized access by returning null
                    _logger.LogInformation("Operational day with ID {DayId} not found or access denied.", id);
                    return NotFound($"Day with ID {id} not found or access denied.");
                }
                _logger.LogInformation("Successfully retrieved operational day with ID: {DayId}", id);
                return Ok(dayDto);
            }
            catch (UnauthorizedAccessException ex)
            {
                 _logger.LogWarning(ex, "Unauthorized access attempt to get operational day {DayId}: {Message}", id, ex.Message);
                 // Could be Unauthorized (no context) or Forbidden (SuperAdmin attempt)
                 return Forbid();
            }
            catch (Exception ex)
            {
                 _logger.LogError(ex, "An error occurred while retrieving operational day {DayId}", id);
                 return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving the day.");
            }
        }
    }
}
