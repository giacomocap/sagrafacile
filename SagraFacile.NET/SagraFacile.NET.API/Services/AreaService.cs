using Microsoft.EntityFrameworkCore;
using SagraFacile.NET.API.Data;
using Microsoft.AspNetCore.Http; // Required for IHttpContextAccessor
using SagraFacile.NET.API.Models;
using SagraFacile.NET.API.DTOs; // Added for AreaDto
using SagraFacile.NET.API.Services.Interfaces;
using System; // Required for ArgumentNullException
using System.Collections.Generic;
using System.Linq; // Required for Where clause
using System.Security.Claims; // Required for ClaimsPrincipal
using System.Text.RegularExpressions; // Added for slug generation
using System.Threading.Tasks;

namespace SagraFacile.NET.API.Services
{
    // Inherit from BaseService
    public class AreaService : BaseService, IAreaService
    {
        private readonly ApplicationDbContext _context;
        // IHttpContextAccessor is now inherited from BaseService

        public AreaService(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor)
            : base(httpContextAccessor) // Call base constructor
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        // GetUserContext helper is now inherited from BaseService

        // Removed optional organizationId parameter - it's derived from user context now
        public async Task<IEnumerable<AreaDto>> GetAllAreasAsync() // Return AreaDto
        {
            var (userOrgId, isSuperAdmin) = GetUserContext();

            var query = _context.Areas.AsQueryable();

            // If a specific organization context is selected (even by SuperAdmin), filter by it.
            if (userOrgId.HasValue)
            {
                query = query.Where(a => a.OrganizationId == userOrgId.Value);
            }
            // If it's a SuperAdmin AND no specific organization is selected (userOrgId is null),
            // then they see all areas. Otherwise (non-SuperAdmin), the check above already filtered.
            else if (!isSuperAdmin) // This case handles non-SuperAdmin when userOrgId is somehow null (shouldn't happen)
            {
                // This should not happen based on GetUserContext logic, but defensively check
                throw new InvalidOperationException("User organization context is missing for non-SuperAdmin.");
            }
            // Implicit else: isSuperAdmin is true AND userOrgId is null -> No filtering applied, show all.

            var areas = await query.ToListAsync();

            // Map to DTOs
            return areas.Select(area => new AreaDto
            {
                Id = area.Id,
                Name = area.Name,
                Slug = area.Slug, // Map Slug
                OrganizationId = area.OrganizationId,
                EnableCompletionConfirmation = area.EnableCompletionConfirmation,
                EnableKds = area.EnableKds,
                EnableWaiterConfirmation = area.EnableWaiterConfirmation,
                ReceiptPrinterId = area.ReceiptPrinterId, // Map ReceiptPrinterId
                PrintComandasAtCashier = area.PrintComandasAtCashier, // Map PrintComandasAtCashier
                EnableQueueSystem = area.EnableQueueSystem,
                GuestCharge = area.GuestCharge,
                TakeawayCharge = area.TakeawayCharge
            });
        }

        public async Task<AreaDto?> GetAreaByIdAsync(int id) // Return AreaDto?
        {
            var (userOrgId, isSuperAdmin) = GetUserContext();
            var area = await _context.Areas.FindAsync(id);

            if (area == null)
            {
                return null;
            }

            // If not SuperAdmin, ensure the area belongs to the user's organization
            if (!isSuperAdmin && area.OrganizationId != userOrgId)
            {
                // User is trying to access an area outside their organization
                return null; // Or throw UnauthorizedAccessException? Returning null is often safer for GET.
            }

            // Map to DTO
            return new AreaDto
            {
                Id = area.Id,
                Name = area.Name,
                Slug = area.Slug, // Map Slug
                OrganizationId = area.OrganizationId,
                EnableCompletionConfirmation = area.EnableCompletionConfirmation,
                EnableKds = area.EnableKds,
                EnableWaiterConfirmation = area.EnableWaiterConfirmation,
                ReceiptPrinterId = area.ReceiptPrinterId, // Map ReceiptPrinterId
                PrintComandasAtCashier = area.PrintComandasAtCashier, // Map PrintComandasAtCashier
                EnableQueueSystem = area.EnableQueueSystem,
                GuestCharge = area.GuestCharge,
                TakeawayCharge = area.TakeawayCharge
            };
        }

