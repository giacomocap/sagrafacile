using Microsoft.EntityFrameworkCore;
using SagraFacile.NET.API.Data;
using SagraFacile.NET.API.DTOs;
using SagraFacile.NET.API.Models;
using SagraFacile.NET.API.Models.Enums;
using SagraFacile.NET.API.Services.Interfaces;

namespace SagraFacile.NET.API.Services
{
    public class AdMediaItemService : IAdMediaItemService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AdMediaItemService> _logger;

        public AdMediaItemService(ApplicationDbContext context, ILogger<AdMediaItemService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IEnumerable<AdMediaItemDto>> GetAdsByOrganizationAsync(int organizationId)
        {
            _logger.LogInformation("Fetching ad media items for OrganizationId: {OrganizationId}.", organizationId);
            var ads = await _context.AdMediaItems
                .Where(ad => ad.OrganizationId == organizationId)
                .OrderBy(ad => ad.Name)
                .Select(ad => new AdMediaItemDto
                {
                    Id = ad.Id,
                    OrganizationId = ad.OrganizationId,
                    Name = ad.Name,
                    MediaType = ad.MediaType.ToString(),
                    FilePath = ad.FilePath,
                    MimeType = ad.MimeType,
                    UploadedAt = ad.UploadedAt
                })
                .ToListAsync();
            _logger.LogInformation("Found {Count} ad media items for OrganizationId: {OrganizationId}.", ads.Count(), organizationId);
            return ads;
        }

        public async Task<(AdMediaItemDto? createdAd, string? error)> CreateAdAsync(int organizationId, AdMediaItemUpsertDto adDto)
        {
            _logger.LogInformation("Attempting to create ad media item for OrganizationId: {OrganizationId}, Name: {Name}.", organizationId, adDto.Name);

            var organization = await _context.Organizations.FindAsync(organizationId);
            if (organization == null)
            {
                _logger.LogWarning("Create ad failed: Organization with ID {OrganizationId} not found.", organizationId);
                return (null, "Organization not found.");
            }

            var mediaType = GetMediaType(adDto.File.ContentType);
            if (mediaType == null)
            {
                _logger.LogWarning("Create ad failed: Unsupported file type '{ContentType}'.", adDto.File.ContentType);
                return (null, "Unsupported file type.");
            }

            var (filePath, error) = await SaveFileAsync(adDto.File, organizationId);
            if (error != null)
            {
                _logger.LogError("Create ad failed: Error saving file: {Error}", error);
                return (null, error);
            }

            var adMediaItem = new AdMediaItem
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                Name = adDto.Name,
                MediaType = mediaType.Value,
                FilePath = filePath,
                MimeType = adDto.File.ContentType,
                UploadedAt = DateTime.UtcNow
            };

            _context.AdMediaItems.Add(adMediaItem);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Ad media item '{Name}' created successfully with ID {AdId} and FilePath {FilePath}.", adMediaItem.Name, adMediaItem.Id, adMediaItem.FilePath);

            var resultDto = new AdMediaItemDto
            {
                Id = adMediaItem.Id,
                OrganizationId = adMediaItem.OrganizationId,
                Name = adMediaItem.Name,
                MediaType = adMediaItem.MediaType.ToString(),
                FilePath = adMediaItem?.FilePath!,
                MimeType = adMediaItem?.MimeType!,
                UploadedAt = adMediaItem.UploadedAt
            };

            return (resultDto, null);
        }

        public async Task<(bool success, string? error)> UpdateAdAsync(Guid adId, AdMediaItemUpsertDto adDto)
        {
            _logger.LogInformation("Attempting to update ad media item with ID: {AdId}.", adId);
            var adMediaItem = await _context.AdMediaItems.FindAsync(adId);
            if (adMediaItem == null)
            {
                _logger.LogWarning("Update ad failed: Ad media item with ID {AdId} not found.", adId);
                return (false, "Ad not found.");
            }

            adMediaItem.Name = adDto.Name;

            _context.Entry(adMediaItem).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            _logger.LogInformation("Ad media item {AdId} updated successfully. New name: {Name}.", adId, adDto.Name);

            return (true, null);
        }

        public async Task<(bool success, string? error)> DeleteAdAsync(Guid adId)
        {
            _logger.LogInformation("Attempting to delete ad media item with ID: {AdId}.", adId);
            var adMediaItem = await _context.AdMediaItems.FindAsync(adId);
            if (adMediaItem == null)
            {
                _logger.LogWarning("Delete ad failed: Ad media item with ID {AdId} not found.", adId);
                return (false, "Ad not found.");
            }

            // Delete file from storage
            var fullPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", adMediaItem.FilePath.TrimStart('/'));
            if (File.Exists(fullPath))
            {
                try
                {
                    _logger.LogInformation("Deleting file from storage: {FilePath}.", fullPath);
                    File.Delete(fullPath);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error deleting file: {FilePath}.", fullPath);
                    return (false, "Error deleting file from storage.");
                }
            }
            else
            {
                _logger.LogWarning("File not found at {FilePath} for ad media item {AdId}. Proceeding with database deletion.", fullPath, adId);
            }

            _context.AdMediaItems.Remove(adMediaItem);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Ad media item {AdId} deleted successfully from database.", adId);

            return (true, null);
        }

        private MediaType? GetMediaType(string contentType)
        {
            _logger.LogDebug("Determining media type for content type: {ContentType}.", contentType);
            if (contentType.StartsWith("image/"))
            {
                return MediaType.Image;
            }
            if (contentType.StartsWith("video/"))
            {
                return MediaType.Video;
            }
            _logger.LogWarning("Unsupported media type: {ContentType}.", contentType);
            return null;
        }

        private async Task<(string? filePath, string? error)> SaveFileAsync(IFormFile file, int organizationId)
        {
            _logger.LogInformation("Attempting to save file '{FileName}' for OrganizationId: {OrganizationId}.", file.FileName, organizationId);
            try
            {
                var directoryPath = Path.Combine("wwwroot", "media", "promo", organizationId.ToString());
                _logger.LogDebug("Ensuring directory exists: {DirectoryPath}.", directoryPath);
                Directory.CreateDirectory(directoryPath);

                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
                var filePath = Path.Combine(directoryPath, fileName);
                _logger.LogDebug("Saving file to: {FilePath}.", filePath);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                var relativePath = Path.Combine("/media", "promo", organizationId.ToString(), fileName).Replace("\\", "/");
                _logger.LogInformation("File '{FileName}' saved successfully to {RelativePath}.", file.FileName, relativePath);
                return (relativePath, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving file '{FileName}' for OrganizationId: {OrganizationId}.", file.FileName, organizationId);
                return (null, "Error saving file.");
            }
        }

        public Task<IEnumerable<AdMediaItemDto>> GetActiveAdsByAreaAsync(int areaId)
        {
            _logger.LogInformation("GetActiveAdsByAreaAsync not implemented for AreaId: {AreaId}.", areaId);
            throw new NotImplementedException();
        }

        public Task<IEnumerable<AdMediaItemDto>> GetAllAdsByAreaAsync(int areaId)
        {
            _logger.LogInformation("GetAllAdsByAreaAsync not implemented for AreaId: {AreaId}.", areaId);
            throw new NotImplementedException();
        }
    }
}
