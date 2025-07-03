using System.ComponentModel.DataAnnotations;

namespace SagraFacile.NET.API.Models
{
    public class SyncConfiguration
    {
        public int Id { get; set; }

        [Required]
        [StringLength(255)]
        public required string PlatformBaseUrl { get; set; }

        [Required]
        [StringLength(255)]
        public required string ApiKey { get; set; }

        public bool IsEnabled { get; set; } = true;

        // Foreign key for Organization
        public Guid OrganizationId { get; set; }

        // Navigation property
        public virtual Organization Organization { get; set; } = null!;
    }
}
