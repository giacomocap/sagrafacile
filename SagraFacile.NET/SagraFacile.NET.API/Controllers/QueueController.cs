using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SagraFacile.NET.API.DTOs;
using SagraFacile.NET.API.Services.Interfaces;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace SagraFacile.NET.API.Controllers
{
    [ApiController]
    [Route("api/areas/{areaId}/queue")]
    [Authorize] // Require authentication for all queue operations
    public class QueueController : ControllerBase
    {
        private readonly IQueueService _queueService;
        private readonly ILogger<QueueController> _logger;

        public QueueController(IQueueService queueService, ILogger<QueueController> logger)
        {
            _queueService = queueService;
            _logger = logger;
        }

        // GET /api/areas/{areaId}/queue/state
        [HttpGet("state")]
        [ProducesResponseType(typeof(QueueStateDto), 200)]
        [ProducesResponseType(404)] // Area not found or not authorized
        [ProducesResponseType(400)] // Bad request (e.g., context issue)
        public async Task<IActionResult> GetState(int areaId)
        {
            _logger.LogInformation("Received request to get queue state for Area {AreaId}.", areaId);
            var result = await _queueService.GetQueueStateAsync(areaId);
            if (!result.Success)
            {
                 // Distinguish between not found/auth error (404) and other errors (400)
                 // Assuming Fail messages indicate the type
                 if (result.Errors.Any(e => e.Contains("not found", StringComparison.OrdinalIgnoreCase) || e.Contains("not authorized", StringComparison.OrdinalIgnoreCase)))
                 {
                      _logger.LogWarning("Queue state for Area {AreaId} not found or not authorized: {Errors}", areaId, string.Join("; ", result.Errors));
                      return NotFound(string.Join("; ", result.Errors));
                 }
                 _logger.LogError("Failed to get queue state for Area {AreaId}: {Errors}", areaId, string.Join("; ", result.Errors));
                 return BadRequest(string.Join("; ", result.Errors));
            }
            _logger.LogInformation("Successfully retrieved queue state for Area {AreaId}.", areaId);
            return Ok(result.Value);
        }

        // POST /api/areas/{areaId}/queue/call-next
        [HttpPost("call-next")]
        [Authorize(Roles = "Cashier,Admin,SuperAdmin")] // Specify roles allowed to call
        [ProducesResponseType(typeof(CalledNumberDto), 200)]
        [ProducesResponseType(400)] // e.g., station invalid, queue disabled, conflict
        [ProducesResponseType(404)] // Area or Station not found
        public async Task<IActionResult> CallNext(int areaId, [FromBody] CallNextRequest request)
        {
            _logger.LogInformation("Received request to call next queue number for Area {AreaId} from CashierStation {CashierStationId}.", areaId, request.CashierStationId);
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid model state for CallNext request for Area {AreaId}: {@ModelState}", areaId, ModelState);
                return BadRequest(ModelState);
            }

            var result = await _queueService.CallNextAsync(areaId, request.CashierStationId);

            if (!result.Success)
            {
                 if (result.Errors.Any(e => e.Contains("not found", StringComparison.OrdinalIgnoreCase)))
                 {
                      _logger.LogWarning("Area {AreaId} or CashierStation {CashierStationId} not found for CallNext: {Errors}", areaId, request.CashierStationId, string.Join("; ", result.Errors));
                      return NotFound(string.Join("; ", result.Errors));
                 }
                _logger.LogError("Failed to call next queue number for Area {AreaId} from CashierStation {CashierStationId}: {Errors}", areaId, request.CashierStationId, string.Join("; ", result.Errors));
                return BadRequest(string.Join("; ", result.Errors));
            }
            _logger.LogInformation("Successfully called next queue number {TicketNumber} for Area {AreaId} from CashierStation {CashierStationId}.", result.Value.TicketNumber, areaId, request.CashierStationId);
            return Ok(result.Value);
        }

        // POST /api/areas/{areaId}/queue/call-specific
        [HttpPost("call-specific")]
        [Authorize(Roles = "Cashier,Admin,SuperAdmin")]
        [ProducesResponseType(typeof(CalledNumberDto), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> CallSpecific(int areaId, [FromBody] CallSpecificRequest request)
        {
            _logger.LogInformation("Received request to call specific queue number {TicketNumber} for Area {AreaId} from CashierStation {CashierStationId}.", request.TicketNumber, areaId, request.CashierStationId);
             if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid model state for CallSpecific request for Area {AreaId}: {@ModelState}", areaId, ModelState);
                return BadRequest(ModelState);
            }

            var result = await _queueService.CallSpecificAsync(areaId, request.CashierStationId, request.TicketNumber);
             if (!result.Success)
            {
                  if (result.Errors.Any(e => e.Contains("not found", StringComparison.OrdinalIgnoreCase)))
                 {
                      _logger.LogWarning("Area {AreaId} or CashierStation {CashierStationId} not found for CallSpecific: {Errors}", areaId, request.CashierStationId, string.Join("; ", result.Errors));
                      return NotFound(string.Join("; ", result.Errors));
                 }
                _logger.LogError("Failed to call specific queue number {TicketNumber} for Area {AreaId} from CashierStation {CashierStationId}: {Errors}", request.TicketNumber, areaId, request.CashierStationId, string.Join("; ", result.Errors));
                return BadRequest(string.Join("; ", result.Errors));
            }
            _logger.LogInformation("Successfully called specific queue number {TicketNumber} for Area {AreaId} from CashierStation {CashierStationId}.", result.Value.TicketNumber, areaId, request.CashierStationId);
            return Ok(result.Value);
        }

        // POST /api/areas/{areaId}/queue/reset
        [HttpPost("reset")]
        [Authorize(Roles = "Admin,SuperAdmin")] // Only admins can reset
        [ProducesResponseType(204)] // No Content on success
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> ResetQueue(int areaId, [FromBody] ResetQueueRequest? request)
        {
            int startingNumber = request?.StartingNumber ?? 1;
            _logger.LogInformation("Received request to reset queue for Area {AreaId} with starting number {StartingNumber}.", areaId, startingNumber);
            var result = await _queueService.ResetQueueAsync(areaId, startingNumber);
            if (!result.Success)
            {
                 if (result.Errors.Any(e => e.Contains("not found", StringComparison.OrdinalIgnoreCase)))
                 {
                      _logger.LogWarning("Area {AreaId} not found for ResetQueue: {Errors}", areaId, string.Join("; ", result.Errors));
                      return NotFound(string.Join("; ", result.Errors));
                 }
                _logger.LogError("Failed to reset queue for Area {AreaId}: {Errors}", areaId, string.Join("; ", result.Errors));
                return BadRequest(string.Join("; ", result.Errors));
            }
            _logger.LogInformation("Successfully reset queue for Area {AreaId} to starting number {StartingNumber}.", areaId, startingNumber);
            return NoContent(); // Success
        }

        // PUT /api/areas/{areaId}/queue/next-sequential-number
        [HttpPut("next-sequential-number")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        [ProducesResponseType(204)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> UpdateNextSequential(int areaId, [FromBody] UpdateNextSequentialRequest request)
        {
            _logger.LogInformation("Received request to update next sequential queue number for Area {AreaId} to {NewNextNumber}.", areaId, request.NewNextNumber);
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid model state for UpdateNextSequential request for Area {AreaId}: {@ModelState}", areaId, ModelState);
                return BadRequest(ModelState);
            }
            var result = await _queueService.UpdateNextSequentialNumberAsync(areaId, request.NewNextNumber);
             if (!result.Success)
            {
                 if (result.Errors.Any(e => e.Contains("not found", StringComparison.OrdinalIgnoreCase)))
                 {
                      _logger.LogWarning("Area {AreaId} not found for UpdateNextSequential: {Errors}", areaId, string.Join("; ", result.Errors));
                      return NotFound(string.Join("; ", result.Errors));
                 }
                _logger.LogError("Failed to update next sequential queue number for Area {AreaId} to {NewNextNumber}: {Errors}", areaId, request.NewNextNumber, string.Join("; ", result.Errors));
                return BadRequest(string.Join("; ", result.Errors));
            }
            _logger.LogInformation("Successfully updated next sequential queue number for Area {AreaId} to {NewNextNumber}.", areaId, request.NewNextNumber);
            return NoContent(); // Success
        }

        // POST /api/areas/{areaId}/queue/toggle
        [HttpPost("toggle")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        [ProducesResponseType(204)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> ToggleQueueSystem(int areaId, [FromBody] bool enable)
        {
            _logger.LogInformation("Received request to toggle queue system for Area {AreaId} to {Enable}.", areaId, enable);
            var result = await _queueService.ToggleQueueSystemAsync(areaId, enable);
            if (!result.Success)
            {
                _logger.LogError("Failed to toggle queue system for Area {AreaId} to {Enable}: {Errors}", areaId, enable, string.Join("; ", result.Errors));
                return BadRequest(result.Errors);
            }
            _logger.LogInformation("Successfully toggled queue system for Area {AreaId} to {Enable}.", areaId, enable);
            return Ok();
        }

        // POST /api/areas/{areaId}/queue/respeak-last-called
        [HttpPost("respeak-last-called")]
        [Authorize(Roles = "Cashier,Admin,SuperAdmin")]
        [ProducesResponseType(typeof(CalledNumberDto), 200)]
        [ProducesResponseType(400)] // e.g., station invalid, queue disabled, no number called yet
        [ProducesResponseType(404)] // Area or Station not found
        public async Task<IActionResult> RespeakLastCalled(int areaId, [FromBody] RespeakRequest request)
        {
            _logger.LogInformation("Received request to respeak last called number for Area {AreaId} from CashierStation {CashierStationId}.", areaId, request.CashierStationId);
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid model state for RespeakLastCalled request for Area {AreaId}: {@ModelState}", areaId, ModelState);
                return BadRequest(ModelState);
            }

            var result = await _queueService.RespeakLastCalledNumberAsync(areaId, request.CashierStationId);

            if (!result.Success)
            {
                if (result.Errors.Any(e => e.Contains("not found", StringComparison.OrdinalIgnoreCase)))
                {
                    _logger.LogWarning("Area {AreaId} or CashierStation {CashierStationId} not found for RespeakLastCalled: {Errors}", areaId, request.CashierStationId, string.Join("; ", result.Errors));
                    return NotFound(string.Join("; ", result.Errors));
                }
                // Specific check for "No number has been called yet"
                if (result.Errors.Any(e => e.Contains("No number has been called yet", StringComparison.OrdinalIgnoreCase)))
                {
                    _logger.LogInformation("No number has been called yet for Area {AreaId} from CashierStation {CashierStationId}.", areaId, request.CashierStationId);
                    return BadRequest(string.Join("; ", result.Errors));
                }
                _logger.LogError("Failed to respeak last called number for Area {AreaId} from CashierStation {CashierStationId}: {Errors}", areaId, request.CashierStationId, string.Join("; ", result.Errors));
                return BadRequest(string.Join("; ", result.Errors));
            }
            _logger.LogInformation("Successfully re-spoke last called number {TicketNumber} for Area {AreaId} from CashierStation {CashierStationId}.", result.Value.TicketNumber, areaId, request.CashierStationId);
            return Ok(result.Value);
        }

        // --- Request DTOs (defined inline for simplicity, move to DTOs folder if preferred) ---
        public class CallNextRequest
        {
            [Required] public int CashierStationId { get; set; }
        }

        public class CallSpecificRequest
        {
            [Required] public int CashierStationId { get; set; }
            [Required][Range(1, int.MaxValue)] public int TicketNumber { get; set; }
        }

         public class ResetQueueRequest
        {
            [Range(1, int.MaxValue)] public int? StartingNumber { get; set; }
        }

         public class UpdateNextSequentialRequest
        {
             [Required][Range(1, int.MaxValue)] public int NewNextNumber { get; set; }
        }

        public class RespeakRequest
        {
            [Required] public int CashierStationId { get; set; }
        }
    }
}
