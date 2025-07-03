using Microsoft.AspNetCore.Authorization; // Now used
using Microsoft.AspNetCore.Mvc;
using SagraFacile.NET.API.DTOs; // Add DTO namespace
using SagraFacile.NET.API.Models;
using SagraFacile.NET.API.Services.Interfaces;

namespace SagraFacile.NET.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] // Require authentication for all actions
    public class OrganizationsController : ControllerBase
    {
        private readonly IOrganizationService _organizationService;
        private readonly ILogger<OrganizationsController> _logger; // Added for logging

        // Inject the service instead of the DbContext
        public OrganizationsController(IOrganizationService organizationService, ILogger<OrganizationsController> logger)
        {
            _organizationService = organizationService;
            _logger = logger;
        }

        // GET: api/Organizations
        [HttpGet]
        // Allow all authenticated users to get the list. The service layer will filter.
        [ProducesResponseType(typeof(IEnumerable<OrganizationDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<OrganizationDto>>> GetOrganizations() // Update return type
        {
            _logger.LogInformation("Received request to get all organizations.");
            try
            {
                var organizations = await _organizationService.GetAllOrganizationsAsync();
                _logger.LogInformation("Successfully retrieved {OrganizationCount} organizations.", organizations.Count());
                return Ok(organizations); // Use Ok() for successful GET requests returning data
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while getting organizations.");
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        // GET: api/Organizations/5
        [HttpGet("{id}")]
        // [AllowAnonymous] // Removed - Requires authentication now
        [ProducesResponseType(typeof(OrganizationDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<OrganizationDto>> GetOrganization(Guid id)
        {
            _logger.LogInformation("Received request to get organization by ID: {OrganizationId}.", id);
            try
            {
                var organizationDto = await _organizationService.GetOrganizationByIdAsync(id);

                if (organizationDto == null)
                {
                    _logger.LogWarning("Organization with ID {OrganizationId} not found or access denied.", id);
                    return NotFound($"Organization with ID {id} not found or access denied.");
                }

                _logger.LogInformation("Successfully retrieved organization {OrganizationId}.", id);
                return Ok(organizationDto);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized access attempt for organization {OrganizationId}.", id);
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while getting organization {OrganizationId}.", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        // POST: api/Organizations
        // Consider creating a DTO (Data Transfer Object) for input instead of using the model directly
        [HttpPost]
        [Authorize(Roles = "SuperAdmin")] // Only SuperAdmins can update
        [ProducesResponseType(typeof(Organization), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<Organization>> PostOrganization([FromBody] Organization organization) // Use [FromBody]
        {
            _logger.LogInformation("Received request to create organization: {OrganizationName}.", organization.Name);
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Create organization request for {OrganizationName} failed due to invalid model state.", organization.Name);
                return BadRequest(ModelState);
            }

            try
            {
                var createdOrganization = await _organizationService.CreateOrganizationAsync(organization);
                _logger.LogInformation("Organization '{OrganizationName}' (ID: {OrganizationId}) created successfully.", createdOrganization.Name, createdOrganization.Id);
                // Return 201 Created status with location header and the created resource
                return CreatedAtAction(nameof(GetOrganization), new { id = createdOrganization.Id }, createdOrganization); // id is already Guid here
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while creating the organization {OrganizationName}.", organization.Name);
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        // PUT: api/Organizations/5
        // Consider creating a DTO for input
        [HttpPut("{id}")]
        [Authorize(Roles = "SuperAdmin")] // Only SuperAdmins can update
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> PutOrganization(Guid id, [FromBody] Organization organization) // Use [FromBody]
        {
            _logger.LogInformation("Received request to update organization {OrganizationId}.", id);
            if (id != organization.Id)
            {
                _logger.LogWarning("Update organization failed for ID {OrganizationId}: ID mismatch between route parameter and request body.", id);
                return BadRequest("ID mismatch between route parameter and request body.");
            }

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Update organization request for {OrganizationId} failed due to invalid model state.", id);
                return BadRequest(ModelState);
            }

            try
            {
                var updateResult = await _organizationService.UpdateOrganizationAsync(id, organization);

                if (!updateResult)
                {
                    if (!await _organizationService.OrganizationExistsAsync(id))
                    {
                        _logger.LogWarning("Update organization failed: Organization with ID {OrganizationId} not found.", id);
                        return NotFound($"Organization with ID {id} not found.");
                    }
                    else
                    {
                        _logger.LogError("An unknown error occurred while updating organization {OrganizationId}.", id);
                        return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while updating the organization.");
                    }
                }
                _logger.LogInformation("Organization {OrganizationId} updated successfully.", id);
                return NoContent(); // Standard response for successful PUT
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while updating organization {OrganizationId}.", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }

        // DELETE: api/Organizations/5
        [HttpDelete("{id}")]
        [Authorize(Roles = "SuperAdmin")] // Only SuperAdmins can update
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteOrganization(Guid id)
        {
            _logger.LogInformation("Received request to delete organization {OrganizationId}.", id);
            try
            {
                var deleteResult = await _organizationService.DeleteOrganizationAsync(id);

                if (!deleteResult)
                {
                    if (!await _organizationService.OrganizationExistsAsync(id))
                    {
                        _logger.LogWarning("Delete organization failed: Organization with ID {OrganizationId} not found.", id);
                        return NotFound($"Organization with ID {id} not found.");
                    }
                    else
                    {
                        _logger.LogError("An unknown error occurred while deleting organization {OrganizationId}. It might be in use.", id);
                        return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while deleting the organization. It might be in use.");
                    }
                }
                _logger.LogInformation("Organization {OrganizationId} deleted successfully.", id);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while deleting organization {OrganizationId}.", id);
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }
    }
}
