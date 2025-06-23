using SagraFacile.NET.API.DTOs;
using System;
using System.Threading.Tasks;

namespace SagraFacile.NET.API.Services.Interfaces
{
    public interface IPrintJobService
    {
        Task<PaginatedResult<PrintJobDto>> GetPrintJobsAsync(PrintJobQueryParameters queryParams);
        Task<(bool Success, string? Error)> RetryPrintJobAsync(Guid jobId);
    }
}
