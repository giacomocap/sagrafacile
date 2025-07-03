using System.Collections.Generic;

namespace SagraFacile.NET.API.DTOs;

public class UserDto
{
    public string Id { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool EmailConfirmed { get; set; }
    public List<string> Roles { get; set; } = new List<string>();
    // Add OrganizationId/Name if needed, depending on SuperAdmin view requirements
    public Guid OrganizationId { get; set; }
    // public string OrganizationName { get; set; } = string.Empty;
}
