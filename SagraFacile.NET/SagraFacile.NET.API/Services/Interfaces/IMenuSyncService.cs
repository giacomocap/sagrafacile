namespace SagraFacile.NET.API.Services.Interfaces
{
    public interface IMenuSyncService
    {
        /// <summary>
        /// Synchronizes the menu data (Areas, Categories, Menu Items) with the SagraPreOrdine platform
        /// </summary>
        /// <param name="organizationId">The organization ID</param>
        /// <returns>A result object containing success status and any error messages</returns>
        Task<MenuSyncResult> SyncMenuAsync(int organizationId);
    }

    /// <summary>
    /// Represents the result of a menu synchronization operation
    /// </summary>
    public class MenuSyncResult
    {
        /// <summary>
        /// Indicates whether the synchronization was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// The HTTP status code returned by the API (if applicable)
        /// </summary>
        public int? StatusCode { get; set; }

        /// <summary>
        /// Error message (if any)
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Detailed error information (if available)
        /// </summary>
        public string? ErrorDetails { get; set; }

        /// <summary>
        /// Creates a successful result
        /// </summary>
        public static MenuSyncResult CreateSuccess()
        {
            return new MenuSyncResult
            {
                Success = true
            };
        }

        /// <summary>
        /// Creates a failed result with the specified error message
        /// </summary>
        public static MenuSyncResult CreateFailure(string errorMessage, string? errorDetails = null, int? statusCode = null)
        {
            return new MenuSyncResult
            {
                Success = false,
                ErrorMessage = errorMessage,
                ErrorDetails = errorDetails,
                StatusCode = statusCode
            };
        }
    }
}
