using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SagraFacile.NET.API.DTOs;
using SagraFacile.NET.API.Services.Interfaces;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SagraFacile.NET.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin,SuperAdmin")] // Restrict access to Admins and SuperAdmins
    public class PrintersController : ControllerBase
    {
        private readonly IPrinterService _printerService;
        private readonly ILogger<PrintersController> _logger;

        public PrintersController(IPrinterService printerService, ILogger<PrintersController> logger)
        {
            _printerService = printerService;
            _logger = logger;
        }

        // GET: api/Printers
        [HttpGet]
        public async Task<ActionResult<IEnumerable<PrinterDto>>> GetPrinters()
        {
            try
            {
                var printers = await _printerService.GetPrintersAsync();
                return Ok(printers);
            }
            catch (InvalidOperationException ex) // Catch specific context errors
            {
                _logger.LogWarning(ex, "Error retrieving printers due to missing context.");
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving printers.");
                return StatusCode(500, "Internal server error while retrieving printers.");
            }
        }

        // GET: api/Printers/5
        [HttpGet("{id}")]
        public async Task<ActionResult<PrinterDto>> GetPrinter(int id)
        {
            var printer = await _printerService.GetPrinterByIdAsync(id);

            if (printer == null)
            {
                return NotFound();
            }

            return Ok(printer);
        }

        // POST: api/Printers
        [HttpPost]
        public async Task<ActionResult<PrinterDto>> PostPrinter(PrinterUpsertDto printerDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var (createdPrinter, error) = await _printerService.CreatePrinterAsync(printerDto);

            if (error != null)
            {
                // Check for specific error types if needed, otherwise return BadRequest
                return BadRequest(new { message = error });
            }

            if (createdPrinter == null)
            {
                 _logger.LogError("Printer creation failed unexpectedly after passing service checks.");
                 return StatusCode(500, "Internal server error during printer creation.");
            }

            // Map to DTO before returning
            var resultDto = new PrinterDto
            {
                 Id = createdPrinter.Id,
                 Name = createdPrinter.Name,
                 Type = createdPrinter.Type,
                 ConnectionString = createdPrinter.ConnectionString,
                 WindowsPrinterName = createdPrinter.WindowsPrinterName,
                 IsEnabled = createdPrinter.IsEnabled,
                 OrganizationId = createdPrinter.OrganizationId
            };

            return CreatedAtAction(nameof(GetPrinter), new { id = createdPrinter.Id }, resultDto);
        }

        // PUT: api/Printers/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutPrinter(int id, PrinterUpsertDto printerDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var (success, error) = await _printerService.UpdatePrinterAsync(id, printerDto);

            if (!success)
            {
                if (error == "Printer not found." || error == "Printer not found (concurrency issue).")
                {
                    return NotFound(new { message = error });
                }
                // Other errors are likely bad requests or authorization issues
                return BadRequest(new { message = error });
            }

            return NoContent(); // Successful update
        }

        // DELETE: api/Printers/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePrinter(int id)
        {
            var (success, error) = await _printerService.DeletePrinterAsync(id);

            if (!success)
            {
                if (error == "Printer not found.")
                {
                    return NotFound(new { message = error });
                }
                // Other errors indicate dependencies or authorization issues
                return BadRequest(new { message = error });
            }

            return NoContent(); // Successful delete
        }

        // GET: api/Printers/config/{instanceGuid}
        // This endpoint is for the Windows Companion App to fetch its specific configuration
        [HttpGet("config/{instanceGuid}")]
        [AllowAnonymous] // Companion app might call this before full auth/registration
        public async Task<IActionResult> GetPrinterConfig(string instanceGuid)
        {
            if (string.IsNullOrWhiteSpace(instanceGuid))
            {
                return BadRequest("Instance GUID cannot be empty.");
            }

            _logger.LogInformation("Received request for printer config with GUID: {InstanceGuid}", instanceGuid);
            var config = await _printerService.GetPrinterConfigAsync(instanceGuid);

            if (config == null)
            {
                _logger.LogWarning("No configuration found for printer with GUID: {InstanceGuid}", instanceGuid);
                return NotFound(new { message = "Printer configuration not found or printer is disabled." });
            }

            // Return a simple object with the configuration
            return Ok(new { config.Value.PrintMode, config.Value.WindowsPrinterName });
        }
    }
}
