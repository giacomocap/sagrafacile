using Microsoft.AspNetCore.Http;
using System;
using System.Security.Claims;

namespace SagraFacile.NET.API.Services;

public abstract class BaseService
{
    protected readonly IHttpContextAccessor _httpContextAccessor;

    protected BaseService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
    }

    /// <summary>
    /// Gets the Organization ID and SuperAdmin status for the current authenticated user.
    /// </summary>
    /// <returns>A tuple containing the nullable OrganizationId and a boolean indicating if the user is a SuperAdmin.</returns>
    /// <exception cref="UnauthorizedAccessException">Thrown if the user is not authenticated or essential claims are missing.</exception>
    /// <exception cref="InvalidOperationException">Thrown if an authenticated non-SuperAdmin user lacks a valid organizationId claim.</exception>
    protected (int? OrganizationId, bool IsSuperAdmin) GetUserContext()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user == null || !user.Identity!.IsAuthenticated)
        {
            // Should not happen if [Authorize] is used correctly, but good practice
            throw new UnauthorizedAccessException("User is not authenticated.");
        }

        var isSuperAdmin = user.IsInRole("SuperAdmin");
        var orgIdClaim = user.FindFirstValue("organizationId");

        if (isSuperAdmin)
        {
            // SuperAdmin can see all, doesn't strictly *belong* to one org in this context
            return (null, true);
        }

        if (int.TryParse(orgIdClaim, out var orgId))
        {
            return (orgId, false);
        }

            // This indicates a problem - authenticated user without an organizationId claim (and not SuperAdmin)
            throw new InvalidOperationException("User organization claim is missing or invalid.");
        }

        /// <summary>
    /// Gets the User ID for the current authenticated user.
    /// </summary>
    /// <returns>The user's ID (GUID as string).</returns>
    /// <exception cref="UnauthorizedAccessException">Thrown if the user is not authenticated or the sub claim is missing.</exception>
    protected string GetUserId()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user == null || !user.Identity!.IsAuthenticated)
        {
            throw new UnauthorizedAccessException("User is not authenticated.");
        }

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub"); // "sub" is often used in JWT

        if (string.IsNullOrEmpty(userId))
        {
             throw new UnauthorizedAccessException("User ID claim (sub or NameIdentifier) is missing.");
        }
        return userId;
    }
}
