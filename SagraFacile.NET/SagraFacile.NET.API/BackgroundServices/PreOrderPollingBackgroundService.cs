using Microsoft.EntityFrameworkCore;
using SagraFacile.NET.API.Data;
using SagraFacile.NET.API.Services.Interfaces;

namespace SagraFacile.NET.API.BackgroundServices
{
    public class PreOrderPollingBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider; // Use IServiceProvider to create scopes
        private readonly ILogger<PreOrderPollingBackgroundService> _logger;
        private readonly TimeSpan _pollingInterval = TimeSpan.FromMinutes(1); // Configurable interval (e.g., 1 minute)

        public PreOrderPollingBackgroundService(
            IServiceProvider serviceProvider, // Inject the provider
            ILogger<PreOrderPollingBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("PreOrder Polling Background Service starting.");

            stoppingToken.Register(() =>
                _logger.LogInformation("PreOrder Polling Background Service is stopping."));

            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("PreOrder Polling Background Service running at: {time}", DateTimeOffset.Now);

                try
                {
                    // Create a scope for this iteration to resolve scoped services
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                        var pollingService = scope.ServiceProvider.GetRequiredService<IPreOrderPollingService>();

                        // Find organizations with sync enabled
                        var organizationsToPoll = await dbContext.SyncConfigurations
                            .Where(sc => sc.IsEnabled && !string.IsNullOrEmpty(sc.PlatformBaseUrl) && !string.IsNullOrEmpty(sc.ApiKey))
                            .ToListAsync(stoppingToken);

                        if (!organizationsToPoll.Any())
                        {
                            _logger.LogInformation("No organizations found with preorder sync enabled.");
                        }
                        else
                        {
                            _logger.LogInformation("Found {Count} organizations to poll for preorders.", organizationsToPoll.Count);
                            var pollingTasks = new List<Task>();

                            foreach (var syncConfig in organizationsToPoll)
                            {
                                // Add each organization's poll as a separate task
                                pollingTasks.Add(pollingService.PollAndImportPreOrdersAsync(syncConfig.OrganizationId, syncConfig, stoppingToken));
                            }

                            // Wait for all polling tasks for this cycle to complete
                            await Task.WhenAll(pollingTasks);
                            _logger.LogInformation("Finished polling cycle for {Count} organizations.", organizationsToPoll.Count);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // Prevent throwing if stoppingToken was signaled
                    _logger.LogInformation("Polling cancelled.");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred during the preorder polling cycle.");
                }

                // Wait for the next polling interval
                try
                {
                    await Task.Delay(_pollingInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Delay cancelled, stopping service.");
                    break; // Exit loop if delay is cancelled
                }
            }

            _logger.LogInformation("PreOrder Polling Background Service finished.");
        }
    }
}
