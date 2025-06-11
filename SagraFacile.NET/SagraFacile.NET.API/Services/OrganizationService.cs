using Microsoft.EntityFrameworkCore;
using SagraFacile.NET.API.Data;
using SagraFacile.NET.API.DTOs; // Add DTO namespace
using SagraFacile.NET.API.Models;
using SagraFacile.NET.API.Services.Interfaces;
using System.Collections.Generic;
using System.Linq; // Add Linq for Select
using System.Text.RegularExpressions; // Added for slug generation
using System.Threading.Tasks;

namespace SagraFacile.NET.API.Services
{
    public class OrganizationService : IOrganizationService
    {
        private readonly ApplicationDbContext _context;

        public OrganizationService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<OrganizationDto>> GetAllOrganizationsAsync() // Update return type
        {
            return await _context.Organizations
                                 .Select(org => new OrganizationDto
                                 {
                                     Id = org.Id,
                                     Name = org.Name,
                                     Slug = org.Slug // Map Slug
                                 })
                                 .ToListAsync();
        }

        public async Task<Organization?> GetOrganizationByIdAsync(int id)
        {
            // FindAsync is not suitable here as we need the DTO
            var organization = await _context.Organizations.FindAsync(id);
            // Return the full Organization entity for now, controller can map if needed
            // Or create a GetOrganizationDtoByIdAsync method returning OrganizationDto
            return organization;
        }

        // New method to get by slug
        public async Task<OrganizationDto?> GetOrganizationBySlugAsync(string slug)
        {
            return await _context.Organizations
                                 .Where(o => o.Slug == slug)
                                 .Select(org => new OrganizationDto
                                 {
                                     Id = org.Id,
                                     Name = org.Name,
                                     Slug = org.Slug
                                 })
                                 .FirstOrDefaultAsync();
        }


        public async Task<Organization> CreateOrganizationAsync(Organization organization)
        {
            organization.Slug = GenerateSlug(organization.Name);
            // Consider adding logic to ensure slug uniqueness if GenerateSlug isn't perfect
            // or if names can be very similar. Could involve checking DB and appending a number.
            _context.Organizations.Add(organization);
            await _context.SaveChangesAsync();
            return organization; // The organization object now has the generated Id and Slug
        }

        public async Task<bool> UpdateOrganizationAsync(int id, Organization organization)
        {
            if (id != organization.Id)
            {
                return false; // Or throw an exception
            }

            var existingOrganization = await _context.Organizations.FindAsync(id);

            if (existingOrganization == null)
            {
                return false; // Not found
            }

            // Update properties from the incoming organization object
            if (existingOrganization.Name != organization.Name)
            {
                existingOrganization.Name = organization.Name;
                existingOrganization.Slug = GenerateSlug(organization.Name); // Regenerate slug if name changes
                // Add uniqueness check/handling if necessary
            }
            // Update other properties as needed

            // _context.Entry(organization).State = EntityState.Modified; // Keep removed

            try
            {
                await _context.SaveChangesAsync();
                return true;
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await OrganizationExistsAsync(id))
                {
                    return false; // Organization not found
                }
                else
                {
                    throw; // Re-throw the concurrency exception if needed
                }
            }
            catch (DbUpdateException)
            {
                // Handle other potential update errors if necessary
                return false;
            }
        }

        public async Task<bool> DeleteOrganizationAsync(int id)
        {
            var organization = await _context.Organizations.FindAsync(id);
            if (organization == null)
            {
                return false; // Not found
            }

            try
            {
                // Consider adding checks here if DeleteBehavior.Restrict isn't sufficient
                // e.g., check if organization.Areas.Any() before removing
                _context.Organizations.Remove(organization);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (DbUpdateException)
            {
                // Handle potential errors, e.g., foreign key constraints if Restrict wasn't set
                // Log the error
                return false;
            }
        }

        public async Task<bool> OrganizationExistsAsync(int id)
        {
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
