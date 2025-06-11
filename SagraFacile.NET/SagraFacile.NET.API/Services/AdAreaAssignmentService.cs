using Microsoft.EntityFrameworkCore;
using SagraFacile.NET.API.Data;
using SagraFacile.NET.API.DTOs;
using SagraFacile.NET.API.Models;
using SagraFacile.NET.API.Services.Interfaces;
using SagraFacile.NET.API.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SagraFacile.NET.API.Services
{
    public class AdAreaAssignmentService : IAdAreaAssignmentService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AdAreaAssignmentService> _logger;

        public AdAreaAssignmentService(ApplicationDbContext context, ILogger<AdAreaAssignmentService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IEnumerable<AdAreaAssignmentDto>> GetAssignmentsForAreaAsync(int areaId)
        {
            return await _context.AdAreaAssignments
                .Include(a => a.AdMediaItem)
                .Where(a => a.AreaId == areaId)
                .OrderBy(a => a.DisplayOrder)
                .Select(a => new AdAreaAssignmentDto
                {
                    Id = a.Id,
                    AdMediaItemId = a.AdMediaItemId,
                    AreaId = a.AreaId,
                    DisplayOrder = a.DisplayOrder,
                    DurationSeconds = a.DurationSeconds,
                    IsActive = a.IsActive,
                    AdMediaItem = new AdMediaItemDto
                    {
                        Id = a.AdMediaItem.Id,
                        OrganizationId = a.AdMediaItem.OrganizationId,
                        Name = a.AdMediaItem.Name,
                        MediaType = a.AdMediaItem.MediaType.ToString(),
                        FilePath = a.AdMediaItem.FilePath,
                        MimeType = a.AdMediaItem.MimeType,
                        UploadedAt = a.AdMediaItem.UploadedAt
                    }
                })
                .ToListAsync();
        }

        public async Task<ServiceResult<AdAreaAssignmentDto>> CreateAssignmentAsync(AdAreaAssignmentUpsertDto dto)
        {
            var adExists = await _context.AdMediaItems.AnyAsync(ad => ad.Id == dto.AdMediaItemId);
            if (!adExists)
            {
                return ServiceResult<AdAreaAssignmentDto>.Fail("AdMediaItem not found.");
            }

            var areaExists = await _context.Areas.AnyAsync(a => a.Id == dto.AreaId);
            if (!areaExists)
            {
                return ServiceResult<AdAreaAssignmentDto>.Fail("Area not found.");
            }

            var assignment = new AdAreaAssignment
            {
                Id = Guid.NewGuid(),
                AdMediaItemId = dto.AdMediaItemId,
                AreaId = dto.AreaId,
                DisplayOrder = dto.DisplayOrder,
                DurationSeconds = dto.DurationSeconds,
                IsActive = dto.IsActive
            };

            _context.AdAreaAssignments.Add(assignment);
            await _context.SaveChangesAsync();

            var createdDto = await _context.AdAreaAssignments
                .Include(a => a.AdMediaItem)
                .Where(a => a.Id == assignment.Id)
                .Select(a => new AdAreaAssignmentDto
                {
                    Id = a.Id,
                    AdMediaItemId = a.AdMediaItemId,
                    AreaId = a.AreaId,
                    DisplayOrder = a.DisplayOrder,
                    DurationSeconds = a.DurationSeconds,
                    IsActive = a.IsActive,
                    AdMediaItem = new AdMediaItemDto
                    {
                        Id = a.AdMediaItem.Id,
                        OrganizationId = a.AdMediaItem.OrganizationId,
                        Name = a.AdMediaItem.Name,
                        MediaType = a.AdMediaItem.MediaType.ToString(),
                        FilePath = a.AdMediaItem.FilePath,
                        MimeType = a.AdMediaItem.MimeType,
                        UploadedAt = a.AdMediaItem.UploadedAt
                    }
                })
                .FirstAsync();

            return ServiceResult<AdAreaAssignmentDto>.Ok(createdDto);
        }

        public async Task<ServiceResult> UpdateAssignmentAsync(Guid assignmentId, AdAreaAssignmentUpsertDto dto)
        {
            var assignment = await _context.AdAreaAssignments.FindAsync(assignmentId);
            if (assignment == null)
            {
                return ServiceResult.Fail("Assignment not found.");
            }

            assignment.DisplayOrder = dto.DisplayOrder;
            assignment.DurationSeconds = dto.DurationSeconds;
            assignment.IsActive = dto.IsActive;
            // Note: Changing AdMediaItemId or AreaId is not supported to keep it simple.
            // Users should delete and recreate if they need to change the core link.

            _context.Entry(assignment).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            return ServiceResult.Ok();
        }

        public async Task<ServiceResult> DeleteAssignmentAsync(Guid assignmentId)
        {
            var assignment = await _context.AdAreaAssignments.FindAsync(assignmentId);
            if (assignment == null)
            {
                return ServiceResult.Fail("Assignment not found.");
            }

            _context.AdAreaAssignments.Remove(assignment);
            await _context.SaveChangesAsync();

            return ServiceResult.Ok();
        }
    }
}
