using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SagraFacile.NET.API.DTOs;
using SagraFacile.NET.API.Services.Interfaces;

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
            _logger.LogInformation("Received request to get printer assignments for PrinterId: {PrinterId}, AreaId: {AreaId}", printerId, areaId);
            if (areaId <= 0)
            {
                _logger.LogWarning("Bad request: Invalid AreaId {AreaId} provided for getting printer assignments for PrinterId: {PrinterId}", areaId, printerId);
                return BadRequest("Valid AreaId query parameter is required.");
            }
            try
            {
                // Service layer should handle authorization (printer belongs to org)
                var assignments = await _assignmentService.GetAssignmentsForPrinterAsync(printerId, areaId);
                _logger.LogInformation("Successfully retrieved {Count} printer assignments for PrinterId: {PrinterId}, AreaId: {AreaId}", ((List<PrinterCategoryAssignmentDto>)assignments).Count, printerId, areaId);
                // Consider mapping to a simpler DTO if MenuCategory details are not needed or too verbose
                return Ok(assignments);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized access attempt to get printer assignments for PrinterId: {PrinterId}, AreaId: {AreaId}", printerId, areaId);
                return Forbid();
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogWarning(ex, "Resource not found during get printer assignments for PrinterId: {PrinterId}, AreaId: {AreaId}. Error: {Error}", printerId, areaId, ex.Message);
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while retrieving printer assignments for PrinterId: {PrinterId}, AreaId: {AreaId}", printerId, areaId);
                return StatusCode(500, "An error occurred while retrieving printer assignments.");
            }
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
            _logger.LogInformation("Received request to set printer assignments for PrinterId: {PrinterId}, AreaId: {AreaId} with {CategoryCount} categories.", printerId, areaId, dto.CategoryIds.Count()); // Changed to .Count()
            if (areaId <= 0)
            {
                _logger.LogWarning("Bad request: Invalid AreaId {AreaId} provided for setting printer assignments for PrinterId: {PrinterId}", areaId, printerId);
                return BadRequest("Valid AreaId query parameter is required.");
            }

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid model state for setting printer assignments for PrinterId: {PrinterId}. Errors: {@Errors}", printerId, ModelState);
                return BadRequest(ModelState);
            }

            try
            {
                var (success, error) = await _assignmentService.SetAssignmentsForPrinterAsync(printerId, areaId, dto.CategoryIds);

                if (!success)
                {
                    _logger.LogWarning("Failed to set assignments for printer {PrinterId}, AreaId: {AreaId}. Error: {Error}", printerId, areaId, error);
                    if (error == "Printer not found.")
                    {
                        return NotFound(new { message = error });
                    }
                    // Handle other specific errors like invalid category IDs or auth issues
                    return BadRequest(new { message = error });
                }

                _logger.LogInformation("Successfully set printer assignments for PrinterId: {PrinterId}, AreaId: {AreaId}", printerId, areaId);
                return NoContent(); // Indicates success with no content to return
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized access attempt to set printer assignments for PrinterId: {PrinterId}, AreaId: {AreaId}", printerId, areaId);
                return Forbid();
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogWarning(ex, "Resource not found during set printer assignments for PrinterId: {PrinterId}, AreaId: {AreaId}. Error: {Error}", printerId, areaId, ex.Message);
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while setting printer assignments for PrinterId: {PrinterId}, AreaId: {AreaId}", printerId, areaId);
                return StatusCode(500, "An error occurred while setting printer assignments.");
            }
        }

        // Maybe add individual assignment management later if needed (e.g., POST /api/printers/{printerId}/assignments/{categoryId})
    }
}
