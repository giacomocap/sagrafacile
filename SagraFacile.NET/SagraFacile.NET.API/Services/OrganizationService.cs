using Microsoft.EntityFrameworkCore;
using SagraFacile.NET.API.Data;
using SagraFacile.NET.API.DTOs;
using SagraFacile.NET.API.Models;
using SagraFacile.NET.API.Services.Interfaces;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http; // Required for IHttpContextAccessor

namespace SagraFacile.NET.API.Services
{
    public class OrganizationService : BaseService, IOrganizationService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<OrganizationService> _logger;

        public OrganizationService(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor, ILogger<OrganizationService> logger)
            : base(httpContextAccessor)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IEnumerable<OrganizationDto>> GetAllOrganizationsAsync()
        {
            _logger.LogInformation("Fetching organizations based on user context.");
            var (userOrgId, isSuperAdmin) = GetUserContext();

            var query = _context.Organizations.AsQueryable();

            if (!isSuperAdmin)
            {
                if (!userOrgId.HasValue)
                {
                    _logger.LogWarning("Non-SuperAdmin user does not have an organization ID in their claims. Returning empty list.");
                    return new List<OrganizationDto>();
                }
                _logger.LogInformation("User is not SuperAdmin. Filtering organizations for OrganizationId: {OrganizationId}", userOrgId.Value);
                query = query.Where(org => org.Id == userOrgId.Value);
            }
            else
            {
                _logger.LogInformation("User is SuperAdmin. Fetching all organizations.");
            }

            var organizations = await query
                                 .Select(org => new OrganizationDto
                                 {
                                     Id = org.Id,
                                     Name = org.Name,
                                     Slug = org.Slug
                                 })
                                 .ToListAsync();

            _logger.LogInformation("Retrieved {Count} organizations.", organizations.Count);
            return organizations;
        }

        public async Task<Organization?> GetOrganizationByIdAsync(Guid id)
        {
            _logger.LogInformation("Fetching organization by ID: {OrganizationId}.", id);
            var (userOrgId, isSuperAdmin) = GetUserContext();

            var organization = await _context.Organizations.FindAsync(id);

            if (organization == null)
            {
                _logger.LogWarning("Organization with ID {OrganizationId} not found.", id);
                return null;
            }

            // Authorization check
            if (!isSuperAdmin && organization.Id != userOrgId)
            {
                _logger.LogWarning("Unauthorized access attempt: User from Org {UserOrgId} tried to access Org {OrganizationId}.", userOrgId, id);
                return null; // Return null to signify not found or not authorized
            }

            _logger.LogInformation("Successfully retrieved and authorized organization {OrganizationId}.", id);
            return organization;
        }


        // New method to get by slug
        public async Task<OrganizationDto?> GetOrganizationBySlugAsync(string slug)
        {
            _logger.LogInformation("Fetching organization by slug: {OrganizationSlug}.", slug);
            var (userOrgId, isSuperAdmin) = GetUserContext();

            var organization = await _context.Organizations
                                 .Where(o => o.Slug == slug)
                                 .FirstOrDefaultAsync();

            if (organization == null)
            {
                _logger.LogWarning("Organization with slug '{OrganizationSlug}' not found.", slug);
                return null;
            }

            // Authorization check
            if (!isSuperAdmin && organization.Id != userOrgId)
            {
                _logger.LogWarning("Unauthorized access attempt: User from Org {UserOrgId} tried to access Org {OrganizationId} via slug '{OrganizationSlug}'.", userOrgId, organization.Id, slug);
                return null;
            }

            _logger.LogInformation("Successfully retrieved and authorized organization with slug '{OrganizationSlug}'.", slug);

            // Map to DTO
            return new OrganizationDto
            {
                Id = organization.Id,
                Name = organization.Name,
                Slug = organization.Slug
            };
        }


