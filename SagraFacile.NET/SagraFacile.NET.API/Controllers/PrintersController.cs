using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SagraFacile.NET.API.DTOs;
using SagraFacile.NET.API.Services.Interfaces;
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
        [ProducesResponseType(typeof(IEnumerable<PrinterDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<PrinterDto>>> GetPrinters()
        {
            _logger.LogInformation("Received request to get all printers.");
            try
            {
                var printers = await _printerService.GetPrintersAsync();
                _logger.LogInformation("Successfully retrieved {PrinterCount} printers.", printers.Count());
                return Ok(printers);
            }
            catch (InvalidOperationException ex) // Catch specific context errors
            {
                _logger.LogWarning(ex, "Error retrieving printers due to missing context.");
                return Unauthorized(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized access attempt in GetPrinters.");
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving printers.");
                return StatusCode(StatusCodes.Status500InternalServerError, "Internal server error while retrieving printers.");
            }
        }

        // GET: api/Printers/5
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(PrinterDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<PrinterDto>> GetPrinter(int id)
        {
            _logger.LogInformation("Received request to get printer by ID: {PrinterId}.", id);
            try
            {
                var printer = await _printerService.GetPrinterByIdAsync(id);

                if (printer == null)
                {
                    _logger.LogWarning("Printer with ID {PrinterId} not found or access denied.", id);
                    return NotFound();
                }

                _logger.LogInformation("Successfully retrieved printer {PrinterId}.", id);
                return Ok(printer);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized access attempt during GetPrinter for {PrinterId}.", id);
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while getting printer {PrinterId}.", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        // POST: api/Printers
        [HttpPost]
        [ProducesResponseType(typeof(PrinterDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<PrinterDto>> PostPrinter(PrinterUpsertDto printerDto)
        {
            _logger.LogInformation("Received request to create printer: {PrinterName}.", printerDto.Name);
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Create printer request for {PrinterName} failed due to invalid model state.", printerDto.Name);
                return BadRequest(ModelState);
            }

            try
            {
                var (createdPrinter, error) = await _printerService.CreatePrinterAsync(printerDto);

                if (error != null)
                {
                    _logger.LogWarning("Printer creation failed for {PrinterName}: {Error}", printerDto.Name, error);
                    return BadRequest(new { message = error });
                }

                if (createdPrinter == null)
                {
                    _logger.LogError("Printer creation failed unexpectedly after passing service checks for {PrinterName}.", printerDto.Name);
                    return StatusCode(StatusCodes.Status500InternalServerError, "Internal server error during printer creation.");
                }

                // Map to DTO before returning
                var resultDto = new PrinterDto
                {
                    Id = createdPrinter.Id,
                    Name = createdPrinter.Name,
                    Type = createdPrinter.Type,
                    ConnectionString = createdPrinter.ConnectionString,
                    IsEnabled = createdPrinter.IsEnabled,
                    OrganizationId = createdPrinter.OrganizationId,
                    PrintMode = createdPrinter.PrintMode
                };

                _logger.LogInformation("Printer '{PrinterName}' (ID: {PrinterId}) created successfully.", createdPrinter.Name, createdPrinter.Id);
                return CreatedAtAction(nameof(GetPrinter), new { id = createdPrinter.Id }, resultDto);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized access attempt during PostPrinter for {PrinterName}.", printerDto.Name);
                return Forbid();
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Invalid operation in PostPrinter for {PrinterName}, possibly missing user context.", printerDto.Name);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving user context.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while creating the printer {PrinterName}.", printerDto.Name);
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        // PUT: api/Printers/5
        [HttpPut("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> PutPrinter(int id, PrinterUpsertDto printerDto)
        {
            _logger.LogInformation("Received request to update printer {PrinterId}.", id);
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Update printer request for {PrinterId} failed due to invalid model state.", id);
                return BadRequest(ModelState);
            }

            try
            {
                var (success, error) = await _printerService.UpdatePrinterAsync(id, printerDto);

                if (!success)
                {
                    if (error == "Printer not found." || error == "Printer not found (concurrency issue).")
                    {
                        _logger.LogWarning("Update printer failed for ID {PrinterId}: {Error}", id, error);
                        return NotFound(new { message = error });
                    }
                    _logger.LogWarning("Update printer failed for ID {PrinterId}: {Error}", id, error);
                    return BadRequest(new { message = error });
                }

                _logger.LogInformation("Printer {PrinterId} updated successfully.", id);
                return NoContent(); // Successful update
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized access attempt during PutPrinter for {PrinterId}.", id);
                return Forbid();
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Invalid operation in PutPrinter for {PrinterId}, possibly missing user context.", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving user context.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while updating printer {PrinterId}.", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        // DELETE: api/Printers/5
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeletePrinter(int id)
        {
            _logger.LogInformation("Received request to delete printer {PrinterId}.", id);
            try
            {
                var (success, error) = await _printerService.DeletePrinterAsync(id);

                if (!success)
                {
                    if (error == "Printer not found.")
                    {
                        _logger.LogWarning("Delete printer failed for ID {PrinterId}: {Error}", id, error);
                        return NotFound(new { message = error });
                    }
                    _logger.LogWarning("Delete printer failed for ID {PrinterId}: {Error}", id, error);
                    return BadRequest(new { message = error });
                }

                _logger.LogInformation("Printer {PrinterId} deleted successfully.", id);
                return NoContent(); // Successful delete
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized access attempt during DeletePrinter for {PrinterId}.", id);
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while deleting printer {PrinterId}.", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        // GET: api/Printers/config/{instanceGuid}
        // This endpoint is for the Windows Companion App to fetch its specific configuration
        [HttpGet("config/{instanceGuid}")]
        [AllowAnonymous] // Companion app might call this before full auth/registration
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetPrinterConfig(string instanceGuid)
        {
            _logger.LogInformation("Received request for printer config with GUID: {InstanceGuid}", instanceGuid);
            if (string.IsNullOrWhiteSpace(instanceGuid))
            {
                _logger.LogWarning("GetPrinterConfig failed: Instance GUID cannot be empty.");
                return BadRequest("Instance GUID cannot be empty.");
            }

            try
            {
                var config = await _printerService.GetPrinterConfigAsync(instanceGuid);

                if (config == null)
                {
                    _logger.LogWarning("No configuration found for printer with GUID: {InstanceGuid}", instanceGuid);
                    return NotFound(new { message = "Printer configuration not found or printer is disabled." });
                }

                // Return a simple object with the configuration
                return Ok(new { PrintMode = config.Value });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while getting printer config for GUID: {InstanceGuid}.", instanceGuid);
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        // POST: api/Printers/5/test-print
        [HttpPost("{id}/test-print")]
        [Authorize(Roles = "Admin,SuperAdmin")] // Ensure only authorized users can trigger this
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> TestPrint(int id)
        {
            _logger.LogInformation("Received request to test print for printer ID: {PrinterId}", id);

            try
            {
                var (success, error) = await _printerService.SendTestPrintAsync(id);

                if (!success)
                {
                    if (error == "Printer not found.")
                    {
                        _logger.LogWarning("Test print failed for printer ID {PrinterId}: Printer not found.", id);
                        return NotFound(new { message = error });
                    }
                    if (error == "User not authorized for this printer.")
                    {
                        _logger.LogWarning("Test print failed for printer ID {PrinterId}: User not authorized.", id);
                        return Forbid();
                    }
                    if (error == "Printer is disabled.")
                    {
                        _logger.LogWarning("Test print failed for printer ID {PrinterId}: Printer is disabled.", id);
                        return BadRequest(new { message = error });
                    }
                    _logger.LogError("Test print failed for printer ID {PrinterId}: {Error}", id, error);
                    return StatusCode(StatusCodes.Status500InternalServerError, new { message = $"Test print failed: {error}" });
                }

                _logger.LogInformation("Test print successfully sent to printer ID: {PrinterId}", id);
                return Ok(new { message = "Test print sent successfully." });
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized access attempt during TestPrint for {PrinterId}.", id);
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while sending test print for printer {PrinterId}.", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }
    }
}
