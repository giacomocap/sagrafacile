using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SagraFacile.NET.API.DTOs;
using SagraFacile.NET.API.Services.Interfaces;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SagraFacile.NET.API.Controllers
{
    [ApiController]
    public class CashierStationsController : ControllerBase
    {
        private readonly ICashierStationService _cashierStationService;

        public CashierStationsController(ICashierStationService cashierStationService)
        {
            _cashierStationService = cashierStationService;
        }

        // GET: api/cashierstations/organization/{organizationId}
        [HttpGet("api/cashierstations/organization/{organizationId}")]
        [Authorize(Roles = "SuperAdmin, Admin, AreaAdmin")] // Admins can see all stations in an org
        [ProducesResponseType(typeof(IEnumerable<CashierStationDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<IEnumerable<CashierStationDto>>> GetStationsByOrganization(int organizationId)
        {
            // Service handles actual access check to the organizationId based on HttpContext user
            var stations = await _cashierStationService.GetStationsByOrganizationAsync(organizationId, null); // User passed as null, service derives from HttpContext
            return Ok(stations);
        }

        // GET: api/cashierstations/area/{areaId}
        [HttpGet("api/cashierstations/area/{areaId}")]
        [Authorize(Roles = "SuperAdmin, Admin, AreaAdmin, Cashier, Waiter")] // Cashiers/Waiters might need this to select their station
        [ProducesResponseType(typeof(IEnumerable<CashierStationDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)] // If area itself not found or no access
        public async Task<ActionResult<IEnumerable<CashierStationDto>>> GetStationsByArea(int areaId)
        {
            var stations = await _cashierStationService.GetStationsByAreaAsync(areaId, null);
            // The service returns an empty list if area not found or no access to its org, which is fine for this endpoint.
            return Ok(stations);
        }

        // GET: api/cashierstations/{stationId}
        [HttpGet("api/cashierstations/{stationId}")]
        [Authorize(Roles = "SuperAdmin, Admin, AreaAdmin, Cashier, Waiter")] 
        [ProducesResponseType(typeof(CashierStationDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<CashierStationDto>> GetStation(int stationId)
        {
            var station = await _cashierStationService.GetStationByIdAsync(stationId, null);
            if (station == null)
            {
                return NotFound($"Cashier Station with ID {stationId} not found or not accessible.");
            }
            return Ok(station);
        }

        // POST: api/cashierstations/organization/{organizationId}
        [HttpPost("api/cashierstations/organization/{organizationId}")]
        [Authorize(Roles = "SuperAdmin, Admin")]
        [ProducesResponseType(typeof(CashierStationDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<CashierStationDto>> CreateStation(int organizationId, [FromBody] CashierStationUpsertDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var (station, error) = await _cashierStationService.CreateStationAsync(organizationId, dto, null);

            if (error != null)
            {
                // Check for specific error types if needed, or just return BadRequest
                if (error.Contains("Unauthorized")) return Forbid(error);
                return BadRequest(error);
            }
            if (station == null) return BadRequest("Failed to create cashier station."); // Should have error if null

            return CreatedAtAction(nameof(GetStation), new { stationId = station.Id }, station);
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
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var (station, error) = await _cashierStationService.UpdateStationAsync(stationId, dto, null);

            if (error != null)
            {
                if (error.Contains("not found")) return NotFound(error);
                if (error.Contains("Unauthorized")) return Forbid(error);
                return BadRequest(error);
            }
            
            return NoContent();
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
            var (success, error) = await _cashierStationService.DeleteStationAsync(stationId, null);

            if (!success)
            {
                if (error == null) return StatusCode(500, "An unknown error occurred during deletion.");
                if (error.Contains("not found")) return NotFound(error);
                if (error.Contains("Unauthorized")) return Forbid(error);
                // If error is about being in use, return BadRequest or Conflict (409)
                if (error.Contains("associated with existing orders")) return BadRequest(error);
                return BadRequest(error);
            }

            return NoContent();
        }
    }
}
