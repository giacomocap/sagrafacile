using Microsoft.EntityFrameworkCore;
using SagraFacile.NET.API.Data;
using SagraFacile.NET.API.DTOs;
using SagraFacile.NET.API.Models;
using SagraFacile.NET.API.Models.Results;
using SagraFacile.NET.API.Services.Interfaces;

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
            _logger.LogInformation("Fetching ad area assignments for AreaId: {AreaId}.", areaId);
            var assignments = await _context.AdAreaAssignments
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
            _logger.LogInformation("Found {Count} ad area assignments for AreaId: {AreaId}.", assignments.Count(), areaId);
            return assignments;
        }

        public async Task<ServiceResult<AdAreaAssignmentDto>> CreateAssignmentAsync(AdAreaAssignmentUpsertDto dto)
        {
            _logger.LogInformation("Attempting to create ad area assignment for AdMediaItemId: {AdMediaItemId}, AreaId: {AreaId}.", dto.AdMediaItemId, dto.AreaId);

            var adExists = await _context.AdMediaItems.AnyAsync(ad => ad.Id == dto.AdMediaItemId);
            if (!adExists)
            {
                _logger.LogWarning("Create assignment failed: AdMediaItem with ID {AdMediaItemId} not found.", dto.AdMediaItemId);
                return ServiceResult<AdAreaAssignmentDto>.Fail("AdMediaItem not found.");
            }

            var areaExists = await _context.Areas.AnyAsync(a => a.Id == dto.AreaId);
            if (!areaExists)
            {
                _logger.LogWarning("Create assignment failed: Area with ID {AreaId} not found.", dto.AreaId);
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
            _logger.LogInformation("Ad area assignment created successfully with ID {AssignmentId}.", assignment.Id);

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
            _logger.LogInformation("Attempting to update ad area assignment with ID: {AssignmentId}.", assignmentId);
            var assignment = await _context.AdAreaAssignments.FindAsync(assignmentId);
            if (assignment == null)
            {
                _logger.LogWarning("Update assignment failed: Assignment with ID {AssignmentId} not found.", assignmentId);
                return ServiceResult.Fail("Assignment not found.");
            }

            assignment.DisplayOrder = dto.DisplayOrder;
            assignment.DurationSeconds = dto.DurationSeconds;
            assignment.IsActive = dto.IsActive;
            // Note: Changing AdMediaItemId or AreaId is not supported to keep it simple.

            _context.Entry(assignment).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            _logger.LogInformation("Ad area assignment {AssignmentId} updated successfully.", assignmentId);

            return ServiceResult.Ok();
        }

        public async Task<ServiceResult> DeleteAssignmentAsync(Guid assignmentId)
        {
            _logger.LogInformation("Attempting to delete ad area assignment with ID: {AssignmentId}.", assignmentId);
            var assignment = await _context.AdAreaAssignments.FindAsync(assignmentId);
            if (assignment == null)
            {
                _logger.LogWarning("Delete assignment failed: Assignment with ID {AssignmentId} not found.", assignmentId);
                return ServiceResult.Fail("Assignment not found.");
            }

            _context.AdAreaAssignments.Remove(assignment);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Ad area assignment {AssignmentId} deleted successfully.", assignmentId);

            return ServiceResult.Ok();
        }
    }
}
