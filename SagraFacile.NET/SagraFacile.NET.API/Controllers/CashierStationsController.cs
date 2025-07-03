using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SagraFacile.NET.API.DTOs;
using SagraFacile.NET.API.Services.Interfaces;

namespace SagraFacile.NET.API.Controllers
{
    [ApiController]
    public class CashierStationsController : ControllerBase
    {
        private readonly ICashierStationService _cashierStationService;
        private readonly ILogger<CashierStationsController> _logger; // Added

        public CashierStationsController(ICashierStationService cashierStationService, ILogger<CashierStationsController> logger) // Added ILogger
        {
            _cashierStationService = cashierStationService ?? throw new ArgumentNullException(nameof(cashierStationService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger)); // Added
        }

        // GET: api/cashierstations/organization/{organizationId}
        [HttpGet("api/cashierstations/organization/{organizationId}")]
        [Authorize(Roles = "SuperAdmin, Admin, AreaAdmin")] // Admins can see all stations in an org
        [ProducesResponseType(typeof(IEnumerable<CashierStationDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<IEnumerable<CashierStationDto>>> GetStationsByOrganization(Guid organizationId)
        {
            _logger.LogInformation("Received request to get cashier stations for OrganizationId: {OrganizationId}", organizationId);
            try
            {
                // Service handles actual access check to the organizationId based on HttpContext user
                var stations = await _cashierStationService.GetStationsByOrganizationAsync(organizationId, null); // User passed as null, service derives from HttpContext
                _logger.LogInformation("Successfully retrieved {Count} cashier stations for OrganizationId: {OrganizationId}", ((List<CashierStationDto>)stations).Count, organizationId);
                return Ok(stations);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized access attempt to get cashier stations for OrganizationId: {OrganizationId}", organizationId);
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while getting cashier stations for OrganizationId: {OrganizationId}", organizationId);
                return StatusCode(500, "An error occurred while getting cashier stations.");
            }
        }

        // GET: api/cashierstations/area/{areaId}
        [HttpGet("api/cashierstations/area/{areaId}")]
        [Authorize(Roles = "SuperAdmin, Admin, AreaAdmin, Cashier, Waiter")] // Cashiers/Waiters might need this to select their station
        [ProducesResponseType(typeof(IEnumerable<CashierStationDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)] // If area itself not found or no access
        public async Task<ActionResult<IEnumerable<CashierStationDto>>> GetStationsByArea(int areaId)
        {
            _logger.LogInformation("Received request to get cashier stations for AreaId: {AreaId}", areaId);
            try
            {
                var stations = await _cashierStationService.GetStationsByAreaAsync(areaId, null);
                // The service returns an empty list if area not found or no access to its org, which is fine for this endpoint.
                _logger.LogInformation("Successfully retrieved {Count} cashier stations for AreaId: {AreaId}", ((List<CashierStationDto>)stations).Count, areaId);
                return Ok(stations);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized access attempt to get cashier stations for AreaId: {AreaId}", areaId);
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while getting cashier stations for AreaId: {AreaId}", areaId);
                return StatusCode(500, "An error occurred while getting cashier stations.");
            }
        }

        // GET: api/cashierstations/{stationId}
        [HttpGet("api/cashierstations/{stationId}")]
        [Authorize(Roles = "SuperAdmin, Admin, AreaAdmin, Cashier, Waiter")] 
        [ProducesResponseType(typeof(CashierStationDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<CashierStationDto>> GetStation(int stationId)
        {
            _logger.LogInformation("Received request to get cashier station with Id: {StationId}", stationId);
            try
            {
                var station = await _cashierStationService.GetStationByIdAsync(stationId, null);
                if (station == null)
                {
                    _logger.LogWarning("Cashier Station with ID {StationId} not found or not accessible.", stationId);
                    return NotFound($"Cashier Station with ID {stationId} not found or not accessible.");
                }
                _logger.LogInformation("Successfully retrieved cashier station with Id: {StationId}", stationId);
                return Ok(station);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized access attempt to get cashier station with Id: {StationId}", stationId);
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while getting cashier station with Id: {StationId}", stationId);
                return StatusCode(500, "An error occurred while getting cashier station.");
            }
        }

        // POST: api/cashierstations/organization/{organizationId}
        [HttpPost("api/cashierstations/organization/{organizationId}")]
        [Authorize(Roles = "SuperAdmin, Admin")]
        [ProducesResponseType(typeof(CashierStationDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<CashierStationDto>> CreateStation(Guid organizationId, [FromBody] CashierStationUpsertDto dto)
        {
            _logger.LogInformation("Received request to create cashier station for OrganizationId: {OrganizationId}, Name: {Name}", organizationId, dto.Name);
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid model state for creating cashier station for OrganizationId: {OrganizationId}. Errors: {@Errors}", organizationId, ModelState);
                return BadRequest(ModelState);
            }

            try
            {
                var (station, error) = await _cashierStationService.CreateStationAsync(organizationId, dto, null);

                if (error != null)
                {
                    _logger.LogWarning("Failed to create cashier station for OrganizationId: {OrganizationId}, Name: {Name}. Error: {Error}", organizationId, dto.Name, error);
                    // Check for specific error types if needed, or just return BadRequest
                    if (error.Contains("Unauthorized")) return Forbid(error);
                    return BadRequest(error);
                }
                if (station == null)
                {
                    _logger.LogError("Cashier station creation returned null despite no error for OrganizationId: {OrganizationId}, Name: {Name}", organizationId, dto.Name);
                    return BadRequest("Failed to create cashier station."); // Should have error if null
                }

                _logger.LogInformation("Successfully created cashier station with Id: {StationId}, Name: {Name} for OrganizationId: {OrganizationId}", station.Id, station.Name, organizationId);
                return CreatedAtAction(nameof(GetStation), new { stationId = station.Id }, station);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized access attempt to create cashier station for OrganizationId: {OrganizationId}", organizationId);
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while creating cashier station for OrganizationId: {OrganizationId}, Name: {Name}", organizationId, dto.Name);
                return StatusCode(500, "An error occurred while creating cashier station.");
            }
        }

        // PUT: api/cashierstations/{stationId}
        [HttpPut("api/cashierstations/{stationId}")]
        [Authorize(Roles = "SuperAdmin, Admin")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> UpdateStation(int stationId, [FromBody] CashierStationUpsertDto dto)
        {
            _logger.LogInformation("Received request to update cashier station with Id: {StationId}, Name: {Name}", stationId, dto.Name);
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid model state for updating cashier station with Id: {StationId}. Errors: {@Errors}", stationId, ModelState);
                return BadRequest(ModelState);
            }

            try
            {
                var (station, error) = await _cashierStationService.UpdateStationAsync(stationId, dto, null);

                if (error != null)
                {
                    _logger.LogWarning("Failed to update cashier station with Id: {StationId}, Name: {Name}. Error: {Error}", stationId, dto.Name, error);
                    if (error.Contains("not found")) return NotFound(error);
                    if (error.Contains("Unauthorized")) return Forbid(error);
                    return BadRequest(error);
                }
                
                _logger.LogInformation("Successfully updated cashier station with Id: {StationId}", stationId);
                return NoContent();
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized access attempt to update cashier station with Id: {StationId}", stationId);
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while updating cashier station with Id: {StationId}, Name: {Name}", stationId, dto.Name);
                return StatusCode(500, "An error occurred while updating cashier station.");
            }
        }

        // DELETE: api/cashierstations/{stationId}
        [HttpDelete("api/cashierstations/{stationId}")]
        [Authorize(Roles = "SuperAdmin, Admin")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)] // For errors like "in use"
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> DeleteStation(int stationId)
        {
            _logger.LogInformation("Received request to delete cashier station with Id: {StationId}", stationId);
            try
            {
                var (success, error) = await _cashierStationService.DeleteStationAsync(stationId, null);

                if (!success)
                {
                    _logger.LogWarning("Failed to delete cashier station with Id: {StationId}. Error: {Error}", stationId, error);
                    if (error == null) return StatusCode(500, "An unknown error occurred during deletion.");
                    if (error.Contains("not found")) return NotFound(error);
                    if (error.Contains("Unauthorized")) return Forbid(error);
                    // If error is about being in use, return BadRequest or Conflict (409)
                    if (error.Contains("associated with existing orders")) return BadRequest(error);
                    return BadRequest(error);
                }

                _logger.LogInformation("Successfully deleted cashier station with Id: {StationId}", stationId);
                return NoContent();
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized access attempt to delete cashier station with Id: {StationId}", stationId);
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while deleting cashier station with Id: {StationId}", stationId);
                return StatusCode(500, "An error occurred while deleting cashier station.");
            }
        }
    }
}
