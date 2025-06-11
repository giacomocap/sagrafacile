using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SagraFacile.NET.API.DTOs;
using SagraFacile.NET.API.Services.Interfaces;
using System.Threading.Tasks;

namespace SagraFacile.NET.API.Controllers
{
    [Route("api/printers/{printerId}/assignments")] // Route specific to a printer
    [ApiController]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public class PrinterAssignmentsController : ControllerBase
    {
        private readonly IPrinterAssignmentService _assignmentService;
        private readonly ILogger<PrinterAssignmentsController> _logger;

        public PrinterAssignmentsController(IPrinterAssignmentService assignmentService, ILogger<PrinterAssignmentsController> logger)
        {
            _assignmentService = assignmentService;
            _logger = logger;
        }

        // GET: api/printers/{printerId}/assignments
        /// <summary>
        /// Gets the current menu category assignments for a specific printer.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAssignments(int printerId, int areaId)
        {
            if (areaId <= 0)
            {
                return BadRequest("Valid AreaId query parameter is required.");
            }
            // Service layer should handle authorization (printer belongs to org)
            var assignments = await _assignmentService.GetAssignmentsForPrinterAsync(printerId, areaId);
            // Consider mapping to a simpler DTO if MenuCategory details are not needed or too verbose
            return Ok(assignments);
        }

        // POST: api/printers/{printerId}/assignments  (or PUT might be more appropriate)
        // Using POST to handle setting the complete list, replacing existing.
        // A PUT on /api/printers/{printerId}/assignments could also work.
        /// <summary>
        /// Sets the complete list of menu category assignments for a specific printer.
        /// Replaces all existing assignments for the printer with the provided list.
        /// </summary>
        [HttpPost] // Or [HttpPut]
        public async Task<IActionResult> SetAssignments(int printerId, [FromQuery] int areaId, SetPrinterAssignmentsDto dto)
        {
            if (areaId <= 0)
            {
                return BadRequest("Valid AreaId query parameter is required.");
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var (success, error) = await _assignmentService.SetAssignmentsForPrinterAsync(printerId, areaId, dto.CategoryIds);

            if (!success)
            {
                if (error == "Printer not found.")
                {
                    return NotFound(new { message = error });
                }
                // Handle other specific errors like invalid category IDs or auth issues
                _logger.LogWarning("Failed to set assignments for printer {PrinterId}: {Error}", printerId, error);
                return BadRequest(new { message = error });
            }

            return NoContent(); // Indicates success with no content to return
        }

        // Maybe add individual assignment management later if needed (e.g., POST /api/printers/{printerId}/assignments/{categoryId})
    }
}
