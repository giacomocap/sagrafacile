using Microsoft.EntityFrameworkCore;
using SagraFacile.NET.API.Data;
using SagraFacile.NET.API.Models;
using SagraFacile.NET.API.DTOs; // Added for AreaDto
using SagraFacile.NET.API.Services.Interfaces;
using System.Text.RegularExpressions;

namespace SagraFacile.NET.API.Services
{
    // Inherit from BaseService
    public class AreaService : BaseService, IAreaService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AreaService> _logger; // Inject ILogger
        // IHttpContextAccessor is now inherited from BaseService

        public AreaService(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor, ILogger<AreaService> logger)
            : base(httpContextAccessor) // Call base constructor
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // GetUserContext helper is now inherited from BaseService

        // Removed optional organizationId parameter - it's derived from user context now
        public async Task<IEnumerable<AreaDto>> GetAllAreasAsync() // Return AreaDto
        {
            _logger.LogInformation("Fetching all areas.");
            var (userOrgId, isSuperAdmin) = GetUserContext();

            var query = _context.Areas.AsQueryable();

            // If a specific organization context is selected (even by SuperAdmin), filter by it.
            if (userOrgId.HasValue)
            {
                _logger.LogDebug("Filtering areas by OrganizationId: {OrganizationId}.", userOrgId.Value);
                query = query.Where(a => a.OrganizationId == userOrgId.Value);
            }
            // If it's a SuperAdmin AND no specific organization is selected (userOrgId is null),
            // then they see all areas. Otherwise (non-SuperAdmin), the check above already filtered.
            else if (!isSuperAdmin) // This case handles non-SuperAdmin when userOrgId is somehow null (shouldn't happen)
            {
                _logger.LogError("User organization context is missing for non-SuperAdmin when fetching all areas.");
                throw new InvalidOperationException("User organization context is missing for non-SuperAdmin.");
            }
            else
            {
                _logger.LogDebug("Caller is SuperAdmin and no specific organization selected. Fetching all areas across organizations.");
            }

            var areas = await query.ToListAsync();

            // Map to DTOs
            var areaDtos = areas.Select(area => new AreaDto
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
            }).ToList();
            _logger.LogInformation("Successfully fetched {Count} areas.", areaDtos.Count);
            return areaDtos;
        }

        public async Task<AreaDto?> GetAreaByIdAsync(int id) // Return AreaDto?
        {
            _logger.LogInformation("Fetching area with ID: {AreaId}.", id);
            var (userOrgId, isSuperAdmin) = GetUserContext();
            var area = await _context.Areas.FindAsync(id);

            if (area == null)
            {
                _logger.LogWarning("Area with ID {AreaId} not found.", id);
                return null;
            }

            // If not SuperAdmin, ensure the area belongs to the user's organization
            if (!isSuperAdmin)
            {
                _logger.LogDebug("Caller is not SuperAdmin. Checking organization access for AreaId: {AreaId}.", id);
                if (area.OrganizationId != userOrgId)
                {
                    _logger.LogWarning("Unauthorized access attempt: User {UserId} from Org {UserOrgId} tried to access Area {AreaId} from Org {AreaOrgId}.", GetUserId(), userOrgId, id, area.OrganizationId);
                    return null;
                }
            }
            else
            {
                _logger.LogDebug("Caller is SuperAdmin. Access granted for AreaId: {AreaId}.", id);
            }

            // Map to DTO
            var areaDto = new AreaDto
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
            _logger.LogInformation("Successfully fetched area {AreaId}.", id);
            return areaDto;
        }

        // New method to get by slugs
        public async Task<AreaDto?> GetAreaBySlugsAsync(string orgSlug, string areaSlug)
        {
            _logger.LogInformation("Fetching area by Organization Slug: {OrgSlug} and Area Slug: {AreaSlug}.", orgSlug, areaSlug);
            var areaDto = await _context.Areas
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
                                 })
                                 .FirstOrDefaultAsync();
            if (areaDto == null)
            {
                _logger.LogWarning("Area not found for Organization Slug: {OrgSlug} and Area Slug: {AreaSlug}.", orgSlug, areaSlug);
            }
            else
            {
                _logger.LogInformation("Successfully fetched area {AreaId} by slugs.", areaDto.Id);
            }
            return areaDto;
        }

        public async Task<Area> CreateAreaAsync(Area area)
        {
            _logger.LogInformation("Attempting to create area '{AreaName}' for OrganizationId: {OrganizationId}.", area.Name, area.OrganizationId);
            var (userOrgId, isSuperAdmin) = GetUserContext();

            if (!isSuperAdmin)
            {
                _logger.LogDebug("Caller is not SuperAdmin. Enforcing area creation within caller's organization.");
                if (!userOrgId.HasValue)
                {
                    _logger.LogError("User organization context is missing for non-SuperAdmin when creating area.");
                    throw new InvalidOperationException("User organization context is missing.");
                }
                // Ensure the area is being created for the user's own organization
                if (area.OrganizationId != Guid.Empty && area.OrganizationId != userOrgId.Value)
                {
                    _logger.LogWarning("Unauthorized attempt to create area for a different organization. Caller OrgId: {CallerOrgId}, Target OrgId: {TargetOrgId}.", userOrgId.Value, area.OrganizationId);
                    throw new UnauthorizedAccessException("Cannot create an area for a different organization.");
                }
                // Assign the user's organization ID if not provided or if it was Guid.Empty
                area.OrganizationId = userOrgId.Value;
            }
            else // SuperAdmin is creating
            {
                _logger.LogDebug("Caller is SuperAdmin. Verifying target OrganizationId for area creation.");
                // SuperAdmin MUST specify a valid OrganizationId
                if (area.OrganizationId == Guid.Empty || !await _context.Organizations.AnyAsync(o => o.Id == area.OrganizationId))
                {
                    _logger.LogWarning("SuperAdmin area creation failed: Organization with ID {OrganizationId} not found or invalid.", area.OrganizationId);
                    throw new KeyNotFoundException($"Organization with ID {area.OrganizationId} not found or invalid.");
                }
            }

            // Verify Organization exists (redundant for non-SuperAdmin if check above is done, but safe)
            if (!await _context.Organizations.AnyAsync(o => o.Id == area.OrganizationId))
            {
                _logger.LogWarning("Area creation failed: Organization with ID {OrganizationId} not found.", area.OrganizationId);
                throw new KeyNotFoundException($"Organization with ID {area.OrganizationId} not found.");
            }

            // Generate and ensure unique slug within the organization
            area.Slug = await GenerateUniqueAreaSlug(area.Name, area.OrganizationId);
            _logger.LogDebug("Generated unique slug '{Slug}' for area '{AreaName}'.", area.Slug, area.Name);

            _context.Areas.Add(area);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Area '{AreaName}' created successfully with ID {AreaId} and Slug '{Slug}'.", area.Name, area.Id, area.Slug);
            return area;
        }

        // Updated to accept AreaUpsertDto
        public async Task<bool> UpdateAreaAsync(int id, AreaUpsertDto areaDto)
        {
            _logger.LogInformation("Attempting to update area with ID: {AreaId}. Incoming DTO: {@AreaDto}", id, areaDto);
            var (userOrgId, isSuperAdmin) = GetUserContext();
            _logger.LogDebug("Caller User Org ID: {UserOrgId}, Is SuperAdmin: {IsSuperAdmin}.", userOrgId, isSuperAdmin);

            var existingArea = await _context.Areas.FindAsync(id);
            if (existingArea == null)
            {
                _logger.LogWarning("Update area failed: Area with ID {AreaId} not found.", id);
                return false;
            }
            _logger.LogDebug("Found existing area: {@ExistingArea}", existingArea);

            // Authorization check: SuperAdmin or user belongs to the area's organization
            if (!isSuperAdmin && existingArea.OrganizationId != userOrgId)
            {
                _logger.LogWarning("Unauthorized update attempt: User {UserId} from Org {UserOrgId} tried to update Area {AreaId} from Org {AreaOrgId}.", GetUserId(), userOrgId, id, existingArea.OrganizationId);
                throw new UnauthorizedAccessException("User is not authorized to update this area.");
            }
            _logger.LogDebug("Authorization check passed for AreaId: {AreaId}.", id);

            // Authorization check 2: Prevent changing the OrganizationId unless SuperAdmin
            if (!isSuperAdmin && existingArea.OrganizationId != areaDto.OrganizationId)
            {
                if (areaDto.OrganizationId != Guid.Empty)
                {
                    _logger.LogWarning("Unauthorized attempt to change area's organization. Caller is not SuperAdmin. AreaId: {AreaId}, Current OrgId: {CurrentOrgId}, Attempted OrgId: {AttemptedOrgId}.", id, existingArea.OrganizationId, areaDto.OrganizationId);
                    throw new UnauthorizedAccessException("User is not authorized to change the area's organization.");
                }
                else
                {
                    areaDto.OrganizationId = existingArea.OrganizationId; // Revert to original if Guid.Empty was passed
                    _logger.LogDebug("AreaDto OrganizationId was Guid.Empty, reverted to existing area's OrganizationId: {OrganizationId}.", existingArea.OrganizationId);
                }
            }
            _logger.LogDebug("Organization ID change check passed.");

            // Update properties from the incoming DTO
            bool nameChanged = existingArea.Name != areaDto.Name;
            existingArea.Name = areaDto.Name;
            bool enableCompletionConfirmationChanged = existingArea.EnableCompletionConfirmation != areaDto.EnableCompletionConfirmation;
            if (enableCompletionConfirmationChanged)
            {
                existingArea.EnableCompletionConfirmation = areaDto.EnableCompletionConfirmation;
                _logger.LogDebug("EnableCompletionConfirmation changed to {Value} for AreaId: {AreaId}.", areaDto.EnableCompletionConfirmation, id);
            }
            bool enableKdsChanged = existingArea.EnableKds != areaDto.EnableKds;
            if (enableKdsChanged)
            {
                existingArea.EnableKds = areaDto.EnableKds;
                _logger.LogDebug("EnableKds changed to {Value} for AreaId: {AreaId}.", areaDto.EnableKds, id);
            }
            bool enableWaiterConfirmationChanged = existingArea.EnableWaiterConfirmation != areaDto.EnableWaiterConfirmation;
            if (enableWaiterConfirmationChanged)
            {
                existingArea.EnableWaiterConfirmation = areaDto.EnableWaiterConfirmation;
                _logger.LogDebug("EnableWaiterConfirmation changed to {Value} for AreaId: {AreaId}.", areaDto.EnableWaiterConfirmation, id);
            }
            bool enableQueueSystemChanged = existingArea.EnableQueueSystem != areaDto.EnableQueueSystem;
            if (enableQueueSystemChanged)
            {
                existingArea.EnableQueueSystem = areaDto.EnableQueueSystem;
                _logger.LogDebug("EnableQueueSystem changed to {Value} for AreaId: {AreaId}.", areaDto.EnableQueueSystem, id);
            }

            // Update PrintComandasAtCashier
            bool printComandasAtCashierChanged = existingArea.PrintComandasAtCashier != areaDto.PrintComandasAtCashier;
            if (printComandasAtCashierChanged)
            {
                existingArea.PrintComandasAtCashier = areaDto.PrintComandasAtCashier;
                _logger.LogDebug("PrintComandasAtCashier changed to {Value} for AreaId: {AreaId}.", areaDto.PrintComandasAtCashier, id);
            }

            bool receiptPrinterIdChanged = existingArea.ReceiptPrinterId != areaDto.ReceiptPrinterId;
            if (receiptPrinterIdChanged)
            {
                existingArea.ReceiptPrinterId = areaDto.ReceiptPrinterId;
                _logger.LogDebug("ReceiptPrinterId changed to {Value} for AreaId: {AreaId}.", areaDto.ReceiptPrinterId, id);
            }

            existingArea.GuestCharge = areaDto.GuestCharge;
            existingArea.TakeawayCharge = areaDto.TakeawayCharge;
            _logger.LogDebug("GuestCharge set to {GuestCharge} and TakeawayCharge set to {TakeawayCharge} for AreaId: {AreaId}.", areaDto.GuestCharge, areaDto.TakeawayCharge, id);

            // Only allow SuperAdmin to change OrganizationId if needed, otherwise keep existingArea.OrganizationId
            bool orgChanged = false;
            if (isSuperAdmin && existingArea.OrganizationId != areaDto.OrganizationId)
            {
                _logger.LogDebug("SuperAdmin changing OrganizationId for AreaId: {AreaId} from {OldOrgId} to {NewOrgId}.", id, existingArea.OrganizationId, areaDto.OrganizationId);
                // Ensure the target organization exists before assigning
                if (!await _context.Organizations.AnyAsync(o => o.Id == areaDto.OrganizationId))
                {
                    _logger.LogWarning("Update area failed: Target organization with ID {OrganizationId} not found.", areaDto.OrganizationId);
                    throw new KeyNotFoundException($"Target organization with ID {areaDto.OrganizationId} not found.");
                }
                existingArea.OrganizationId = areaDto.OrganizationId;
                orgChanged = true;
            }
            _logger.LogDebug("Final Organization ID for AreaId {AreaId} is {OrganizationId}.", id, existingArea.OrganizationId);

            // Regenerate slug if name or organization changed
            if (nameChanged || orgChanged)
            {
                existingArea.Slug = await GenerateUniqueAreaSlug(existingArea.Name, existingArea.OrganizationId, existingArea.Id);
                _logger.LogDebug("Slug regenerated for AreaId {AreaId}. New slug: '{NewSlug}'.", id, existingArea.Slug);
            }

            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Area {AreaId} updated successfully.", id);
                return true;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogError(ex, "Concurrency error updating area {AreaId}.", id);
                if (!await _context.Areas.AnyAsync(e => e.Id == id))
                {
                    _logger.LogWarning("Area {AreaId} was concurrently deleted.", id);
                    return false;
                }
                else
                {
                    throw;
                }
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database update error updating area {AreaId}. Inner exception: {InnerMessage}", id, ex.InnerException?.Message);
                return false;
            }
        }

        public async Task<bool> DeleteAreaAsync(int id)
        {
            _logger.LogInformation("Attempting to delete area with ID: {AreaId}.", id);
            var (userOrgId, isSuperAdmin) = GetUserContext();

            var area = await _context.Areas.FindAsync(id);
            if (area == null)
            {
                _logger.LogWarning("Delete area failed: Area with ID {AreaId} not found.", id);
                return false;
            }

            // Check ownership before deleting
            if (!isSuperAdmin && area.OrganizationId != userOrgId)
            {
                _logger.LogWarning("Unauthorized delete attempt: User {UserId} from Org {UserOrgId} tried to delete Area {AreaId} from Org {AreaOrgId}.", GetUserId(), userOrgId, id, area.OrganizationId);
                throw new UnauthorizedAccessException("User is not authorized to delete this area.");
            }
            _logger.LogDebug("Authorization check passed for deleting AreaId: {AreaId}.", id);

            try
            {
                _context.Areas.Remove(area);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Area {AreaId} deleted successfully.", id);
                return true;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database update error deleting area {AreaId}. Inner exception: {InnerMessage}", id, ex.InnerException?.Message);
                return false;
            }
        }

        public async Task<bool> AreaExistsAsync(int id)
        {
            _logger.LogDebug("Checking if area with ID {AreaId} exists.", id);
            var exists = await _context.Areas.AnyAsync(e => e.Id == id);
            _logger.LogDebug("Area {AreaId} exists: {Exists}.", id, exists);
            return exists;
        }

        // --- Slug Generation Helpers ---

        private async Task<string> GenerateUniqueAreaSlug(string name, Guid organizationId, int? existingAreaId = null)
        {
            _logger.LogDebug("Generating unique slug for area '{AreaName}' in OrganizationId: {OrganizationId}. Existing AreaId: {ExistingAreaId}.", name, organizationId, existingAreaId);
            string baseSlug = GenerateSlug(name);
            string uniqueSlug = baseSlug;
            int counter = 1;

            while (await _context.Areas.AnyAsync(a => a.OrganizationId == organizationId && a.Slug == uniqueSlug && a.Id != existingAreaId))
            {
                uniqueSlug = $"{baseSlug}-{counter}";
                counter++;
                _logger.LogDebug("Slug '{UniqueSlug}' already exists, trying '{NewSlug}'.", uniqueSlug, $"{baseSlug}-{counter}");
            }
            _logger.LogDebug("Generated unique slug: '{UniqueSlug}'.", uniqueSlug);
            return uniqueSlug;
        }

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
