using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SagraFacile.NET.API.DTOs;
using SagraFacile.NET.API.Services.Interfaces;

namespace SagraFacile.NET.API.Controllers
{
    [Route("api/")]
    [ApiController]
    public class AdMediaItemsController : ControllerBase
    {
        private readonly IAdMediaItemService _adMediaItemService;
        private readonly ILogger<AdMediaItemsController> _logger;

        public AdMediaItemsController(IAdMediaItemService adMediaItemService, ILogger<AdMediaItemsController> logger)
        {
            _adMediaItemService = adMediaItemService;
            _logger = logger;
        }

        // ADMIN ENDPOINTS
        [HttpGet("admin/organizations/{organizationId}/ads")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<ActionResult<IEnumerable<AdMediaItemDto>>> GetAdminAds(int organizationId)
        {
            _logger.LogInformation("Received request to get ad media items for OrganizationId: {OrganizationId}", organizationId);
            try
            {
                var ads = await _adMediaItemService.GetAdsByOrganizationAsync(organizationId);
                _logger.LogInformation("Successfully retrieved {Count} ad media items for OrganizationId: {OrganizationId}", ((List<AdMediaItemDto>)ads).Count, organizationId);
                return Ok(ads);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized access attempt to get ad media items for OrganizationId: {OrganizationId}", organizationId);
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while getting ad media items for OrganizationId: {OrganizationId}", organizationId);
                return StatusCode(500, "An error occurred while getting ad media items.");
            }
        }

        [HttpPost("admin/organizations/{organizationId}/ads")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult<AdMediaItemDto>> PostAd(int organizationId, [FromForm] AdMediaItemUpsertDto adDto)
        {
            _logger.LogInformation("Received request to create ad media item for OrganizationId: {OrganizationId}, Name: {Name}", organizationId, adDto.Name);
            if (adDto.File == null || adDto.File.Length == 0)
            {
                _logger.LogWarning("Bad request: No file provided for ad media item creation for OrganizationId: {OrganizationId}", organizationId);
                return BadRequest("A file is required.");
            }

            try
            {
                var (createdAd, error) = await _adMediaItemService.CreateAdAsync(organizationId, adDto);

                if (error != null)
                {
                    _logger.LogWarning("Failed to create ad media item for OrganizationId: {OrganizationId}, Name: {Name}. Error: {Error}", organizationId, adDto.Name, error);
                    return BadRequest(new { message = error });
                }

                _logger.LogInformation("Successfully created ad media item with Id: {AdId} for OrganizationId: {OrganizationId}", createdAd.Id, createdAd.OrganizationId);
                return CreatedAtAction(nameof(GetAdminAds), new { organizationId = createdAd.OrganizationId }, createdAd);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized access attempt to create ad media item for OrganizationId: {OrganizationId}", organizationId);
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while creating ad media item for OrganizationId: {OrganizationId}, Name: {Name}", organizationId, adDto.Name);
                return StatusCode(500, "An error occurred while creating ad media item.");
            }
        }

        [HttpPut("admin/ads/{adId}")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> PutAd(Guid adId, [FromForm] AdMediaItemUpsertDto adDto)
        {
            _logger.LogInformation("Received request to update ad media item with Id: {AdId}, Name: {Name}", adId, adDto.Name);
            try
            {
                var (success, error) = await _adMediaItemService.UpdateAdAsync(adId, adDto);

                if (!success)
                {
                    _logger.LogWarning("Failed to update ad media item with Id: {AdId}, Name: {Name}. Error: {Error}", adId, adDto.Name, error);
                    if (error.Contains("not found"))
                    {
                        return NotFound(new { message = error });
                    }
                    return BadRequest(new { message = error });
                }

                _logger.LogInformation("Successfully updated ad media item with Id: {AdId}", adId);
                return NoContent();
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized access attempt to update ad media item with Id: {AdId}", adId);
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while updating ad media item with Id: {AdId}, Name: {Name}", adId, adDto.Name);
                return StatusCode(500, "An error occurred while updating ad media item.");
            }
        }

        [HttpDelete("admin/ads/{adId}")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> DeleteAd(Guid adId)
        {
            _logger.LogInformation("Received request to delete ad media item with Id: {AdId}", adId);
            try
            {
                var (success, error) = await _adMediaItemService.DeleteAdAsync(adId);

                if (!success)
                {
                    _logger.LogWarning("Failed to delete ad media item with Id: {AdId}. Error: {Error}", adId, error);
                    if (error.Contains("not found"))
                    {
                        return NotFound(new { message = error });
                    }
                    return BadRequest(new { message = error });
                }

                _logger.LogInformation("Successfully deleted ad media item with Id: {AdId}", adId);
                return NoContent();
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized access attempt to delete ad media item with Id: {AdId}", adId);
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while deleting ad media item with Id: {AdId}", adId);
                return StatusCode(500, "An error occurred while deleting ad media item.");
            }
        }
    }
}
