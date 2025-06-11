using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging; // Added for ILogger
using SagraFacile.NET.API.Data;
using SagraFacile.NET.API.DTOs;
using SagraFacile.NET.API.Models;
using SagraFacile.NET.API.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http; // Added for IHttpContextAccessor

namespace SagraFacile.NET.API.Services
{
    public class DayService : BaseService, IDayService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<DayService> _logger;
        // No SignalR needed for DayService currently

        public DayService(
            ApplicationDbContext context,
            IHttpContextAccessor httpContextAccessor,
            ILogger<DayService> logger)
            : base(httpContextAccessor) // Call base constructor
        {
            _context = context;
            _logger = logger;
        }

        public async Task<DayDto?> GetCurrentOpenDayAsync(int organizationId)
        {
            // Authorization check: Ensure the requesting user has access to this organization
            var (userOrganizationId, isSuperAdmin) = GetUserContext();
            if (!isSuperAdmin && organizationId != userOrganizationId)
            {
                // Silently return null if not authorized, mimicking not found
                _logger.LogWarning("User denied access to get current open day for organization {OrganizationId}.", organizationId);
                return null;
            }

            var openDay = await _context.Days
                .Include(d => d.OpenedByUser) // Include user details for DTO
                .Include(d => d.ClosedByUser) // Include user details for DTO
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.OrganizationId == organizationId && d.Status == DayStatus.Open);

            if (openDay == null)
            {
                return null;
            }

