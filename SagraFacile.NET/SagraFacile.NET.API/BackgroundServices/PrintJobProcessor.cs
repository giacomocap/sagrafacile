using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SagraFacile.NET.API.Data;
using SagraFacile.NET.API.Models.Enums;
using SagraFacile.NET.API.Services.Interfaces;

namespace SagraFacile.NET.API.BackgroundServices
{
    public class PrintJobProcessor : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<PrintJobProcessor> _logger;
        private static readonly SemaphoreSlim _signal = new SemaphoreSlim(0);

        public PrintJobProcessor(IServiceProvider serviceProvider, ILogger<PrintJobProcessor> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public static void Trigger() => _signal.Release();

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("PrintJobProcessor is starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Wait for a signal or a timeout
                    var signaled = await _signal.WaitAsync(TimeSpan.FromSeconds(3), stoppingToken);

                    if (signaled)
                    {
                        _logger.LogInformation("PrintJobProcessor triggered by a signal (high-priority job).");
                    }

                    await ProcessPendingJobsAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // When StopAsync is called, WaitAsync throws this exception.
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An unhandled exception occurred in PrintJobProcessor.");
                    // Wait a bit before retrying to avoid fast-looping on a persistent error.
                    await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                }
            }

            _logger.LogInformation("PrintJobProcessor is stopping.");
        }

        private async Task ProcessPendingJobsAsync(CancellationToken stoppingToken)
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var printerService = scope.ServiceProvider.GetRequiredService<IPrinterService>();
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<PrintJobProcessor>>();

                // Get pending jobs or failed jobs that are ready for retry
                var jobsToProcess = await context.PrintJobs
                    .Include(j => j.Printer)
                    .Where(j => j.Status == PrintJobStatus.Pending ||
                                (j.Status == PrintJobStatus.Failed && j.RetryCount < 5 && j.LastAttemptAt < DateTime.UtcNow.AddSeconds(-30)))
                    .OrderBy(j => j.CreatedAt)
                    .Take(10) // Process in batches
                    .ToListAsync(stoppingToken);

                if (!jobsToProcess.Any())
                {
                    return;
                }

                logger.LogInformation("Found {JobCount} print jobs to process.", jobsToProcess.Count);

                foreach (var job in jobsToProcess)
                {
                    if (stoppingToken.IsCancellationRequested) break;

                    job.Status = PrintJobStatus.Processing;
                    job.LastAttemptAt = DateTime.UtcNow;
                    await context.SaveChangesAsync(stoppingToken);

                    var (success, error) = await printerService.SendToPrinterAsync(job.Printer, job.Content, job.JobType);

                    if (success)
                    {
                        job.Status = PrintJobStatus.Succeeded;
                        job.CompletedAt = DateTime.UtcNow;
                        job.ErrorMessage = null;
                    }
                    else
                    {
                        job.Status = PrintJobStatus.Failed;
                        job.RetryCount++;
                        job.ErrorMessage = error;
                        logger.LogWarning("Print job {JobId} failed for printer {PrinterName}. Error: {Error}. Retry count: {RetryCount}", job.Id, job.Printer.Name, error, job.RetryCount);
                    }

                    await context.SaveChangesAsync(stoppingToken);
                }
            }
        }
    }
}
