using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SagraFacile.NET.API.Data;
using SagraFacile.NET.API.DTOs;
using SagraFacile.NET.API.Models;
using SagraFacile.NET.API.Services.Interfaces;

namespace SagraFacile.NET.API.Services
{
    public class CashierStationService : BaseService, ICashierStationService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;

        public CashierStationService(
            ApplicationDbContext context,
            UserManager<User> userManager,
            IHttpContextAccessor httpContextAccessor) : base(httpContextAccessor)
        {
            _context = context;
            _userManager = userManager;
        }

        // Helper method to check organization access based on user context
        private async Task<bool> HasAccessToOrganization(int targetOrganizationId, string[]? allowedRoles = null)
        {
            var (userOrganizationId, isSuperAdmin) = GetUserContext(); // From BaseService

            if (isSuperAdmin) return true; // SuperAdmin has access to all organizations

            // Non-SuperAdmin must belong to the target organization
            if (userOrganizationId != targetOrganizationId) return false;

            // If specific roles are required, check them
            if (allowedRoles != null && allowedRoles.Any())
            {
                var userId = GetUserId(); // From BaseService
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null) return false; // Should not happen for an authenticated user

                foreach (var role in allowedRoles)
                {
                    if (await _userManager.IsInRoleAsync(user, role))
                    {
                        return true; // User has one of the allowed roles
                    }
                }
                return false; // User does not have any of the allowed roles
            }