            return MapDayToDto(openDay);
        }

        public async Task<DayDto?> GetPublicCurrentOpenDayAsync(int organizationId)
        {
            var openDay = await _context.Days
                .Include(d => d.OpenedByUser)
                .Include(d => d.ClosedByUser)
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.OrganizationId == organizationId && d.Status == DayStatus.Open);

            if (openDay == null)
            {
                return null;
            }

            return MapDayToDto(openDay);
        }

        // New method called by Controller
        public async Task<DayDto?> GetCurrentOpenDayForUserAsync(ClaimsPrincipal user) // Keep user param here as it's passed from controller
        {
            // BaseService methods GetUserContext() and GetUserId() use HttpContextAccessor, no user param needed
            var (userOrganizationId, isSuperAdmin) = GetUserContext();

            if (!userOrganizationId.HasValue)
            {
                // This case should ideally be caught by [Authorize] but double-check context
                 _logger.LogWarning("GetCurrentOpenDayForUserAsync: Could not determine organization context for user {UserId}.", GetUserId()); // Removed user param
                throw new UnauthorizedAccessException("Organization context could not be determined for the user.");
            }

            // SuperAdmins can see the current day of their *current* context org
            // Non-admins can see the current day of *their* org
            // Call the existing method which handles the actual query
            return await GetCurrentOpenDayAsync(userOrganizationId.Value);
        }


        public async Task<DayDto> OpenDayAsync(ClaimsPrincipal user) // Keep user param here as it's passed from controller
        {
            // BaseService methods GetUserContext() and GetUserId() use HttpContextAccessor, no user param needed
            var (userOrganizationId, isSuperAdmin) = GetUserContext();
            var authenticatedUserId = GetUserId(); // Removed user param

            if (string.IsNullOrEmpty(authenticatedUserId) || !userOrganizationId.HasValue)
            {
                throw new UnauthorizedAccessException("User is not authenticated or does not belong to an organization.");
            }

            // Explicitly forbid SuperAdmins from opening a day without specific context
            // The controller already checks this, but belt-and-suspenders approach.
            if (isSuperAdmin)
            {
                _logger.LogWarning("SuperAdmin {UserId} attempted to open a day via service.", authenticatedUserId);
                throw new UnauthorizedAccessException("SuperAdmins cannot open a day directly via this method. Operate within an organization context.");
            }

            // Check if a day is already open for this organization
            bool isDayAlreadyOpen = await _context.Days
                .AnyAsync(d => d.OrganizationId == userOrganizationId.Value && d.Status == DayStatus.Open);

            if (isDayAlreadyOpen)
            {
                _logger.LogWarning("Attempted to open a new day for organization {OrganizationId} when one is already open.", userOrganizationId.Value);
                throw new InvalidOperationException($"A day is already open for organization ID {userOrganizationId.Value}.");
            }

            // Create and save the new Day
            var newDay = new Day
            {
                OrganizationId = userOrganizationId.Value,
                StartTime = DateTime.UtcNow,
                Status = DayStatus.Open,
                OpenedByUserId = authenticatedUserId
                // EndTime, ClosedByUserId, TotalSales are null/default initially
            };

            _context.Days.Add(newDay);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Opened new day {DayId} for organization {OrganizationId} by user {UserId}.", newDay.Id, newDay.OrganizationId, newDay.OpenedByUserId);

            // Fetch the user details for the DTO
            var opener = await _context.Users.FindAsync(authenticatedUserId);
            newDay.OpenedByUser = opener!; // Assign for mapping

            return MapDayToDto(newDay);
        }

        // Refactored to use ClaimsPrincipal for context/auth
        public async Task<DayDto> CloseDayAsync(int dayId, ClaimsPrincipal user) // Keep user param here as it's passed from controller
        {
            // BaseService methods GetUserContext() and GetUserId() use HttpContextAccessor, no user param needed
            var (userOrganizationId, isSuperAdmin) = GetUserContext();
            var authenticatedUserId = GetUserId(); // Removed user param

            if (string.IsNullOrEmpty(authenticatedUserId) || !userOrganizationId.HasValue) // Check OrgId too
            {
                throw new UnauthorizedAccessException("User is not authenticated or does not belong to an organization.");
            }

            // Explicitly forbid SuperAdmins from closing a day without specific context
            if (isSuperAdmin)
            {
                _logger.LogWarning("SuperAdmin {UserId} attempted to close day {DayId} via service.", authenticatedUserId, dayId);
                throw new UnauthorizedAccessException("SuperAdmins cannot close a day directly via this method. Operate within an organization context.");
            }

            // Fetch the day, including related user data for DTO mapping
            var dayToClose = await _context.Days
                .Include(d => d.OpenedByUser)
                .Include(d => d.ClosedByUser) // Include even if null initially
                .FirstOrDefaultAsync(d => d.Id == dayId);

            if (dayToClose == null)
            {
                throw new KeyNotFoundException($"Day with ID {dayId} not found.");
            }

            // Authorization check
            if (!isSuperAdmin && dayToClose.OrganizationId != userOrganizationId)
            {
                _logger.LogWarning("User {UserId} denied access to close day {DayId} belonging to organization {OrganizationId}.", authenticatedUserId, dayId, dayToClose.OrganizationId);
                throw new UnauthorizedAccessException($"Access denied to close day ID {dayId}.");
            }

            if (dayToClose.Status == DayStatus.Closed)
            {
                _logger.LogWarning("Attempted to close day {DayId} which is already closed.", dayId);
                throw new InvalidOperationException($"Day ID {dayId} is already closed.");
            }

            // Calculate TotalSales for the day before closing
            // Sum the TotalAmount of all 'Paid' orders associated with this DayId
            // Note: This assumes 'Paid' is the final state for sales calculation.
            // If other statuses contribute (e.g., partially paid?), adjust the filter.
            dayToClose.TotalSales = await _context.Orders
                .Where(o => o.DayId == dayId && o.Status == OrderStatus.Paid)
                .SumAsync(o => o.TotalAmount); // Sum the TotalAmount directly

             _logger.LogInformation("Calculated TotalSales for Day {DayId}: {TotalSales}", dayId, dayToClose.TotalSales);

            // Update the Day entity
            dayToClose.Status = DayStatus.Closed;
            dayToClose.EndTime = DateTime.UtcNow;
            dayToClose.ClosedByUserId = authenticatedUserId;

            _context.Days.Update(dayToClose);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Closed day {DayId} for organization {OrganizationId} by user {UserId}.", dayToClose.Id, dayToClose.OrganizationId, dayToClose.ClosedByUserId);

            // Fetch the closer user details for the DTO
            var closer = await _context.Users.FindAsync(authenticatedUserId);
            dayToClose.ClosedByUser = closer; // Assign for mapping

            return MapDayToDto(dayToClose);
        }

        public async Task<IEnumerable<DayDto>> GetDaysAsync(int organizationId, DateTime? startDate, DateTime? endDate, ClaimsPrincipal user) // Keep user param here as it's passed from controller/other services
        {
            // BaseService methods GetUserContext() and GetUserId() use HttpContextAccessor, no user param needed
            var (userOrganizationId, isSuperAdmin) = GetUserContext();

            // Authorization check
            if (!isSuperAdmin && organizationId != userOrganizationId)
            {
                _logger.LogWarning("User denied access to get days for organization {OrganizationId}.", organizationId);
                // Throw or return empty list? Let's throw for admin functions.
                throw new UnauthorizedAccessException($"Access denied to view days for organization ID {organizationId}.");
            }

            var query = _context.Days
                .Where(d => d.OrganizationId == organizationId)
                .Include(d => d.OpenedByUser) // Include user details
                .Include(d => d.ClosedByUser) // Include user details
                .AsNoTracking(); // Define base query without ordering first

            // Apply date filters if provided
            if (startDate.HasValue)
            {
                query = query.Where(d => d.StartTime >= startDate.Value.Date); // Compare date part only
            }
            if (endDate.HasValue)
            {
                // Include the whole end day
                query = query.Where(d => d.StartTime < endDate.Value.Date.AddDays(1));
            }

            // Apply ordering *after* filtering
            var orderedQuery = query.OrderByDescending(d => d.StartTime); // Show newest first

            var days = await orderedQuery.ToListAsync(); // Execute the ordered query

            return days.Select(MapDayToDto).ToList();
        }

        public async Task<DayDto?> GetDayByIdAsync(int dayId, ClaimsPrincipal user) // Keep user param here as it's passed from controller/other services
        {
            // BaseService methods GetUserContext() and GetUserId() use HttpContextAccessor, no user param needed
            var (userOrganizationId, isSuperAdmin) = GetUserContext();

            var day = await _context.Days
                .Include(d => d.OpenedByUser)
                .Include(d => d.ClosedByUser)
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == dayId);

            if (day == null)
            {
                return null; // Not found
            }

            // Authorization check
            if (!isSuperAdmin && day.OrganizationId != userOrganizationId)
            {
                _logger.LogWarning("User denied access to view day {DayId}.", dayId);
                // Return null as if not found for security
                return null;
            }

            return MapDayToDto(day);
        }

        // New method called by Controller
        public async Task<IEnumerable<DayDto>> GetDaysForUserAsync(ClaimsPrincipal user) // Keep user param here as it's passed from controller
        {
            // BaseService methods GetUserContext() and GetUserId() use HttpContextAccessor, no user param needed
            var (userOrganizationId, isSuperAdmin) = GetUserContext();
            var authenticatedUserId = GetUserId(); // Get user ID once

            if (!userOrganizationId.HasValue)
            {
                _logger.LogWarning("GetDaysForUserAsync: Could not determine organization context for user {UserId}.", authenticatedUserId); // Use variable
                throw new UnauthorizedAccessException("Organization context could not be determined for the user.");
            }

            // Explicitly forbid SuperAdmins from listing days without specific context via this method
            if (isSuperAdmin)
            {
                _logger.LogWarning("SuperAdmin {UserId} attempted to list days via GetDaysForUserAsync.", authenticatedUserId); // Use variable
                throw new UnauthorizedAccessException("SuperAdmins cannot list days directly via this method. Use GetDaysAsync with an organization ID.");
            }

            // Call the existing GetDaysAsync, passing the determined orgId and null date filters
            // This reuses the query logic including Includes and ordering.
            return await GetDaysAsync(userOrganizationId.Value, null, null, user);
        }

        // New method called by Controller (functionally same as GetDayByIdAsync but clearer naming)
        public async Task<DayDto?> GetDayByIdForUserAsync(int dayId, ClaimsPrincipal user) // Keep user param here as it's passed from controller
        {
             // BaseService methods GetUserContext() and GetUserId() use HttpContextAccessor, no user param needed
             var (userOrganizationId, isSuperAdmin) = GetUserContext();
             var authenticatedUserId = GetUserId(); // Get user ID once

            if (!userOrganizationId.HasValue)
            {
                _logger.LogWarning("GetDayByIdForUserAsync: Could not determine organization context for user {UserId}.", authenticatedUserId); // Use variable
                throw new UnauthorizedAccessException("Organization context could not be determined for the user.");
            }

             // Explicitly forbid SuperAdmins from getting a day without specific context via this method
            if (isSuperAdmin)
            {
                 _logger.LogWarning("SuperAdmin {UserId} attempted to get day {DayId} via GetDayByIdForUserAsync.", authenticatedUserId, dayId); // Use variable
                 throw new UnauthorizedAccessException("SuperAdmins cannot get a day directly via this method. Operate within an organization context.");
            }

            // Call the existing GetDayByIdAsync which already handles the logic
            // including the check if the day belongs to the user's organization.
            return await GetDayByIdAsync(dayId, user);
        }


        // --- Helper Methods ---

        private DayDto MapDayToDto(Day day)
        {
            return new DayDto
            {
                Id = day.Id,
                OrganizationId = day.OrganizationId,
                StartTime = day.StartTime,
                EndTime = day.EndTime,
                Status = day.Status,
                OpenedByUserId = day.OpenedByUserId,
                OpenedByUserFirstName = day.OpenedByUser?.FirstName, // Handle potential null navigation property
                OpenedByUserLastName = day.OpenedByUser?.LastName,
                ClosedByUserId = day.ClosedByUserId,
                ClosedByUserFirstName = day.ClosedByUser?.FirstName, // Handle potential null navigation property
                ClosedByUserLastName = day.ClosedByUser?.LastName,
                TotalSales = day.TotalSales
            };
        }
    }
}
