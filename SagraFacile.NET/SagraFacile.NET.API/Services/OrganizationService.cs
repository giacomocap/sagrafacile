using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SagraFacile.NET.API.Data;
using SagraFacile.NET.API.DTOs;
using SagraFacile.NET.API.Models;
using SagraFacile.NET.API.Models.Results;
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
        private readonly UserManager<User> _userManager;
        private readonly ILogger<OrganizationService> _logger;

        public OrganizationService(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor, UserManager<User> userManager, ILogger<OrganizationService> logger)
            : base(httpContextAccessor)
        {
            _context = context;
            _userManager = userManager;
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
                                     Slug = org.Slug,
                                     SubscriptionStatus = org.SubscriptionStatus
                                 })
                                 .ToListAsync();

            _logger.LogInformation("Retrieved {Count} organizations.", organizations.Count);
            return organizations;
        }

        public async Task<OrganizationDto?> GetOrganizationByIdAsync(Guid id)
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
                // Throwing an exception is better for security here, as it's a clear unauthorized action
                // rather than just "not found". The controller will catch this and return a 403 Forbidden.
                throw new UnauthorizedAccessException("User is not authorized to access this organization.");
            }

            _logger.LogInformation("Successfully retrieved and authorized organization {OrganizationId}.", id);
            
            // Map to DTO
            return new OrganizationDto
            {
                Id = organization.Id,
                Name = organization.Name,
                Slug = organization.Slug,
                SubscriptionStatus = organization.SubscriptionStatus
            };
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
                Slug = organization.Slug,
                SubscriptionStatus = organization.SubscriptionStatus
            };
        }


        public async Task<Organization> CreateOrganizationAsync(Organization organization)
        {
            _logger.LogInformation("Attempting to create organization: {OrganizationName}.", organization.Name);
            organization.Slug = await GenerateUniqueSlugAsync(organization.Name);
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
                existingOrganization.Slug = await GenerateUniqueSlugAsync(organization.Name); // Regenerate slug if name changes
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

        private async Task<string> GenerateUniqueSlugAsync(string phrase)
        {
            string baseSlug = GenerateSlug(phrase);
            string finalSlug = baseSlug;
            int counter = 1;

            while (await _context.Organizations.AnyAsync(o => o.Slug == finalSlug))
            {
                finalSlug = $"{baseSlug}-{counter}";
                counter++;
            }

            return finalSlug;
        }

        private static string GenerateSlug(string phrase)
        {
            if (string.IsNullOrWhiteSpace(phrase))
                return "n-a";

            string str = phrase.ToLowerInvariant();
            str = Regex.Replace(str, @"[^a-z0-9\s-]", ""); 
            str = Regex.Replace(str, @"\s+", " ").Trim(); 
            str = str.Length > 100 ? str.Substring(0, 100) : str;
            str = Regex.Replace(str, @"\s", "-"); 
            return str;
        }

        public async Task<ServiceResult<OrganizationDto>> ProvisionOrganizationAsync(OrganizationProvisionRequestDto provisionDto, string userId)
        {
            _logger.LogInformation("Attempting to provision organization '{OrganizationName}' for user {UserId}", provisionDto.OrganizationName, userId);

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                _logger.LogWarning("Provisioning failed: User with ID {UserId} not found.", userId);
                return ServiceResult<OrganizationDto>.Fail("User not found.");
            }

            if (user.OrganizationId.HasValue)
            {
                _logger.LogWarning("Provisioning failed: User {UserId} already belongs to organization {OrganizationId}.", userId, user.OrganizationId);
                return ServiceResult<OrganizationDto>.Fail("User already belongs to an organization.");
            }

            var newOrganization = new Organization
            {
                Name = provisionDto.OrganizationName,
                Slug = await GenerateUniqueSlugAsync(provisionDto.OrganizationName),
                SubscriptionStatus = "Trial", // Default to Trial for SaaS
                Id = Guid.NewGuid() 
            };

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                _context.Organizations.Add(newOrganization);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Organization '{OrganizationName}' created with ID {OrganizationId}.", newOrganization.Name, newOrganization.Id);

                user.OrganizationId = newOrganization.Id;
                var updateUserResult = await _userManager.UpdateAsync(user);
                if (!updateUserResult.Succeeded)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError("Failed to assign organization to user {UserId}. Errors: {Errors}", userId, string.Join(", ", updateUserResult.Errors.Select(e => e.Description)));
                    return ServiceResult<OrganizationDto>.Fail(updateUserResult.Errors.Select(e => e.Description));
                }
                _logger.LogInformation("Assigned user {UserId} to new organization {OrganizationId}.", userId, newOrganization.Id);

                var addToRoleResult = await _userManager.AddToRoleAsync(user, "Admin");
                if (!addToRoleResult.Succeeded)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError("Failed to assign 'Admin' role to user {UserId}. Errors: {Errors}", userId, string.Join(", ", addToRoleResult.Errors.Select(e => e.Description)));
                    return ServiceResult<OrganizationDto>.Fail(addToRoleResult.Errors.Select(e => e.Description));
                }
                _logger.LogInformation("Assigned 'Admin' role to user {UserId}.", userId);

                await transaction.CommitAsync();

                var organizationDto = new OrganizationDto
                {
                    Id = newOrganization.Id,
                    Name = newOrganization.Name,
                    Slug = newOrganization.Slug,
                    SubscriptionStatus = newOrganization.SubscriptionStatus
                };

                _logger.LogInformation("Successfully provisioned organization {OrganizationId} for user {UserId}.", newOrganization.Id, userId);
                return ServiceResult<OrganizationDto>.Ok(organizationDto);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "An unexpected error occurred during organization provisioning for user {UserId}.", userId);
                return ServiceResult<OrganizationDto>.Fail("An unexpected error occurred.");
            }
        }
    }
}
