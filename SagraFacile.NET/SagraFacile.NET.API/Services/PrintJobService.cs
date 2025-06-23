using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SagraFacile.NET.API.BackgroundServices;
using SagraFacile.NET.API.Data;
using SagraFacile.NET.API.DTOs;
using SagraFacile.NET.API.Models;
using SagraFacile.NET.API.Models.Enums;
using SagraFacile.NET.API.Services.Interfaces;
using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace SagraFacile.NET.API.Services
{
    public class PrintJobService : BaseService, IPrintJobService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<PrintJobService> _logger;

        public PrintJobService(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor, ILogger<PrintJobService> logger)
            : base(httpContextAccessor)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<PaginatedResult<PrintJobDto>> GetPrintJobsAsync(PrintJobQueryParameters queryParams)
        {
            var (userOrgId, isSuperAdmin) = GetUserContext();
            if (!userOrgId.HasValue && !isSuperAdmin)
            {
                _logger.LogWarning("Attempt to access print jobs without organization context.");
                return new PaginatedResult<PrintJobDto>(); // Return empty result
            }

            var query = _context.PrintJobs
                .Include(j => j.Printer)
                .Include(j => j.Order)
                .AsNoTracking();

            if (!isSuperAdmin)
            {
                query = query.Where(j => j.OrganizationId == userOrgId.Value);
            }

            // Sorting
            var sortBy = queryParams.SortBy?.ToLowerInvariant();
            Expression<Func<PrintJob, object>> keySelector = sortBy switch
            {
                "status" => j => j.Status,
                "jobtype" => j => j.JobType,
                "lastattemptat" => j => j.LastAttemptAt,
                "retrycount" => j => j.RetryCount,
                _ => j => j.CreatedAt,
            };

            query = queryParams.SortAscending
                ? query.OrderBy(keySelector)
                : query.OrderByDescending(keySelector);

            var totalCount = await query.CountAsync();

            var jobs = await query
                .Skip((queryParams.Page - 1) * queryParams.PageSize)
                .Take(queryParams.PageSize)
                .Select(j => new PrintJobDto
                {
                    Id = j.Id,
                    JobType = j.JobType,
                    Status = j.Status,
                    CreatedAt = j.CreatedAt,
                    LastAttemptAt = j.LastAttemptAt,
                    CompletedAt = j.CompletedAt,
                    RetryCount = j.RetryCount,
                    ErrorMessage = j.ErrorMessage,
                    OrderId = j.OrderId,
                    OrderDisplayNumber = j.Order != null ? j.Order.DisplayOrderNumber : null,
                    PrinterId = j.PrinterId,
                    PrinterName = j.Printer.Name
                })
                .ToListAsync();

            return new PaginatedResult<PrintJobDto>
            {
                Items = jobs,
                TotalCount = totalCount,
                Page = queryParams.Page,
                PageSize = queryParams.PageSize
            };
        }

        public async Task<(bool Success, string? Error)> RetryPrintJobAsync(Guid jobId)
        {
            var (userOrgId, isSuperAdmin) = GetUserContext();
            if (!userOrgId.HasValue && !isSuperAdmin)
            {
                return (false, "User organization context is missing.");
            }

            var job = await _context.PrintJobs.FindAsync(jobId);

            if (job == null)
            {
                return (false, "Print job not found.");
            }

            if (!isSuperAdmin && job.OrganizationId != userOrgId)
            {
                return (false, "Not authorized to retry this print job.");
            }

            if (job.Status != PrintJobStatus.Failed)
            {
                return (false, $"Cannot retry a job with status '{job.Status}'. Only failed jobs can be retried.");
            }

            _logger.LogInformation("Manually retrying print job {JobId}", jobId);

            job.Status = PrintJobStatus.Pending;
            // Reset retry count to give it fresh attempts, but log that it was a manual retry.
            // job.RetryCount = 0; // Optional: decide if manual retry should reset the counter
            job.ErrorMessage = $"{job.ErrorMessage} [Manually retried at {DateTime.UtcNow:O}]";

            await _context.SaveChangesAsync();

            // Trigger the background processor to pick it up immediately
            PrintJobProcessor.Trigger();

            return (true, null);
        }
    }
}
