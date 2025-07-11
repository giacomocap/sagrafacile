namespace SagraFacile.NET.API.DTOs;

public class PendingInvitationDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Roles { get; set; } = string.Empty;
    public DateTime ExpiryDate { get; set; }
    public DateTime InvitedAt { get; set; }
}
