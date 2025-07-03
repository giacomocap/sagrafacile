using Microsoft.EntityFrameworkCore;
using SagraFacile.NET.API.Data;
using SagraFacile.NET.API.Models;
using SagraFacile.NET.API.Services.Interfaces;

namespace SagraFacile.NET.API.Services
{
    public class SyncConfigurationService : BaseService, ISyncConfigurationService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<SyncConfigurationService> _logger; // Added for logging

        public SyncConfigurationService(
            ApplicationDbContext context,
            IHttpContextAccessor httpContextAccessor,
            ILogger<SyncConfigurationService> logger) // Injected ILogger
            : base(httpContextAccessor)
        {
            _context = context;
            _logger = logger; // Initialize logger
        }

        /// <inheritdoc />
        public async Task<SyncConfiguration?> GetSyncConfigurationAsync(Guid organizationId)
        {
            _logger.LogInformation("Attempting to retrieve sync configuration for Organization ID: {OrganizationId}.", organizationId);
            var (userOrgId, isSuperAdmin) = GetUserContext();
            
            try
            {
                // Authorization check: Only SuperAdmin or users from the same organization can access
                if (!isSuperAdmin && userOrgId != organizationId)
                {
                    _logger.LogWarning("User {UserId} denied access to sync configuration for Organization {OrganizationId}.", GetUserId(), organizationId);
                    throw new UnauthorizedAccessException("User is not authorized to access this sync configuration.");
                }

                var config = await _context.SyncConfigurations
                    .FirstOrDefaultAsync(sc => sc.OrganizationId == organizationId);

                if (config == null)
                {
                    _logger.LogInformation("Sync configuration not found for Organization ID: {OrganizationId}.", organizationId);
                }
                else
                {
                    _logger.LogInformation("Successfully retrieved sync configuration for Organization ID: {OrganizationId}.", organizationId);
                }
                return config;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while getting sync configuration for Organization ID: {OrganizationId}.", organizationId);
                throw; // Re-throw to be handled by controller/middleware
            }
        }

        /// <inheritdoc />
        public async Task<SyncConfiguration> SaveSyncConfigurationAsync(SyncConfiguration syncConfiguration)
        {
            _logger.LogInformation("Attempting to save sync configuration for Organization ID: {OrganizationId}.", syncConfiguration.OrganizationId);
            var (userOrgId, isSuperAdmin) = GetUserContext();
            
            try
            {
                // Authorization check: Only SuperAdmin or users from the same organization can modify
                if (!isSuperAdmin && userOrgId != syncConfiguration.OrganizationId)
                {
                    _logger.LogWarning("User {UserId} denied access to modify sync configuration for Organization {OrganizationId}.", GetUserId(), syncConfiguration.OrganizationId);
                    throw new UnauthorizedAccessException("User is not authorized to modify this sync configuration.");
                }

                // Check if the organization exists
                var organizationExists = await _context.Organizations.AnyAsync(o => o.Id == syncConfiguration.OrganizationId);
                if (!organizationExists)
                {
                    _logger.LogWarning("SaveSyncConfigurationAsync failed: Organization with ID {OrganizationId} does not exist.", syncConfiguration.OrganizationId);
                    throw new ArgumentException($"Organization with ID {syncConfiguration.OrganizationId} does not exist.");
                }

                // Check if a configuration already exists for this organization
                var existingConfig = await _context.SyncConfigurations
                    .FirstOrDefaultAsync(sc => sc.OrganizationId == syncConfiguration.OrganizationId);

                if (existingConfig != null)
                {
                    _logger.LogInformation("Updating existing sync configuration for Organization ID: {OrganizationId}.", syncConfiguration.OrganizationId);
                    // Update existing configuration
                    existingConfig.PlatformBaseUrl = syncConfiguration.PlatformBaseUrl;
                    existingConfig.ApiKey = syncConfiguration.ApiKey;
                    existingConfig.IsEnabled = syncConfiguration.IsEnabled;
                    
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Sync configuration for Organization ID: {OrganizationId} updated successfully.", syncConfiguration.OrganizationId);
                    return existingConfig;
                }
                else
                {
                    _logger.LogInformation("Creating new sync configuration for Organization ID: {OrganizationId}.", syncConfiguration.OrganizationId);
                    // Create new configuration
                    _context.SyncConfigurations.Add(syncConfiguration);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("New sync configuration for Organization ID: {OrganizationId} created successfully.", syncConfiguration.OrganizationId);
                    return syncConfiguration;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while saving sync configuration for Organization ID: {OrganizationId}.", syncConfiguration.OrganizationId);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<bool> DeleteSyncConfigurationAsync(Guid organizationId)
        {
            _logger.LogInformation("Attempting to delete sync configuration for Organization ID: {OrganizationId}.", organizationId);
            var (userOrgId, isSuperAdmin) = GetUserContext();
            
            try
            {
                // Authorization check: Only SuperAdmin or users from the same organization can delete
                if (!isSuperAdmin && userOrgId != organizationId)
                {
                    _logger.LogWarning("User {UserId} denied access to delete sync configuration for Organization {OrganizationId}.", GetUserId(), organizationId);
                    throw new UnauthorizedAccessException("User is not authorized to delete this sync configuration.");
                }

                var configuration = await _context.SyncConfigurations
                    .FirstOrDefaultAsync(sc => sc.OrganizationId == organizationId);

                if (configuration == null)
                {
                    _logger.LogInformation("DeleteSyncConfigurationAsync: Sync configuration not found for Organization ID: {OrganizationId}.", organizationId);
                    return false;
                }

                _context.SyncConfigurations.Remove(configuration);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Sync configuration for Organization ID: {OrganizationId} deleted successfully.", organizationId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while deleting sync configuration for Organization ID: {OrganizationId}.", organizationId);
                throw;
            }
        }
    }
}
