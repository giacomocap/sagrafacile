using Microsoft.EntityFrameworkCore;
using SagraFacile.NET.API.Data;
using SagraFacile.NET.API.DTOs;
using SagraFacile.NET.API.Models;
using SagraFacile.NET.API.Models.Enums;
using SagraFacile.NET.API.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

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
            return await _context.AdMediaItems
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
        }

        public async Task<(AdMediaItemDto? createdAd, string? error)> CreateAdAsync(int organizationId, AdMediaItemUpsertDto adDto)
        {
            var organization = await _context.Organizations.FindAsync(organizationId);
            if (organization == null)
            {
                return (null, "Organization not found.");
            }

            var mediaType = GetMediaType(adDto.File.ContentType);
            if (mediaType == null)
            {
                return (null, "Unsupported file type.");
            }

            var (filePath, error) = await SaveFileAsync(adDto.File, organizationId);
            if (error != null)
            {
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

            var resultDto = new AdMediaItemDto
            {
                Id = adMediaItem.Id,
                OrganizationId = adMediaItem.OrganizationId,
                Name = adMediaItem.Name,
                MediaType = adMediaItem.MediaType.ToString(),
                FilePath = adMediaItem.FilePath,
                MimeType = adMediaItem.MimeType,
                UploadedAt = adMediaItem.UploadedAt
            };

            return (resultDto, null);
        }

        public async Task<(bool success, string? error)> UpdateAdAsync(Guid adId, AdMediaItemUpsertDto adDto)
        {
            var adMediaItem = await _context.AdMediaItems.FindAsync(adId);
            if (adMediaItem == null)
            {
                return (false, "Ad not found.");
            }

            adMediaItem.Name = adDto.Name;

            _context.Entry(adMediaItem).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            return (true, null);
        }

        public async Task<(bool success, string? error)> DeleteAdAsync(Guid adId)
        {
            var adMediaItem = await _context.AdMediaItems.FindAsync(adId);
            if (adMediaItem == null)
            {
                return (false, "Ad not found.");
            }

            // Delete file from storage
            var fullPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", adMediaItem.FilePath.TrimStart('/'));
            if (File.Exists(fullPath))
            {
                try
                {
                    File.Delete(fullPath);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error deleting file: {fullPath}");
                    return (false, "Error deleting file from storage.");
                }
            }

            _context.AdMediaItems.Remove(adMediaItem);
            await _context.SaveChangesAsync();

            return (true, null);
        }

        private MediaType? GetMediaType(string contentType)
        {
            if (contentType.StartsWith("image/"))
            {
                return MediaType.Image;
            }
            if (contentType.StartsWith("video/"))
            {
                return MediaType.Video;
            }
            return null;
        }

        private async Task<(string? filePath, string? error)> SaveFileAsync(IFormFile file, int organizationId)
        {
            try
            {
                var directoryPath = Path.Combine("wwwroot", "media", "promo", organizationId.ToString());
                Directory.CreateDirectory(directoryPath);

                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
                var filePath = Path.Combine(directoryPath, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                return (Path.Combine("/media", "promo", organizationId.ToString(), fileName).Replace("\\\\", "/"), null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving file.");
                return (null, "Error saving file.");
            }
        }

        public Task<IEnumerable<AdMediaItemDto>> GetActiveAdsByAreaAsync(int areaId)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<AdMediaItemDto>> GetAllAdsByAreaAsync(int areaId)
        {
            throw new NotImplementedException();
        }
    }
}