        // New method to get by slugs
        public async Task<AreaDto?> GetAreaBySlugsAsync(string orgSlug, string areaSlug)
        {
            // No auth check here, intended for public endpoint. Controller should handle this.
            return await _context.Areas
                                 .Include(a => a.Organization) // Include Organization to filter by its slug
                                 .Where(a => a.Organization != null && a.Organization.Slug == orgSlug && a.Slug == areaSlug)
                                 .Select(area => new AreaDto
                                 {
                                     Id = area.Id,
                                     Name = area.Name,
                                     Slug = area.Slug,
                                     OrganizationId = area.OrganizationId,
                                     EnableCompletionConfirmation = area.EnableCompletionConfirmation,
                                     EnableKds = area.EnableKds,
                                     EnableWaiterConfirmation = area.EnableWaiterConfirmation,
                                     ReceiptPrinterId = area.ReceiptPrinterId, // Map ReceiptPrinterId
                                     PrintComandasAtCashier = area.PrintComandasAtCashier, // Map PrintComandasAtCashier
                                     EnableQueueSystem = area.EnableQueueSystem,
                                     GuestCharge = area.GuestCharge,
                                     TakeawayCharge = area.TakeawayCharge
                                     // Could add Org Name/Slug here if needed by frontend
                                 })
                                 .FirstOrDefaultAsync();
        }

        public async Task<Area> CreateAreaAsync(Area area)
        {
            var (userOrgId, isSuperAdmin) = GetUserContext();

            if (!isSuperAdmin)
            {
                if (!userOrgId.HasValue)
                {
                    throw new InvalidOperationException("User organization context is missing.");
                }
                // Ensure the area is being created for the user's own organization
                if (area.OrganizationId != 0 && area.OrganizationId != userOrgId.Value)
                {
                    throw new UnauthorizedAccessException("Cannot create an area for a different organization.");
                }
                // Assign the user's organization ID if not provided or if it was 0
                area.OrganizationId = userOrgId.Value;
            }
            else // SuperAdmin is creating
            {
                // SuperAdmin MUST specify a valid OrganizationId
                if (area.OrganizationId == 0 || !await _context.Organizations.AnyAsync(o => o.Id == area.OrganizationId))
                {
                    throw new KeyNotFoundException($"Organization with ID {area.OrganizationId} not found or invalid.");
                }
            }

            // Verify Organization exists (redundant for non-SuperAdmin if check above is done, but safe)
            if (!await _context.Organizations.AnyAsync(o => o.Id == area.OrganizationId))
            {
                throw new KeyNotFoundException($"Organization with ID {area.OrganizationId} not found.");
            }

            // Generate and ensure unique slug within the organization
            area.Slug = await GenerateUniqueAreaSlug(area.Name, area.OrganizationId);

            _context.Areas.Add(area);
            await _context.SaveChangesAsync();
            return area;
        }

