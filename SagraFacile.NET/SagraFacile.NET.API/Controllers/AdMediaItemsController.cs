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
            var ads = await _adMediaItemService.GetAdsByOrganizationAsync(organizationId);
            return Ok(ads);
        }

        [HttpPost("admin/organizations/{organizationId}/ads")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult<AdMediaItemDto>> PostAd(int organizationId, [FromForm] AdMediaItemUpsertDto adDto)
        {
            if (adDto.File == null || adDto.File.Length == 0)
            {
                return BadRequest("A file is required.");
            }

            var (createdAd, error) = await _adMediaItemService.CreateAdAsync(organizationId, adDto);

            if (error != null)
            {
                return BadRequest(new { message = error });
            }

            return CreatedAtAction(nameof(GetAdminAds), new { organizationId = createdAd.OrganizationId }, createdAd);
        }

        [HttpPut("admin/ads/{adId}")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> PutAd(Guid adId, [FromForm] AdMediaItemUpsertDto adDto)
        {
            var (success, error) = await _adMediaItemService.UpdateAdAsync(adId, adDto);

            if (!success)
            {
                if (error.Contains("not found"))
                {
                    return NotFound(new { message = error });
                }
                return BadRequest(new { message = error });
            }

            return NoContent();
        }

        [HttpDelete("admin/ads/{adId}")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> DeleteAd(Guid adId)
        {
            var (success, error) = await _adMediaItemService.DeleteAdAsync(adId);

            if (!success)
            {
                if (error.Contains("not found"))
                {
                    return NotFound(new { message = error });
                }
                return BadRequest(new { message = error });
            }

            return NoContent();
        }
    }
}
