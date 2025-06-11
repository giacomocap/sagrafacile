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
            var result = await _queueService.GetQueueStateAsync(areaId);
            if (!result.Success)
            {
                 // Distinguish between not found/auth error (404) and other errors (400)
                 // Assuming Fail messages indicate the type
                 if (result.Errors.Any(e => e.Contains("not found", StringComparison.OrdinalIgnoreCase) || e.Contains("not authorized", StringComparison.OrdinalIgnoreCase)))
                 {
                      return NotFound(string.Join("; ", result.Errors));
                 }
                 return BadRequest(string.Join("; ", result.Errors));
            }
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
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = await _queueService.CallNextAsync(areaId, request.CashierStationId);

            if (!result.Success)
            {
                 if (result.Errors.Any(e => e.Contains("not found", StringComparison.OrdinalIgnoreCase)))
                 {
                      return NotFound(string.Join("; ", result.Errors));
                 }
                return BadRequest(string.Join("; ", result.Errors));
            }
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
             if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = await _queueService.CallSpecificAsync(areaId, request.CashierStationId, request.TicketNumber);
             if (!result.Success)
            {
                  if (result.Errors.Any(e => e.Contains("not found", StringComparison.OrdinalIgnoreCase)))
                 {
                      return NotFound(string.Join("; ", result.Errors));
                 }
                return BadRequest(string.Join("; ", result.Errors));
            }
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
            var result = await _queueService.ResetQueueAsync(areaId, startingNumber);
            if (!result.Success)
            {
                 if (result.Errors.Any(e => e.Contains("not found", StringComparison.OrdinalIgnoreCase)))
                 {
                      return NotFound(string.Join("; ", result.Errors));
                 }
                return BadRequest(string.Join("; ", result.Errors));
            }
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
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            var result = await _queueService.UpdateNextSequentialNumberAsync(areaId, request.NewNextNumber);
             if (!result.Success)
            {
                 if (result.Errors.Any(e => e.Contains("not found", StringComparison.OrdinalIgnoreCase)))
                 {
                      return NotFound(string.Join("; ", result.Errors));
                 }
                return BadRequest(string.Join("; ", result.Errors));
            }
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
            var result = await _queueService.ToggleQueueSystemAsync(areaId, enable);
            if (!result.Success) return BadRequest(result.Errors);
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
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = await _queueService.RespeakLastCalledNumberAsync(areaId, request.CashierStationId);

            if (!result.Success)
            {
                if (result.Errors.Any(e => e.Contains("not found", StringComparison.OrdinalIgnoreCase)))
                {
                    return NotFound(string.Join("; ", result.Errors));
                }
                // Specific check for "No number has been called yet"
                if (result.Errors.Any(e => e.Contains("No number has been called yet", StringComparison.OrdinalIgnoreCase)))
                {
                    // Return a 200 OK with a specific message or a custom DTO, 
                    // or a 400 with a clear error. For now, let's use 400.
                    return BadRequest(string.Join("; ", result.Errors));
                }
                return BadRequest(string.Join("; ", result.Errors));
            }
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
