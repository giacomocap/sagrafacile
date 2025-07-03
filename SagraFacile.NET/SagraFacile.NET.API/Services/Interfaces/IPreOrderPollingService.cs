using SagraFacile.NET.API.Models;

namespace SagraFacile.NET.API.Services.Interfaces
{
    /// <summary>
    /// Service responsible for polling the SagraPreOrdine platform for new preorders,
    /// importing them, and marking them as fetched on the platform.
    /// </summary>
    public interface IPreOrderPollingService
    {
        /// <summary>
        /// Fetches new preorders for a specific organization from the platform,
        /// imports them into the local database, and marks them as fetched.
        /// </summary>
        /// <param name="organizationId">The ID of the organization to poll for.</param>
        /// <param name="syncConfig">The sync configuration for the organization.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task PollAndImportPreOrdersAsync(Guid organizationId, SyncConfiguration syncConfig, CancellationToken cancellationToken);
    }
}