        // Updated to accept AreaUpsertDto
        public async Task<bool> UpdateAreaAsync(int id, AreaUpsertDto areaDto)
        {
            Console.WriteLine($"Updating area {id} with values: {Newtonsoft.Json.JsonConvert.SerializeObject(areaDto)}");
            var (userOrgId, isSuperAdmin) = GetUserContext();
            Console.WriteLine($"User org ID: {userOrgId}, Is SuperAdmin: {isSuperAdmin}");
            // ID mismatch check removed as DTO doesn't contain Id

            var existingArea = await _context.Areas.FindAsync(id); // Fetch existing
            Console.WriteLine($"Existing area: {Newtonsoft.Json.JsonConvert.SerializeObject(existingArea)}");
            if (existingArea == null)
            {
                return false; // Not found
            }

            // Authorization check: SuperAdmin or user belongs to the area's organization
            if (!isSuperAdmin && existingArea.OrganizationId != userOrgId)
            {
                throw new UnauthorizedAccessException("User is not authorized to update this area.");
            }
            Console.WriteLine($"Authorization check passed");
            // Authorization check 2: Prevent changing the OrganizationId unless SuperAdmin
            // Use areaDto.OrganizationId for the check
            if (!isSuperAdmin && existingArea.OrganizationId != areaDto.OrganizationId)
            {
                if (areaDto.OrganizationId > 0)
                    throw new UnauthorizedAccessException("User is not authorized to change the area's organization.");
                else
                    areaDto.OrganizationId = existingArea.OrganizationId;
            }
            Console.WriteLine($"Organization ID check passed");
            // Update properties from the incoming DTO
            bool nameChanged = existingArea.Name != areaDto.Name;
            existingArea.Name = areaDto.Name;
            bool enableCompletionConfirmationChanged = existingArea.EnableCompletionConfirmation != areaDto.EnableCompletionConfirmation;
            if (enableCompletionConfirmationChanged)
            {
                existingArea.EnableCompletionConfirmation = areaDto.EnableCompletionConfirmation;
            }
            bool enableKdsChanged = existingArea.EnableKds != areaDto.EnableKds;
            if (enableKdsChanged)
            {
                existingArea.EnableKds = areaDto.EnableKds;
            }
            bool enableWaiterConfirmationChanged = existingArea.EnableWaiterConfirmation != areaDto.EnableWaiterConfirmation;
            if (enableWaiterConfirmationChanged)
            {
                existingArea.EnableWaiterConfirmation = areaDto.EnableWaiterConfirmation;
            }
            bool enableQueueSystemChanged = existingArea.EnableQueueSystem != areaDto.EnableQueueSystem;
            if (enableQueueSystemChanged)
            {
                existingArea.EnableQueueSystem = areaDto.EnableQueueSystem;
            }

            // Update PrintComandasAtCashier
            bool printComandasAtCashierChanged = existingArea.PrintComandasAtCashier != areaDto.PrintComandasAtCashier;
            if (printComandasAtCashierChanged)
            {
                existingArea.PrintComandasAtCashier = areaDto.PrintComandasAtCashier;
            }

            bool receiptPrinterIdChanged = existingArea.ReceiptPrinterId != areaDto.ReceiptPrinterId;
            if (receiptPrinterIdChanged)
            {
                existingArea.ReceiptPrinterId = areaDto.ReceiptPrinterId;
            }

            existingArea.GuestCharge = areaDto.GuestCharge;
            existingArea.TakeawayCharge = areaDto.TakeawayCharge;

            Console.WriteLine($"Print comandas at cashier check passed");
            // Only allow SuperAdmin to change OrganizationId if needed, otherwise keep existingArea.OrganizationId
            bool orgChanged = false;
            if (isSuperAdmin && existingArea.OrganizationId != areaDto.OrganizationId)
            {
                // Ensure the target organization exists before assigning
                if (!await _context.Organizations.AnyAsync(o => o.Id == areaDto.OrganizationId))
                {
                    throw new KeyNotFoundException($"Target organization with ID {areaDto.OrganizationId} not found.");
                }
                existingArea.OrganizationId = areaDto.OrganizationId;
                orgChanged = true;
            }
            Console.WriteLine($"Organization ID check passed");
            // Regenerate slug if name or organization changed
            if (nameChanged || orgChanged)
            {
                // Use the potentially updated existingArea.OrganizationId for uniqueness check
                existingArea.Slug = await GenerateUniqueAreaSlug(existingArea.Name, existingArea.OrganizationId, existingArea.Id);
            }
            Console.WriteLine($"Slug check passed");
            // _context.Entry(existingArea).State = EntityState.Modified; // Keep removed

            try
            {
                Console.WriteLine($"Saving changes to the existingArea entity {Newtonsoft.Json.JsonConvert.SerializeObject(existingArea)}");
                // Save changes to the existingArea entity
                await _context.SaveChangesAsync();
                return true;
            }
            catch (DbUpdateConcurrencyException)
            {
                // Check existence again in case it was deleted concurrently
                if (!await _context.Areas.AnyAsync(e => e.Id == id)) // Check existence directly
                {
                    return false;
                }
                else
                {
                    throw; // Re-throw concurrency exception
                }
            }
            catch (DbUpdateException ex)
            {
                // Log the exception details for debugging
                Console.WriteLine($"DbUpdateException during Area update: {ex.InnerException?.Message ?? ex.Message}");
                // Could be FK constraint violation if OrganizationId is invalid (though checked above), etc.
                return false;
            }
        }

        public async Task<bool> DeleteAreaAsync(int id)
        {
            var (userOrgId, isSuperAdmin) = GetUserContext();

            var area = await _context.Areas.FindAsync(id);
            if (area == null)
            {
                return false; // Not found
            }

            // Check ownership before deleting
            if (!isSuperAdmin && area.OrganizationId != userOrgId)
            {
                throw new UnauthorizedAccessException("User is not authorized to delete this area.");
            }

            try
            {
                _context.Areas.Remove(area);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (DbUpdateException ex)
            {
                // Log the exception details for debugging
                Console.WriteLine($"DbUpdateException during Area delete: {ex.InnerException?.Message ?? ex.Message}");
                // Likely due to existing Orders referencing this Area (Restrict constraint)
                // Or other FK constraints (MenuCategories)
                return false;
            }
        }

        public async Task<bool> AreaExistsAsync(int id)
        {
            // This check remains simple, authorization happens in the calling methods (Get, Update, Delete)
            // If needed for direct use, it would require organization context too.
            return await _context.Areas.AnyAsync(e => e.Id == id);
        }

        // --- Slug Generation Helpers ---

        private async Task<string> GenerateUniqueAreaSlug(string name, int organizationId, int? existingAreaId = null)
        {
            string baseSlug = GenerateSlug(name);
            string uniqueSlug = baseSlug;
            int counter = 1;

            // Check if the generated slug already exists for a *different* area in the same organization
            while (await _context.Areas.AnyAsync(a => a.OrganizationId == organizationId && a.Slug == uniqueSlug && a.Id != existingAreaId))
            {
                uniqueSlug = $"{baseSlug}-{counter}";
                counter++;
            }
            return uniqueSlug;
        }

        // Simple slug generation helper (copied from OrganizationService)
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
