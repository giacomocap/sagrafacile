using Microsoft.AspNetCore.Authorization; // Now used
using Microsoft.AspNetCore.Mvc;
using SagraFacile.NET.API.DTOs; // Add DTO namespace
using SagraFacile.NET.API.Models;
using SagraFacile.NET.API.Services.Interfaces;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SagraFacile.NET.API.Controllers
{
[Route("api/[controller]")]
[ApiController]
[Authorize(Roles = "SuperAdmin")] // Only SuperAdmins can manage organizations
public class OrganizationsController : ControllerBase
{
    private readonly IOrganizationService _organizationService;

        // Inject the service instead of the DbContext
        public OrganizationsController(IOrganizationService organizationService)
        {
            _organizationService = organizationService;
        }

        // GET: api/Organizations
        [HttpGet]
        // [AllowAnonymous] // Removed - Requires authentication now
        public async Task<ActionResult<IEnumerable<OrganizationDto>>> GetOrganizations() // Update return type
        {
            var organizations = await _organizationService.GetAllOrganizationsAsync();
            return Ok(organizations); // Use Ok() for successful GET requests returning data
        }

        // GET: api/Organizations/5
        [HttpGet("{id}")]
        // [AllowAnonymous] // Removed - Requires authentication now
        public async Task<ActionResult<Organization>> GetOrganization(int id)
        {
            var organization = await _organizationService.GetOrganizationByIdAsync(id);

            if (organization == null)
            {
                return NotFound($"Organization with ID {id} not found.");
            }

            return Ok(organization);
        }

        // POST: api/Organizations
        // Consider creating a DTO (Data Transfer Object) for input instead of using the model directly
        [HttpPost]
        public async Task<ActionResult<Organization>> PostOrganization([FromBody] Organization organization) // Use [FromBody]
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var createdOrganization = await _organizationService.CreateOrganizationAsync(organization);

            // Return 201 Created status with location header and the created resource
            return CreatedAtAction(nameof(GetOrganization), new { id = createdOrganization.Id }, createdOrganization);
        }

        // PUT: api/Organizations/5
        // Consider creating a DTO for input
        [HttpPut("{id}")]
        public async Task<IActionResult> PutOrganization(int id, [FromBody] Organization organization) // Use [FromBody]
        {
            if (id != organization.Id)
            {
                return BadRequest("ID mismatch between route parameter and request body.");
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var updateResult = await _organizationService.UpdateOrganizationAsync(id, organization);

            if (!updateResult)
            {
                // Could be NotFound or another update issue (concurrency handled in service)
                // Check if it exists to return specific error
                if (!await _organizationService.OrganizationExistsAsync(id))
                {
                    return NotFound($"Organization with ID {id} not found.");
                }
                else
                {
                    // Consider logging the specific update issue from the service if possible
                    return StatusCode(500, "An error occurred while updating the organization.");
                }
            }

            return NoContent(); // Standard response for successful PUT
        }

        // DELETE: api/Organizations/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteOrganization(int id)
        {
            var deleteResult = await _organizationService.DeleteOrganizationAsync(id);

            if (!deleteResult)
            {
                 if (!await _organizationService.OrganizationExistsAsync(id))
                {
                    return NotFound($"Organization with ID {id} not found.");
                }
                else
                {
                    // Could be due to constraints (handled in service) or other issues
                    return StatusCode(500, "An error occurred while deleting the organization. It might be in use.");
                }
            }

            return NoContent(); // Standard response for successful DELETE
        }
    }
}
