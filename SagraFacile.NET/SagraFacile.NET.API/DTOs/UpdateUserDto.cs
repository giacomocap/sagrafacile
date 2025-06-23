using System.ComponentModel.DataAnnotations;

namespace SagraFacile.NET.API.DTOs
{
    // This DTO is used for PATCH-like updates, so properties are optional.
    public class UpdateUserDto
    {
        [StringLength(50, ErrorMessage = "First name cannot be longer than 50 characters.")]
        public string? FirstName { get; set; }

        [StringLength(50, ErrorMessage = "Last name cannot be longer than 50 characters.")]
        public string? LastName { get; set; }

        [EmailAddress]
        [StringLength(256, ErrorMessage = "Email cannot be longer than 256 characters.")] // Standard Identity max length
        public string? Email { get; set; }

        // Note: Password changes and role assignments are handled via separate endpoints/DTOs.
        // OrganizationId changes are typically restricted or handled differently.
    }
}
