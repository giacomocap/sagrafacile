using System.ComponentModel.DataAnnotations;

namespace SagraFacile.NET.API.DTOs;

public class LoginDto
{
    [Required]
    [EmailAddress]
    public required string Email { get; set; } // Using Email for login identifier

    [Required]
    [DataType(DataType.Password)]
    public required string Password { get; set; }

    public bool RememberMe { get; set; } = false; // Optional: Add remember me functionality
}
