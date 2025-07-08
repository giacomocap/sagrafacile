using Microsoft.AspNetCore.Identity;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations; // Added for Required, StringLength
using System.ComponentModel.DataAnnotations.Schema;

namespace SagraFacile.NET.API.Models
{
    public enum UserStatus
    {
        Active,
        PendingDeletion,
        Deleted // Represents a user that has been soft-deleted
    }

    // Add profile data for application users by adding properties to the User class
    public class User : IdentityUser
    {
        // Add custom properties
        [Required]
        [StringLength(50)]
        public required string FirstName { get; set; }

        [Required]
        [StringLength(50)]
        public required string LastName { get; set; }

        public UserStatus Status { get; set; } = UserStatus.Active;

        // Foreign Key for Organization
        public Guid? OrganizationId { get; set; }

        // Navigation property for Organization
        [ForeignKey("OrganizationId")]
        public virtual Organization? Organization { get; set; }

        // Navigation property for Orders handled by this cashier
        public virtual ICollection<Order> HandledOrders { get; set; } = new List<Order>();

        // Refresh Token fields
        public string? RefreshToken { get; set; }
        public DateTime? RefreshTokenExpiryTime { get; set; }

        // Field for soft-delete functionality
        public DateTime? DeletionScheduledAt { get; set; }
    }
}
