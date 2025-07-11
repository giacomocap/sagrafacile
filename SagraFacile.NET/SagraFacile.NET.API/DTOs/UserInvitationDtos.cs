using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SagraFacile.NET.API.DTOs
{
    public class UserInvitationRequestDto
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MinLength(1)]
        public List<string> Roles { get; set; } = new List<string>();
    }

    public class AcceptInvitationDto
    {
        [Required]
        public string Token { get; set; } = string.Empty;

        [Required]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        public string LastName { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public class InvitationDetailsDto
    {
        public string Email { get; set; } = string.Empty;
        public string OrganizationName { get; set; } = string.Empty;
    }
}
