using System.ComponentModel.DataAnnotations;

namespace SagraFacile.NET.API.DTOs;

public class RegisterDto
{
    [Required]
    [EmailAddress]
    public required string Email { get; set; }

    [Required]
    [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
    [DataType(DataType.Password)]
    public required string Password { get; set; }

    [DataType(DataType.Password)]
    [Display(Name = "Confirm password")]
    [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
    public required string ConfirmPassword { get; set; }

    [Required]
    public required string FirstName { get; set; } // Assuming User model has FirstName

    [Required]
    public required string LastName { get; set; } // Assuming User model has LastName

    // Add OrganizationId if users must be tied to an organization upon registration
    // This is primarily for SuperAdmins to specify the organization.
    // OrgAdmins will have their own OrganizationId assigned automatically.
    public int? OrganizationId { get; set; }
}
