using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SagraFacile.NET.API.Data;
using SagraFacile.NET.API.Models;
using SagraFacile.NET.API.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace SagraFacile.NET.API.Services
{
    public class KdsStationService : BaseService, IKdsStationService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<KdsStationService> _logger;

        // Inject IHttpContextAccessor and pass it to BaseService
        public KdsStationService(ApplicationDbContext context, ILogger<KdsStationService> logger, IHttpContextAccessor httpContextAccessor)
            : base(httpContextAccessor)
        {
            _context = context;
            _logger = logger;
        }

        // Helper method for authorization
        private async Task AuthorizeAndValidateAreaAccessAsync(int organizationId, int areaId, ClaimsPrincipal user, bool requireAdmin = false)
        {
            var (userOrgId, isSuperAdmin) = GetUserContext(); // Use method from BaseService

            if (!isSuperAdmin && userOrgId != organizationId)
            {
                throw new UnauthorizedAccessException($"User does not belong to organization {organizationId}.");
            }

            if (requireAdmin && !isSuperAdmin && !user.IsInRole("OrgAdmin"))
            {
                 throw new UnauthorizedAccessException($"User requires OrgAdmin or SuperAdmin role for this operation.");
            }

            // Verify the Area exists and belongs to the Organization
            var areaExists = await _context.Areas
                                     .AnyAsync(a => a.Id == areaId && a.OrganizationId == organizationId);
            if (!areaExists)
            {
                 throw new KeyNotFoundException($"Area with ID {areaId} not found in organization {organizationId}.");
            }
        }


        public async Task<IEnumerable<KdsStation>> GetKdsStationsByAreaAsync(int organizationId, int areaId, ClaimsPrincipal user)
        {
            await AuthorizeAndValidateAreaAccessAsync(organizationId, areaId, user); // Use helper for authorization

            return await _context.KdsStations
                .Where(ks => ks.OrganizationId == organizationId && ks.AreaId == areaId) // Filter by OrgId and AreaId
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<KdsStation?> GetKdsStationByIdAsync(int organizationId, int areaId, int kdsStationId, ClaimsPrincipal user)
        {
            await AuthorizeAndValidateAreaAccessAsync(organizationId, areaId, user); // Use helper for authorization

            return await _context.KdsStations
                .AsNoTracking()
                // Ensure the station belongs to the correct org and area
                .FirstOrDefaultAsync(ks => ks.Id == kdsStationId && ks.OrganizationId == organizationId && ks.AreaId == areaId);
        }

        public async Task<KdsStation> CreateKdsStationAsync(int organizationId, int areaId, KdsStation newKdsStation, ClaimsPrincipal user)
        {
            await AuthorizeAndValidateAreaAccessAsync(organizationId, areaId, user, requireAdmin: true); // Use helper for authorization (Admin required)

            // Area existence and ownership is checked in AuthorizeAndValidateAreaAccessAsync

            newKdsStation.AreaId = areaId; // Set AreaId from parameter
            newKdsStation.OrganizationId = organizationId; // Set OrganizationId from parameter

            _context.KdsStations.Add(newKdsStation);
            await _context.SaveChangesAsync();
            _logger.LogInformation($"Created KDS Station {newKdsStation.Id} ('{newKdsStation.Name}') in Area {areaId}, Org {organizationId}.");
            return newKdsStation;
        }

        public async Task<bool> UpdateKdsStationAsync(int organizationId, int areaId, int kdsStationId, KdsStation updatedKdsStation, ClaimsPrincipal user)
        {
            await AuthorizeAndValidateAreaAccessAsync(organizationId, areaId, user, requireAdmin: true); // Use helper for authorization (Admin required)

            var existingStation = await _context.KdsStations
                .FirstOrDefaultAsync(ks => ks.Id == kdsStationId && ks.OrganizationId == organizationId && ks.AreaId == areaId); // Verify ownership again

            if (existingStation == null)
            {
                return false; // Or throw KeyNotFoundException
            }

            // Only update allowed fields (e.g., Name)
            existingStation.Name = updatedKdsStation.Name;

            _context.KdsStations.Update(existingStation);
            await _context.SaveChangesAsync();
            _logger.LogInformation($"Updated KDS Station {existingStation.Id} in Area {areaId}, Org {organizationId}.");
            return true;
        }

        public async Task<bool> DeleteKdsStationAsync(int organizationId, int areaId, int kdsStationId, ClaimsPrincipal user)
        {
            await AuthorizeAndValidateAreaAccessAsync(organizationId, areaId, user, requireAdmin: true); // Use helper for authorization (Admin required)

            var stationToDelete = await _context.KdsStations
                .FirstOrDefaultAsync(ks => ks.Id == kdsStationId && ks.OrganizationId == organizationId && ks.AreaId == areaId); // Verify ownership again

            if (stationToDelete == null)
            {
                return false; // Or throw KeyNotFoundException
            }

            _context.KdsStations.Remove(stationToDelete); // Cascade delete should handle assignments
            await _context.SaveChangesAsync();
            _logger.LogInformation($"Deleted KDS Station {kdsStationId} from Area {areaId}, Org {organizationId}.");
            return true;
        }

        // --- Category Assignments ---

        public async Task<IEnumerable<MenuCategory>> GetAssignedCategoriesAsync(int organizationId, int areaId, int kdsStationId, ClaimsPrincipal user)
        {
            await AuthorizeAndValidateAreaAccessAsync(organizationId, areaId, user); // Use helper for authorization

            // Verify the KDS station belongs to the specified area and organization (checked again for safety)
            var stationExists = await _context.KdsStations
                .AsNoTracking() // No need to track for a check
                .AnyAsync(ks => ks.Id == kdsStationId && ks.AreaId == areaId && ks.OrganizationId == organizationId);

            if (!stationExists)
            {
                throw new KeyNotFoundException($"KDS Station with ID {kdsStationId} not found in Area {areaId}.");
            }

            return await _context.KdsCategoryAssignments
                .Where(kca => kca.KdsStationId == kdsStationId)
                .Include(kca => kca.MenuCategory)
                .Select(kca => kca.MenuCategory)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<bool> AssignCategoryAsync(int organizationId, int areaId, int kdsStationId, int menuCategoryId, ClaimsPrincipal user)
        {
            await AuthorizeAndValidateAreaAccessAsync(organizationId, areaId, user, requireAdmin: true); // Use helper for authorization (Admin required)

            // Verify KDS Station exists in the correct Area/Org (checked again for safety)
            var station = await _context.KdsStations
                .AsNoTracking() // No need to track for a check
                .FirstOrDefaultAsync(ks => ks.Id == kdsStationId && ks.AreaId == areaId && ks.OrganizationId == organizationId);
            if (station == null)
            {
                throw new KeyNotFoundException($"KDS Station with ID {kdsStationId} not found in Area {areaId}.");
            }

            // Verify Menu Category exists in the correct Area/Org
            var category = await _context.MenuCategories
                .AsNoTracking() // No need to track for a check
                .FirstOrDefaultAsync(mc => mc.Id == menuCategoryId && mc.AreaId == areaId); // Categories are tied to Area
            if (category == null)
            {
                throw new KeyNotFoundException($"Menu Category with ID {menuCategoryId} not found in Area {areaId}.");
            }

            // Check if assignment already exists
            var assignmentExists = await _context.KdsCategoryAssignments
                .AnyAsync(kca => kca.KdsStationId == kdsStationId && kca.MenuCategoryId == menuCategoryId);

            if (assignmentExists)
            {
                _logger.LogWarning($"Category {menuCategoryId} is already assigned to KDS Station {kdsStationId}.");
                return false; // Indicate no change was made
            }

            var newAssignment = new KdsCategoryAssignment
            {
                KdsStationId = kdsStationId,
                MenuCategoryId = menuCategoryId
            };

            _context.KdsCategoryAssignments.Add(newAssignment);
            await _context.SaveChangesAsync();
            _logger.LogInformation($"Assigned Category {menuCategoryId} to KDS Station {kdsStationId}.");
            return true;
        }

        public async Task<bool> UnassignCategoryAsync(int organizationId, int areaId, int kdsStationId, int menuCategoryId, ClaimsPrincipal user)
        {
            await AuthorizeAndValidateAreaAccessAsync(organizationId, areaId, user, requireAdmin: true); // Use helper for authorization (Admin required)

            // Find the specific assignment to remove
            var assignmentToRemove = await _context.KdsCategoryAssignments
                // Ensure the assignment corresponds to the correct station/area/org implicitly via kdsStationId check below
                .FirstOrDefaultAsync(kca => kca.KdsStationId == kdsStationId && kca.MenuCategoryId == menuCategoryId);

            if (assignmentToRemove == null)
            {
                _logger.LogWarning($"Assignment for Category {menuCategoryId} and KDS Station {kdsStationId} not found.");
                return false; // Indicate assignment didn't exist
            }

            // Verify the station belongs to the correct area/org (already checked in AuthorizeAndValidateAreaAccessAsync)
            // We trust the assignmentToRemove lookup is sufficient here as it uses the kdsStationId

            _context.KdsCategoryAssignments.Remove(assignmentToRemove);
            await _context.SaveChangesAsync();
            _logger.LogInformation($"Unassigned Category {menuCategoryId} from KDS Station {kdsStationId}.");
            return true;
        }
    }
}
