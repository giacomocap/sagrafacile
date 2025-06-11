using System.ComponentModel.DataAnnotations;

namespace SagraFacile.NET.API.DTOs
{
    /// <summary>
    /// Data Transfer Object for SyncConfiguration
    /// </summary>
    public class SyncConfigurationDto
    {
        /// <summary>
        /// The ID of the SyncConfiguration
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// The ID of the Organization this configuration belongs to
        /// </summary>
        public int OrganizationId { get; set; }

        /// <summary>
        /// The base URL of the SagraPreOrdine platform
        /// </summary>
        [Required]
        [StringLength(255)]
        public required string PlatformBaseUrl { get; set; }

        /// <summary>
        /// The API key for authentication with the SagraPreOrdine platform
        /// </summary>
        [Required]
        [StringLength(255)]
        public required string ApiKey { get; set; }

        /// <summary>
        /// Whether synchronization is enabled
        /// </summary>
        public bool IsEnabled { get; set; } = true;
    }

    /// <summary>
    /// Data Transfer Object for creating or updating a SyncConfiguration
    /// </summary>
    public class SyncConfigurationUpsertDto
    {
        /// <summary>
        /// The base URL of the SagraPreOrdine platform
        /// </summary>
        [Required]
        [StringLength(255)]
        public required string PlatformBaseUrl { get; set; }

        /// <summary>
        /// The API key for authentication with the SagraPreOrdine platform
        /// </summary>
        [Required]
        [StringLength(255)]
        public required string ApiKey { get; set; }

        /// <summary>
        /// Whether synchronization is enabled
        /// </summary>
        public bool IsEnabled { get; set; } = true;
    }
}
