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
            // Pass the User (ClaimsPrincipal) to the service layer
            // The service layer will handle extracting context and authorization
            try
            {
                var dayDto = await _dayService.GetCurrentOpenDayForUserAsync(User); // Pass User principal
                if (dayDto == null)
                {
                    return NotFound("No operational day (Giornata) is currently open for this organization, or access denied.");
                }
                return Ok(dayDto);
            }
            catch (UnauthorizedAccessException ex)
            {
                 _logger.LogWarning("GetCurrentOpenDay Forbidden: {Message}", ex.Message); // Log updated for clarity
                 return Forbid(); // Return Forbidden (403) instead of Unauthorized (401)
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting current open day.");
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
            // Pass User principal to the service layer
            try
            {
                var openedDay = await _dayService.OpenDayAsync(User); // Pass User principal - Corrected method name
                // Return 201 Created with the location header and the created DTO
                return CreatedAtAction(nameof(GetDayById), new { id = openedDay.Id }, openedDay);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning("OpenDay Unauthorized: {Message}", ex.Message);
                // Could be Unauthorized (no context) or Forbidden (SuperAdmin attempt)
                // Let's return Forbid for clarity as only Admins should hit this anyway.
                return Forbid();
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning("OpenDay failed: {Message}", ex.Message);
                return BadRequest(ex.Message); // e.g., "A day is already open." or other service validation
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error opening day.");
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
            // Pass User principal and day ID to the service layer
            try
            {
                var closedDay = await _dayService.CloseDayAsync(id, User); // Pass id and User principal - Corrected method name
                return Ok(closedDay);
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogWarning("CloseDay failed: {Message}", ex.Message);
                return NotFound(ex.Message); // Day not found
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning("CloseDay Unauthorized/Forbidden: {Message}", ex.Message);
                // Could be Unauthorized (no context), Forbidden (SuperAdmin), or Forbidden (wrong org)
                return Forbid();
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning("CloseDay failed: {Message}", ex.Message);
                return BadRequest(ex.Message); // e.g., "Day is already closed." or other service validation
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error closing day {DayId}", id);
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
            // Pass User principal to the service layer
            try
            {
                var days = await _dayService.GetDaysForUserAsync(User); // Pass User principal
                return Ok(days);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning("GetDays Unauthorized/Forbidden: {Message}", ex.Message);
                // Could be Unauthorized (no context) or Forbidden (SuperAdmin attempt)
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting days.");
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
            // Pass User principal and day ID to the service layer
            try
            {
                var dayDto = await _dayService.GetDayByIdForUserAsync(id, User); // Pass id and User principal

                if (dayDto == null)
                {
                    // Service layer handles not found or unauthorized access by returning null
                    return NotFound($"Day with ID {id} not found or access denied.");
                }
                return Ok(dayDto);
            }
            catch (UnauthorizedAccessException ex)
            {
                 _logger.LogWarning("GetDayById Unauthorized/Forbidden: {Message}", ex.Message);
                 // Could be Unauthorized (no context) or Forbidden (SuperAdmin attempt)
                 return Forbid();
            }
            catch (Exception ex)
            {
                 _logger.LogError(ex, "Error getting day {DayId}", id);
                 return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving the day.");
            }
        }
    }
}
