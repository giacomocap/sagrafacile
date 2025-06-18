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
        private readonly ILogger<CashierStationService> _logger; // Inject ILogger

        public CashierStationService(
            ApplicationDbContext context,
            UserManager<User> userManager,
            IHttpContextAccessor httpContextAccessor,
            ILogger<CashierStationService> logger) : base(httpContextAccessor)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger; // Assign ILogger
        }

        // Helper method to check organization access based on user context
        private async Task<bool> HasAccessToOrganization(int targetOrganizationId, string[]? allowedRoles = null)
        {
            var (userOrganizationId, isSuperAdmin) = GetUserContext(); // From BaseService
            _logger.LogDebug("Checking access to organization {TargetOrganizationId}. Caller OrgId: {UserOrganizationId}, IsSuperAdmin: {IsSuperAdmin}.", targetOrganizationId, userOrganizationId, isSuperAdmin);

            if (isSuperAdmin) return true; // SuperAdmin has access to all organizations

            // Non-SuperAdmin must belong to the target organization
            if (userOrganizationId != targetOrganizationId)
            {
                _logger.LogWarning("Access denied: User from organization {UserOrganizationId} attempted to access data for organization {TargetOrganizationId}.", userOrganizationId, targetOrganizationId);
                return false;
            }

            // If specific roles are required, check them
            if (allowedRoles != null && allowedRoles.Any())
            {
                var userId = GetUserId(); // From BaseService
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    _logger.LogError("Access check failed: Authenticated user with ID {UserId} not found.", userId);
                    return false; // Should not happen for an authenticated user
                }

                foreach (var role in allowedRoles)
                {
                    if (await _userManager.IsInRoleAsync(user, role))
                    {
                        _logger.LogDebug("Access granted: User {UserId} has role '{Role}' for organization {TargetOrganizationId}.", userId, role, targetOrganizationId);
                        return true; // User has one of the allowed roles
                    }
                }
                _logger.LogWarning("Access denied: User {UserId} does not have required roles ({Roles}) for organization {TargetOrganizationId}.", userId, string.Join(", ", allowedRoles), targetOrganizationId);
                return false; // User does not have any of the allowed roles
            }

            _logger.LogDebug("Access granted to organization {TargetOrganizationId}.", targetOrganizationId);
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
            _logger.LogInformation("Fetching cashier station with ID: {StationId}.", stationId);
            var station = await _context.CashierStations
                .Include(cs => cs.Area)
                .Include(cs => cs.ReceiptPrinter)
                .FirstOrDefaultAsync(cs => cs.Id == stationId);

            if (station == null)
            {
                _logger.LogWarning("Cashier station with ID {StationId} not found.", stationId);
                return null;
            }

            if (!await HasAccessToOrganization(station.OrganizationId))
            {
                _logger.LogWarning("Unauthorized access to cashier station {StationId} for organization {OrganizationId}.", stationId, station.OrganizationId);
                return null;
            }
            _logger.LogInformation("Successfully fetched cashier station {StationId}.", stationId);
            return MapToDto(station);
        }

        public async Task<IEnumerable<CashierStationDto>> GetStationsByOrganizationAsync(int organizationId, User currentUser_IGNORED)
        {
            _logger.LogInformation("Fetching cashier stations for OrganizationId: {OrganizationId}.", organizationId);
            if (!await HasAccessToOrganization(organizationId))
            {
                _logger.LogWarning("Unauthorized to fetch cashier stations for organization {OrganizationId}.", organizationId);
                return new List<CashierStationDto>();
            }

            var stations = await _context.CashierStations
                .Where(cs => cs.OrganizationId == organizationId)
                .Include(cs => cs.Area)
                .Include(cs => cs.ReceiptPrinter)
                .Select(cs => MapToDto(cs))
                .ToListAsync();
            _logger.LogInformation("Found {Count} cashier stations for OrganizationId: {OrganizationId}.", stations.Count, organizationId);
            return stations;
        }

        public async Task<IEnumerable<CashierStationDto>> GetStationsByAreaAsync(int areaId, User currentUser_IGNORED)
        {
            _logger.LogInformation("Fetching cashier stations for AreaId: {AreaId}.", areaId);
            var area = await _context.Areas.FindAsync(areaId);
            if (area == null)
            {
                _logger.LogWarning("Area with ID {AreaId} not found when fetching cashier stations by area.", areaId);
                return new List<CashierStationDto>(); // Area not found
            }

            if (!await HasAccessToOrganization(area.OrganizationId))
            {
                _logger.LogWarning("Unauthorized to fetch cashier stations for AreaId {AreaId} (OrganizationId: {OrganizationId}).", areaId, area.OrganizationId);
                return new List<CashierStationDto>();
            }

            var stations = await _context.CashierStations
                .Where(cs => cs.AreaId == areaId)
                .Include(cs => cs.Area)
                .Include(cs => cs.ReceiptPrinter)
                .Select(cs => MapToDto(cs))
                .ToListAsync();
            _logger.LogInformation("Found {Count} cashier stations for AreaId: {AreaId}.", stations.Count, areaId);
            return stations;
        }

        public async Task<(CashierStationDto? Station, string? Error)> CreateStationAsync(int organizationId, CashierStationUpsertDto dto, User currentUser_IGNORED)
        {
            _logger.LogInformation("Attempting to create cashier station '{StationName}' for OrganizationId: {OrganizationId}.", dto.Name, organizationId);
            if (!await HasAccessToOrganization(organizationId, new[] { "SuperAdmin", "Admin" }))
            {
                _logger.LogWarning("Unauthorized to create cashier station in organization {OrganizationId}.", organizationId);
                return (null, "Unauthorized to create a station in this organization.");
            }

            var area = await _context.Areas.FindAsync(dto.AreaId);
            if (area == null || area.OrganizationId != organizationId)
            {
                _logger.LogWarning("Create station failed: Invalid AreaId {AreaId} or Area does not belong to organization {OrganizationId}.", dto.AreaId, organizationId);
                return (null, "Invalid AreaId or Area does not belong to the organization.");
            }

            var printer = await _context.Printers.FindAsync(dto.ReceiptPrinterId);
            if (printer == null || printer.OrganizationId != organizationId)
            {
                _logger.LogWarning("Create station failed: Invalid ReceiptPrinterId {PrinterId} or Printer does not belong to organization {OrganizationId}.", dto.ReceiptPrinterId, organizationId);
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
            _logger.LogInformation("Cashier station '{StationName}' created successfully with ID {StationId}.", station.Name, station.Id);

            // Re-fetch with includes for the DTO, because the original `station` object's navigation properties (Area, ReceiptPrinter) are not populated yet.
            var createdStationWithIncludes = await _context.CashierStations
                .Include(cs => cs.Area)      // Eagerly load Area
                .Include(cs => cs.ReceiptPrinter) // Eagerly load ReceiptPrinter
                .FirstAsync(cs => cs.Id == station.Id);

            return (MapToDto(createdStationWithIncludes), null);
        }

        public async Task<(CashierStationDto? Station, string? Error)> UpdateStationAsync(int stationId, CashierStationUpsertDto dto, User currentUser_IGNORED)
        {
            _logger.LogInformation("Attempting to update cashier station with ID: {StationId}. Incoming DTO: {@Dto}", stationId, dto);
            var station = await _context.CashierStations
                .FirstOrDefaultAsync(cs => cs.Id == stationId);

            if (station == null)
            {
                _logger.LogWarning("Update station failed: Station with ID {StationId} not found.", stationId);
                return (null, "Station not found.");
            }

            if (!await HasAccessToOrganization(station.OrganizationId, new[] { "SuperAdmin", "Admin" }))
            {
                _logger.LogWarning("Unauthorized to update cashier station {StationId} in organization {OrganizationId}.", stationId, station.OrganizationId);
                return (null, "Unauthorized to update this station.");
            }

            var area = await _context.Areas.FindAsync(dto.AreaId);
            if (area == null || area.OrganizationId != station.OrganizationId)
            {
                _logger.LogWarning("Update station failed: Invalid AreaId {AreaId} or Area does not belong to the station's organization {OrganizationId}.", dto.AreaId, station.OrganizationId);
                return (null, "Invalid AreaId or Area does not belong to the station's organization.");
            }

            var printer = await _context.Printers.FindAsync(dto.ReceiptPrinterId);
            if (printer == null || printer.OrganizationId != station.OrganizationId)
            {
                _logger.LogWarning("Update station failed: Invalid ReceiptPrinterId {PrinterId} or Printer does not belong to the station's organization {OrganizationId}.", dto.ReceiptPrinterId, station.OrganizationId);
                return (null, "Invalid ReceiptPrinterId or Printer does not belong to the station's organization.");
            }

            station.AreaId = dto.AreaId.Value;
            station.Name = dto.Name;
            station.ReceiptPrinterId = dto.ReceiptPrinterId.Value;
            station.PrintComandasAtThisStation = dto.PrintComandasAtThisStation;
            station.IsEnabled = dto.IsEnabled;

            await _context.SaveChangesAsync();
            _logger.LogInformation("Cashier station {StationId} updated successfully.", stationId);

            // Re-fetch with includes for the DTO
            var updatedStationWithIncludes = await _context.CashierStations
                .Include(cs => cs.Area)
                .Include(cs => cs.ReceiptPrinter)
                .FirstAsync(cs => cs.Id == station.Id);

            return (MapToDto(updatedStationWithIncludes), null);
        }

        public async Task<(bool Success, string? Error)> DeleteStationAsync(int stationId, User currentUser_IGNORED)
        {
            _logger.LogInformation("Attempting to delete cashier station with ID: {StationId}.", stationId);
            var station = await _context.CashierStations
                .FirstOrDefaultAsync(cs => cs.Id == stationId);

            if (station == null)
            {
                _logger.LogWarning("Delete station failed: Station with ID {StationId} not found.", stationId);
                return (false, "Station not found.");
            }

            if (!await HasAccessToOrganization(station.OrganizationId, new[] { "SuperAdmin", "Admin" }))
            {
                _logger.LogWarning("Unauthorized to delete cashier station {StationId} in organization {OrganizationId}.", stationId, station.OrganizationId);
                return (false, "Unauthorized to delete this station.");
            }
            
            bool isUsedInOrders = await _context.Orders.AnyAsync(o => o.CashierStationId == stationId);
            if (isUsedInOrders) {
                _logger.LogWarning("Delete station failed: Station {StationId} cannot be deleted as it is associated with existing orders.", stationId);
                 return (false, "Station cannot be deleted as it is associated with existing orders. Please disable it instead or reassign orders.");
            }

            _context.CashierStations.Remove(station);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Cashier station {StationId} deleted successfully.", stationId);
            return (true, null);
        }
    }
}
