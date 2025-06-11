using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SagraFacile.NET.API.Data;
using SagraFacile.NET.API.Models;
using SagraFacile.NET.API.Services.Interfaces;

namespace SagraFacile.NET.API.Services
{
    public class SyncConfigurationService : BaseService, ISyncConfigurationService
    {
        private readonly ApplicationDbContext _context;

        public SyncConfigurationService(
            ApplicationDbContext context,
            IHttpContextAccessor httpContextAccessor)
            : base(httpContextAccessor)
        {
            _context = context;
        }

        /// <inheritdoc />
        public async Task<SyncConfiguration?> GetSyncConfigurationAsync(int organizationId)
        {
            var (userOrgId, isSuperAdmin) = GetUserContext();
            
            // Authorization check: Only SuperAdmin or users from the same organization can access
            if (!isSuperAdmin && userOrgId != organizationId)
            {
                throw new UnauthorizedAccessException("User is not authorized to access this sync configuration.");
            }

            return await _context.SyncConfigurations
                .FirstOrDefaultAsync(sc => sc.OrganizationId == organizationId);
        }

        /// <inheritdoc />
        public async Task<SyncConfiguration> SaveSyncConfigurationAsync(SyncConfiguration syncConfiguration)
        {
            var (userOrgId, isSuperAdmin) = GetUserContext();
            
            // Authorization check: Only SuperAdmin or users from the same organization can modify
            if (!isSuperAdmin && userOrgId != syncConfiguration.OrganizationId)
            {
                throw new UnauthorizedAccessException("User is not authorized to modify this sync configuration.");
            }

            // Check if the organization exists
            var organizationExists = await _context.Organizations.AnyAsync(o => o.Id == syncConfiguration.OrganizationId);
            if (!organizationExists)
            {
                throw new ArgumentException($"Organization with ID {syncConfiguration.OrganizationId} does not exist.");
            }

            // Check if a configuration already exists for this organization
            var existingConfig = await _context.SyncConfigurations
                .FirstOrDefaultAsync(sc => sc.OrganizationId == syncConfiguration.OrganizationId);

            if (existingConfig != null)
            {
                // Update existing configuration
                existingConfig.PlatformBaseUrl = syncConfiguration.PlatformBaseUrl;
                existingConfig.ApiKey = syncConfiguration.ApiKey;
                existingConfig.IsEnabled = syncConfiguration.IsEnabled;
                
                await _context.SaveChangesAsync();
                return existingConfig;
            }
            else
            {
                // Create new configuration
                _context.SyncConfigurations.Add(syncConfiguration);
                await _context.SaveChangesAsync();
                return syncConfiguration;
            }
        }

        /// <inheritdoc />
        public async Task<bool> DeleteSyncConfigurationAsync(int organizationId)
        {
            var (userOrgId, isSuperAdmin) = GetUserContext();
            
            // Authorization check: Only SuperAdmin or users from the same organization can delete
            if (!isSuperAdmin && userOrgId != organizationId)
            {
                throw new UnauthorizedAccessException("User is not authorized to delete this sync configuration.");
            }

            var configuration = await _context.SyncConfigurations
                .FirstOrDefaultAsync(sc => sc.OrganizationId == organizationId);

            if (configuration == null)
            {
                return false;
            }

            _context.SyncConfigurations.Remove(configuration);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
