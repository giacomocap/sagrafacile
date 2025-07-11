using System;
using System.ComponentModel.DataAnnotations;

namespace SagraFacile.NET.API.Models
{
    public class UserInvitation
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Token { get; set; } = string.Empty;

        [Required]
        public Guid OrganizationId { get; set; }
        public virtual Organization? Organization { get; set; }

        [Required]
        public string Roles { get; set; } = string.Empty; // Comma-separated list of roles

        [Required]
        public DateTime ExpiryDate { get; set; }

        public bool IsUsed { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