            return true; // Access granted if no specific roles are required beyond organization membership
        }

        private static CashierStationDto MapToDto(CashierStation station)
        {
            return new CashierStationDto
            {
                Id = station.Id,
                OrganizationId = station.OrganizationId,
                AreaId = station.AreaId,
                AreaName = station.Area?.Name ?? "N/A",
                Name = station.Name,
                ReceiptPrinterId = station.ReceiptPrinterId,
                ReceiptPrinterName = station.ReceiptPrinter?.Name ?? "N/A",
                PrintComandasAtThisStation = station.PrintComandasAtThisStation,
                IsEnabled = station.IsEnabled
            };
        }

        public async Task<CashierStationDto?> GetStationByIdAsync(int stationId, User currentUser_IGNORED) // currentUser is derived from HttpContext
        {
            var station = await _context.CashierStations
                .Include(cs => cs.Area)
                .Include(cs => cs.ReceiptPrinter)
                .FirstOrDefaultAsync(cs => cs.Id == stationId);

            if (station == null || !await HasAccessToOrganization(station.OrganizationId))
            {
                return null;
            }
            return MapToDto(station);
        }

        public async Task<IEnumerable<CashierStationDto>> GetStationsByOrganizationAsync(int organizationId, User currentUser_IGNORED)
        {
            if (!await HasAccessToOrganization(organizationId))
            {
                // Consider throwing a 403 Forbidden or returning an empty list based on desired behavior
                // For now, returning empty list to avoid leaking information about organization existence if user has no access
                return new List<CashierStationDto>();
            }

            return await _context.CashierStations
                .Where(cs => cs.OrganizationId == organizationId)
                .Include(cs => cs.Area)
                .Include(cs => cs.ReceiptPrinter)
                .Select(cs => MapToDto(cs))
                .ToListAsync();
        }

        public async Task<IEnumerable<CashierStationDto>> GetStationsByAreaAsync(int areaId, User currentUser_IGNORED)
        {
            var area = await _context.Areas.FindAsync(areaId);
                if (area == null) return new List<CashierStationDto>(); // Area not found

            if (!await HasAccessToOrganization(area.OrganizationId))
            {
                return new List<CashierStationDto>();
            }

            return await _context.CashierStations
                .Where(cs => cs.AreaId == areaId)
                .Include(cs => cs.Area)
                .Include(cs => cs.ReceiptPrinter)
                .Select(cs => MapToDto(cs))
                .ToListAsync();
        }

        public async Task<(CashierStationDto? Station, string? Error)> CreateStationAsync(int organizationId, CashierStationUpsertDto dto, User currentUser_IGNORED)
        {
            if (!await HasAccessToOrganization(organizationId, new[] { "SuperAdmin", "Admin" }))
            {
                return (null, "Unauthorized to create a station in this organization.");
            }

            var area = await _context.Areas.FindAsync(dto.AreaId);
            if (area == null || area.OrganizationId != organizationId)
            {
                return (null, "Invalid AreaId or Area does not belong to the organization.");
            }

            var printer = await _context.Printers.FindAsync(dto.ReceiptPrinterId);
            if (printer == null || printer.OrganizationId != organizationId)
            {
                return (null, "Invalid ReceiptPrinterId or Printer does not belong to the organization.");
            }

            var station = new CashierStation
            {
                OrganizationId = organizationId,
                AreaId = dto.AreaId.Value,
                Name = dto.Name,
                ReceiptPrinterId = dto.ReceiptPrinterId.Value,
                PrintComandasAtThisStation = dto.PrintComandasAtThisStation,
                IsEnabled = dto.IsEnabled
            };

            _context.CashierStations.Add(station);
            await _context.SaveChangesAsync();

            // Re-fetch with includes for the DTO, because the original `station` object's navigation properties (Area, ReceiptPrinter) are not populated yet.
            var createdStationWithIncludes = await _context.CashierStations
                .Include(cs => cs.Area)      // Eagerly load Area
                .Include(cs => cs.ReceiptPrinter) // Eagerly load ReceiptPrinter
                .FirstAsync(cs => cs.Id == station.Id);

            return (MapToDto(createdStationWithIncludes), null);
        }

        public async Task<(CashierStationDto? Station, string? Error)> UpdateStationAsync(int stationId, CashierStationUpsertDto dto, User currentUser_IGNORED)
        {
            var station = await _context.CashierStations
                .FirstOrDefaultAsync(cs => cs.Id == stationId);

            if (station == null)
            {
                return (null, "Station not found.");
            }

            if (!await HasAccessToOrganization(station.OrganizationId, new[] { "SuperAdmin", "Admin" }))
            {
                return (null, "Unauthorized to update this station.");
            }

            var area = await _context.Areas.FindAsync(dto.AreaId);
            if (area == null || area.OrganizationId != station.OrganizationId)
            {
                return (null, "Invalid AreaId or Area does not belong to the station's organization.");
            }

            var printer = await _context.Printers.FindAsync(dto.ReceiptPrinterId);
            if (printer == null || printer.OrganizationId != station.OrganizationId)
            {
                return (null, "Invalid ReceiptPrinterId or Printer does not belong to the station's organization.");
            }

            station.AreaId = dto.AreaId.Value;
            station.Name = dto.Name;
            station.ReceiptPrinterId = dto.ReceiptPrinterId.Value;
            station.PrintComandasAtThisStation = dto.PrintComandasAtThisStation;
            station.IsEnabled = dto.IsEnabled;

            await _context.SaveChangesAsync();

            // Re-fetch with includes for the DTO
            var updatedStationWithIncludes = await _context.CashierStations
                .Include(cs => cs.Area)
                .Include(cs => cs.ReceiptPrinter)
                .FirstAsync(cs => cs.Id == station.Id);

            return (MapToDto(updatedStationWithIncludes), null);
        }

        public async Task<(bool Success, string? Error)> DeleteStationAsync(int stationId, User currentUser_IGNORED)
        {
            var station = await _context.CashierStations
                .FirstOrDefaultAsync(cs => cs.Id == stationId);

            if (station == null)
            {
                return (false, "Station not found.");
            }

            if (!await HasAccessToOrganization(station.OrganizationId, new[] { "SuperAdmin", "Admin" }))
            {
                return (false, "Unauthorized to delete this station.");
            }
            
            bool isUsedInOrders = await _context.Orders.AnyAsync(o => o.CashierStationId == stationId);
            if (isUsedInOrders) {
                 // Option 1: Prevent deletion
                 return (false, "Station cannot be deleted as it is associated with existing orders. Please disable it instead or reassign orders.");
                 // Option 2: Soft delete (if IsEnabled is used for this, or add an IsDeleted flag)
                 // station.IsEnabled = false; 
                 // await _context.SaveChangesAsync();
                 // return (true, null); // If soft deleting
            }

            _context.CashierStations.Remove(station);
            await _context.SaveChangesAsync();
            return (true, null);
        }
    }
} 