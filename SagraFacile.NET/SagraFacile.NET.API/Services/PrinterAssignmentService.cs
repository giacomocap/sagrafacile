using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SagraFacile.NET.API.Data;
using SagraFacile.NET.API.DTOs;
using SagraFacile.NET.API.Models;
using SagraFacile.NET.API.Services.Interfaces;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SagraFacile.NET.API.Services
{
    public class PrinterAssignmentService : BaseService, IPrinterAssignmentService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<PrinterAssignmentService> _logger;

        public PrinterAssignmentService(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor, ILogger<PrinterAssignmentService> logger)
            : base(httpContextAccessor)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<IEnumerable<PrinterCategoryAssignmentDto>> GetAssignmentsForPrinterAsync(int printerId, int areaId)
        {
            var (userOrgId, isSuperAdmin) = GetUserContext();

            // Verify printer exists and belongs to the user's org (or user is SuperAdmin)
            var printer = await _context.Printers.FindAsync(printerId);
            if (printer == null || (!isSuperAdmin && printer.OrganizationId != userOrgId))
            {
                // Return empty list or throw NotFoundException/UnauthorizedAccessException depending on desired behavior
                _logger.LogWarning("Attempt to get assignments for inaccessible printer {PrinterId}. UserOrgId: {UserOrgId}, IsSuperAdmin: {IsSuperAdmin}", printerId, userOrgId, isSuperAdmin);
                return new List<PrinterCategoryAssignmentDto>();
            }

            return await _context.PrinterCategoryAssignments
                .Where(a => a.PrinterId == printerId && a.MenuCategory.AreaId == areaId)
                .Include(a => a.MenuCategory)
                .Select(a => new PrinterCategoryAssignmentDto
                {
                    PrinterId = a.PrinterId,
                    MenuCategoryId = a.MenuCategoryId,
                    MenuCategoryName = a.MenuCategory.Name,
                    MenuCategoryAreaId = a.MenuCategory.AreaId
                })
                .ToListAsync();
        }


        public async Task<(bool Success, string? Error)> SetAssignmentsForPrinterAsync(int printerId, int areaId, IEnumerable<int> menuCategoryIds)
        {
            var (userOrgId, isSuperAdmin) = GetUserContext();

            // 1. Verify printer exists and belongs to the user's org (or user is SuperAdmin)
            var printer = await _context.Printers.FindAsync(printerId);
            if (printer == null)
            {
                return (false, "Printer not found.");
            }
            if (!isSuperAdmin && printer.OrganizationId != userOrgId)
            {
                return (false, "User is not authorized to modify assignments for this printer.");
            }
            Guid organizationId = printer.OrganizationId;

            // 2. Verify all provided category IDs exist *within the same organization*
            var validCategoriesInArea = await _context.MenuCategories
                .Where(mc => mc.AreaId == areaId && menuCategoryIds.Contains(mc.Id)) // Ensure categories are in the specified area
                .Select(mc => mc.Id)
                .ToListAsync();

            var invalidCategoryIds = menuCategoryIds.Except(validCategoriesInArea).ToList();
            if (invalidCategoryIds.Any())
            {
                return (false, $"The following Menu Category IDs do not exist or belong to a different organization: {string.Join(", ", invalidCategoryIds)}");
            }

            // 3. Get current assignments
            var currentAssignmentsInArea = await _context.PrinterCategoryAssignments
                .Include(a => a.MenuCategory) // Include MenuCategory to filter by AreaId
                .Where(a => a.PrinterId == printerId && a.MenuCategory.AreaId == areaId)
                .ToListAsync();

            // 4. Determine assignments to remove
            var assignmentsToRemove = currentAssignmentsInArea
                .Where(a => !menuCategoryIds.Contains(a.MenuCategoryId))
                .ToList();

            // 5. Determine assignments to add
            var existingCategoryIdsInArea = currentAssignmentsInArea.Select(a => a.MenuCategoryId).ToHashSet();
            var assignmentsToAdd = menuCategoryIds
                .Where(catId => !existingCategoryIdsInArea.Contains(catId))
                .Select(catId => new PrinterCategoryAssignment
                {
                    PrinterId = printerId,
                    MenuCategoryId = catId
                })               .ToList();

            // 6. Perform DB operations
            if (assignmentsToRemove.Any())
            {
                _context.PrinterCategoryAssignments.RemoveRange(assignmentsToRemove);
            }

            if (assignmentsToAdd.Any())
            {
                _context.PrinterCategoryAssignments.AddRange(assignmentsToAdd);
            }

            // 7. Save changes if any modifications were needed
            if (assignmentsToRemove.Any() || assignmentsToAdd.Any())
            {
                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateException ex)
                {
                    _logger.LogError(ex, "Error updating printer category assignments for PrinterId {PrinterId}", printerId);
                    return (false, "An error occurred while saving the assignments.");
                }
            }

            return (true, null);
        }
    }
}
