using SagraFacile.NET.API.Models;

namespace SagraFacile.NET.API.Services.Interfaces
{
    public interface ISyncConfigurationService
    {
        /// <summary>
        /// Gets the sync configuration for an organization
        /// </summary>
        /// <param name="organizationId">The organization ID</param>
        /// <returns>The sync configuration, or null if not configured</returns>
        Task<SyncConfiguration?> GetSyncConfigurationAsync(int organizationId);

        /// <summary>
        /// Creates or updates the sync configuration for an organization
        /// </summary>
        /// <param name="syncConfiguration">The sync configuration to save</param>
        /// <returns>The saved sync configuration</returns>
        Task<SyncConfiguration> SaveSyncConfigurationAsync(SyncConfiguration syncConfiguration);

        /// <summary>
        /// Deletes the sync configuration for an organization
        /// </summary>
        /// <param name="organizationId">The organization ID</param>
        /// <returns>True if deleted, false if not found</returns>
        Task<bool> DeleteSyncConfigurationAsync(int organizationId);
    }
}
