using System.ComponentModel.DataAnnotations;

namespace SagraFacile.NET.API.DTOs
{
    public class CreateRoleDto
    {
        [Required]
        [StringLength(256, MinimumLength = 1, ErrorMessage = "Role name must be between 1 and 256 characters.")] // Standard Identity max length
        public required string RoleName { get; set; }
    }
}