        public async Task<Organization> CreateOrganizationAsync(Organization organization)
        {
            _logger.LogInformation("Attempting to create organization: {OrganizationName}.", organization.Name);
            organization.Slug = GenerateSlug(organization.Name);
            // Consider adding logic to ensure slug uniqueness if GenerateSlug isn't perfect
            // or if names can be very similar. Could involve checking DB and appending a number.
            _context.Organizations.Add(organization);
            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Organization '{OrganizationName}' (ID: {OrganizationId}) created successfully with slug '{OrganizationSlug}'.", organization.Name, organization.Id, organization.Slug);
                return organization; // The organization object now has the generated Id and Slug
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Error creating organization '{OrganizationName}'.", organization.Name);
                throw; // Re-throw to be handled by controller
            }
        }

        public async Task<bool> UpdateOrganizationAsync(Guid id, Organization organization)
        {
            _logger.LogInformation("Attempting to update organization ID: {OrganizationId}.", id);
            if (id != organization.Id)
            {
                _logger.LogWarning("Update organization failed for ID {OrganizationId}: ID mismatch between route parameter and request body.", id);
                return false; // Or throw an exception
            }

            var existingOrganization = await _context.Organizations.FindAsync(id);

            if (existingOrganization == null)
            {
                _logger.LogWarning("Update organization failed: Organization with ID {OrganizationId} not found.", id);
                return false; // Not found
            }

            // Update properties from the incoming organization object
            if (existingOrganization.Name != organization.Name)
            {
                _logger.LogInformation("Organization {OrganizationId} name changed from '{OldName}' to '{NewName}'. Regenerating slug.", id, existingOrganization.Name, organization.Name);
                existingOrganization.Name = organization.Name;
                existingOrganization.Slug = GenerateSlug(organization.Name); // Regenerate slug if name changes
                // Add uniqueness check/handling if necessary
            }
            // Update other properties as needed

            // _context.Entry(organization).State = EntityState.Modified; // Keep removed

            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Organization {OrganizationId} updated successfully.", id);
                return true;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(ex, "Concurrency exception during update for organization ID {OrganizationId}.", id);
                if (!await OrganizationExistsAsync(id))
                {
                    return false; // Organization not found
                }
                else
                {
                    throw; // Re-throw the concurrency exception if needed
                }
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Error updating organization with ID {OrganizationId}.", id);
                return false;
            }
        }

        public async Task<bool> DeleteOrganizationAsync(Guid id)
        {
            _logger.LogInformation("Attempting to delete organization ID: {OrganizationId}.", id);
            var organization = await _context.Organizations.FindAsync(id);
            if (organization == null)
            {
                _logger.LogWarning("Delete organization failed: Organization with ID {OrganizationId} not found.", id);
                return false; // Not found
            }

            try
            {
                // Consider adding checks here if DeleteBehavior.Restrict isn't sufficient
                // e.g., check if organization.Areas.Any() before removing
                _context.Organizations.Remove(organization);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Organization {OrganizationId} deleted successfully.", id);
                return true;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Error deleting organization with ID {OrganizationId}. It might be in use.", id);
                return false;
            }
        }

        public async Task<bool> OrganizationExistsAsync(Guid id)
        {
            _logger.LogDebug("Checking if organization ID {OrganizationId} exists.", id);
            return await _context.Organizations.AnyAsync(e => e.Id == id);
        }

        // Simple slug generation helper
        private static string GenerateSlug(string phrase)
        {
            string str = phrase.ToLowerInvariant();
            // invalid chars           \s+
            str = Regex.Replace(str, @"[^a-z0-9\s-]", ""); // remove invalid chars
            str = Regex.Replace(str, @"\s+", " ").Trim(); // convert multiple spaces into one space
            str = str.Substring(0, str.Length <= 100 ? str.Length : 100).Trim(); // cut and trim
            str = Regex.Replace(str, @"\s", "-"); // replace spaces with hyphens
            return str;
        }
    }
}
