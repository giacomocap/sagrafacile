using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using SagraFacile.NET.API.DTOs;
using SagraFacile.NET.API.Models; // For Day entity

namespace SagraFacile.NET.API.Services.Interfaces
{
    public interface IDayService
    {
        /// <summary>
        /// Gets the currently open Day for a specific organization.
        /// </summary>
        /// <param name="organizationId">The ID of the organization.</param>
        /// <returns>The DayDto of the open Day, or null if none is open.</returns>
        Task<DayDto?> GetCurrentOpenDayAsync(Guid organizationId); // Keep for internal service use (e.g., OrderService)

        /// <summary>
        /// Gets the currently open Day for a specific organization without requiring user authentication.
        /// For public-facing endpoints.
        /// </summary>
        /// <param name="organizationId">The ID of the organization.</param>
        /// <returns>The DayDto of the open Day, or null if none is open.</returns>
        Task<DayDto?> GetPublicCurrentOpenDayAsync(Guid organizationId);

        /// <summary>
        /// Gets the currently open Day for the organization associated with the calling user.
        /// </summary>
        /// <param name="user">The ClaimsPrincipal representing the authenticated user.</param>
        /// <returns>The DayDto of the open Day, or null if none is open or access denied.</returns>
        /// <exception cref="UnauthorizedAccessException">Thrown if the user's organization context cannot be determined.</exception>
        Task<DayDto?> GetCurrentOpenDayForUserAsync(ClaimsPrincipal user);

        /// <summary>
        /// Opens a new Day for the organization associated with the calling user.
        /// Ensures only one Day can be open per organization at a time.
        /// </summary>
        /// <param name="user">The ClaimsPrincipal representing the authenticated user opening the Day.</param>
        /// <returns>The DayDto of the newly opened Day.</returns>
        /// <exception cref="InvalidOperationException">Thrown if a Day is already open for the organization.</exception>
        /// <exception cref="UnauthorizedAccessException">Thrown if the user is not authorized or doesn't belong to an organization.</exception>
        Task<DayDto> OpenDayAsync(ClaimsPrincipal user);

        /// <summary>
        /// Closes the specified Day.
        /// </summary>
        /// <param name="dayId">The ID of the Day to close.</param>
        /// <param name="user">The ClaimsPrincipal representing the authenticated user closing the Day.</param>
        /// <returns>The DayDto of the closed Day.</returns>
        /// <exception cref="KeyNotFoundException">Thrown if the Day with the specified ID is not found.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the Day is already closed.</exception>
        /// <exception cref="UnauthorizedAccessException">Thrown if the user is not authorized to close this Day.</exception>
        Task<DayDto> CloseDayAsync(int dayId, ClaimsPrincipal user);

        /// <summary>
        /// Gets a list of Days for a specific organization, optionally filtered by date range.
        /// Primarily for Admin use.
        /// </summary>
        /// <param name="organizationId">The ID of the organization.</param>
        /// <param name="startDate">Optional start date filter.</param>
        /// <param name="endDate">Optional end date filter.</param>
        /// <param name="user">The ClaimsPrincipal representing the authenticated user requesting the data.</param>
        /// <returns>A list of DayDto objects.</returns>
        /// <exception cref="UnauthorizedAccessException">Thrown if the user is not authorized to view Days for this organization.</exception>
        Task<IEnumerable<DayDto>> GetDaysAsync(Guid organizationId, DateTime? startDate, DateTime? endDate, ClaimsPrincipal user); // Keep for potential future admin filtering

        /// <summary>
        /// Gets a list of Days for the organization associated with the calling user.
        /// Primarily for Admin use.
        /// </summary>
        /// <param name="user">The ClaimsPrincipal representing the authenticated user requesting the data.</param>
        /// <returns>A list of DayDto objects.</returns>
        /// <exception cref="UnauthorizedAccessException">Thrown if the user is not authorized (Admin role required) or context cannot be determined.</exception>
        Task<IEnumerable<DayDto>> GetDaysForUserAsync(ClaimsPrincipal user);

        /// <summary>
        /// Gets a specific Day by its ID.
        /// Primarily for Admin use.
        /// </summary>
        /// <param name="dayId">The ID of the Day to retrieve.</param>
        /// <param name="user">The ClaimsPrincipal representing the authenticated user requesting the data.</param>
        /// <returns>The DayDto object or null if not found or access denied.</returns>
        /// <exception cref="UnauthorizedAccessException">Thrown if the user is not authorized to view this Day or context cannot be determined.</exception>
        Task<DayDto?> GetDayByIdForUserAsync(int dayId, ClaimsPrincipal user); // Renamed for clarity to match controller usage pattern
    }
}

// Note: The original GetDayByIdAsync(int dayId, ClaimsPrincipal user) signature is functionally identical
// to GetDayByIdForUserAsync. We'll implement GetDayByIdForUserAsync and potentially remove/deprecate
// the old one later if nothing else uses it directly. For now, the controller calls GetDayByIdForUserAsync.
