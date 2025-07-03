using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SagraFacile.NET.API.Models
{
    public class Organization
    {
        public Guid Id { get; set; }

        [Required]
        [StringLength(100)]
        public required string Name { get; set; }

        // URL-friendly identifier
        [StringLength(100)] // Keep length consistent with Name
        public string Slug { get; set; } = string.Empty; // Will be configured as required and unique in DbContext

        // New field for SaaS subscription status
        [StringLength(50)] // e.g., "Trial", "Active", "Expired", "Cancelled"
        public string SubscriptionStatus { get; set; } = "Trial"; // Default to "Trial"

        // Navigation properties
        public virtual ICollection<Area> Areas { get; set; } = new List<Area>();
        public virtual ICollection<User> Users { get; set; } = new List<User>();
        public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
        public virtual SyncConfiguration? SyncConfiguration { get; set; }
    }
}
