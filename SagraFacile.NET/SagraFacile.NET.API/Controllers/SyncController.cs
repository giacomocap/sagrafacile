using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SagraFacile.NET.API.DTOs;
using SagraFacile.NET.API.Models;
using SagraFacile.NET.API.Services.Interfaces;

namespace SagraFacile.NET.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] // Require authentication for all endpoints
    public class SyncController : ControllerBase
    {
        private readonly ISyncConfigurationService _syncConfigurationService;
        private readonly IMenuSyncService _menuSyncService;
        private readonly ILogger<SyncController> _logger;

        public SyncController(
            ISyncConfigurationService syncConfigurationService,
            IMenuSyncService menuSyncService,
            ILogger<SyncController> logger)
        {
            _syncConfigurationService = syncConfigurationService;
            _menuSyncService = menuSyncService;
            _logger = logger;
        }

        /// <summary>
        /// Gets the sync configuration for an organization
        /// </summary>
        /// <param name="organizationId">The organization ID</param>
        /// <returns>The sync configuration, or 404 if not found</returns>
        [HttpGet("organizations/{organizationId}/config")]
        [Authorize(Roles = "Admin,SuperAdmin")] // Only Admin or SuperAdmin can access
        public async Task<ActionResult<SyncConfigurationDto>> GetSyncConfiguration(int organizationId)
        {
            _logger.LogInformation("Received request to get sync configuration for organization {OrganizationId}.", organizationId);
            try
            {
                var config = await _syncConfigurationService.GetSyncConfigurationAsync(organizationId);
                if (config == null)
                {
                    _logger.LogWarning("Sync configuration for organization {OrganizationId} not found.", organizationId);
                    return NotFound();
                }
                
                // Map the model to DTO
                var configDto = new SyncConfigurationDto
                {
                    Id = config.Id,
                    OrganizationId = config.OrganizationId,
                    PlatformBaseUrl = config.PlatformBaseUrl,
                    ApiKey = config.ApiKey,
                    IsEnabled = config.IsEnabled
                };
                
                _logger.LogInformation("Successfully retrieved sync configuration for organization {OrganizationId}.", organizationId);
                return Ok(configDto);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized access attempt to get sync configuration for organization {OrganizationId}", organizationId);
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting sync configuration for organization {OrganizationId}", organizationId);
                return StatusCode(500, "An error occurred while retrieving the sync configuration.");
            }
        }

        /// <summary>
        /// Creates or updates the sync configuration for an organization
        /// </summary>
        /// <param name="organizationId">The organization ID</param>
        /// <param name="configDto">The sync configuration to save</param>
        /// <returns>The saved sync configuration</returns>
        [HttpPut("organizations/{organizationId}/config")]
        [Authorize(Roles = "Admin,SuperAdmin")] // Only Admin or SuperAdmin can modify
        public async Task<ActionResult<SyncConfigurationDto>> SaveSyncConfiguration(int organizationId, [FromBody] SyncConfigurationUpsertDto configDto)
        {
            _logger.LogInformation("Received request to save sync configuration for organization {OrganizationId}. IsEnabled: {IsEnabled}", organizationId, configDto.IsEnabled);
            try
            {
                // Map the DTO to model
                var syncConfiguration = new SyncConfiguration
                {
                    OrganizationId = organizationId,
                    PlatformBaseUrl = configDto.PlatformBaseUrl,
                    ApiKey = configDto.ApiKey,
                    IsEnabled = configDto.IsEnabled
                };

                var savedConfig = await _syncConfigurationService.SaveSyncConfigurationAsync(syncConfiguration);
                
                // Map the saved model back to DTO
                var savedConfigDto = new SyncConfigurationDto
                {
                    Id = savedConfig.Id,
                    OrganizationId = savedConfig.OrganizationId,
                    PlatformBaseUrl = savedConfig.PlatformBaseUrl,
                    ApiKey = savedConfig.ApiKey,
                    IsEnabled = savedConfig.IsEnabled
                };
                
                _logger.LogInformation("Successfully saved sync configuration for organization {OrganizationId}.", organizationId);
                return Ok(savedConfigDto);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized access attempt to save sync configuration for organization {OrganizationId}", organizationId);
                return Forbid();
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid argument when saving sync configuration for organization {OrganizationId}: {Message}", organizationId, ex.Message);
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving sync configuration for organization {OrganizationId}", organizationId);
                return StatusCode(500, "An error occurred while saving the sync configuration.");
            }
        }

        /// <summary>
        /// Deletes the sync configuration for an organization
        /// </summary>
        /// <param name="organizationId">The organization ID</param>
        /// <returns>204 No Content if successful, 404 if not found</returns>
        [HttpDelete("organizations/{organizationId}/config")]
        [Authorize(Roles = "Admin,SuperAdmin")] // Only Admin or SuperAdmin can delete
        public async Task<ActionResult> DeleteSyncConfiguration(int organizationId)
        {
            _logger.LogInformation("Received request to delete sync configuration for organization {OrganizationId}.", organizationId);
            try
            {
                var result = await _syncConfigurationService.DeleteSyncConfigurationAsync(organizationId);
                if (!result)
                {
                    _logger.LogWarning("Sync configuration for organization {OrganizationId} not found for deletion.", organizationId);
                    return NotFound();
                }
                _logger.LogInformation("Successfully deleted sync configuration for organization {OrganizationId}.", organizationId);
                return NoContent();
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized access attempt to delete sync configuration for organization {OrganizationId}", organizationId);
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting sync configuration for organization {OrganizationId}", organizationId);
                return StatusCode(500, "An error occurred while deleting the sync configuration.");
            }
        }

        /// <summary>
        /// Synchronizes the menu data with the SagraPreOrdine platform
        /// </summary>
        /// <param name="organizationId">The organization ID</param>
        /// <returns>The result of the synchronization</returns>
        [HttpPost("organizations/{organizationId}/sync/menu")]
        [Authorize(Roles = "Admin,SuperAdmin")] // Only Admin or SuperAdmin can trigger sync
        public async Task<ActionResult<MenuSyncResult>> SyncMenu(int organizationId)
        {
            _logger.LogInformation("Received request to synchronize menu for organization {OrganizationId}.", organizationId);
            try
            {
                var result = await _menuSyncService.SyncMenuAsync(organizationId);
                if (result.Success)
                {
                    _logger.LogInformation("Successfully synchronized menu for organization {OrganizationId}.", organizationId);
                    return Ok(result);
                }
                else
                {
                    _logger.LogWarning("Menu synchronization failed for organization {OrganizationId}: {Errors}", organizationId, result.ErrorMessage);
                    // Return appropriate status code based on the error
                    if (result.StatusCode.HasValue)
                    {
                        return StatusCode(result.StatusCode.Value, result);
                    }
                    return BadRequest(result);
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized access attempt to sync menu for organization {OrganizationId}", organizationId);
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing menu for organization {OrganizationId}", organizationId);
                return StatusCode(500, "An error occurred during menu synchronization.");
            }
        }
    }
}
