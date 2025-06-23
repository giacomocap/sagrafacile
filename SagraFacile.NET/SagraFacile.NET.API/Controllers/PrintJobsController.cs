using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SagraFacile.NET.API.DTOs;
using SagraFacile.NET.API.Services.Interfaces;
using System;
using System.Threading.Tasks;

namespace SagraFacile.NET.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public class PrintJobsController : ControllerBase
    {
        private readonly IPrintJobService _printJobService;
        private readonly ILogger<PrintJobsController> _logger;

        public PrintJobsController(IPrintJobService printJobService, ILogger<PrintJobsController> logger)
        {
            _printJobService = printJobService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<PaginatedResult<PrintJobDto>>> GetPrintJobs([FromQuery] PrintJobQueryParameters queryParams)
        {
            _logger.LogInformation("Fetching print jobs with parameters: Page={Page}, PageSize={PageSize}, SortBy={SortBy}, SortAscending={SortAscending}",
                queryParams.Page, queryParams.PageSize, queryParams.SortBy, queryParams.SortAscending);

            var result = await _printJobService.GetPrintJobsAsync(queryParams);
            return Ok(result);
        }

        [HttpPost("{jobId}/retry")]
        public async Task<IActionResult> RetryPrintJob(Guid jobId)
        {
            _logger.LogInformation("Received request to retry print job {JobId}", jobId);
            var (success, error) = await _printJobService.RetryPrintJobAsync(jobId);

            if (!success)
            {
                _logger.LogWarning("Failed to retry print job {JobId}: {Error}", jobId, error);
                return BadRequest(new { message = error });
            }

            _logger.LogInformation("Successfully queued print job {JobId} for retry.", jobId);
            return Ok(new { message = "Print job successfully queued for retry." });
        }
    }
}
