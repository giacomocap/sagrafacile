using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SagraFacile.NET.API.Data;
using SagraFacile.NET.API.Models;
using SagraFacile.NET.API.Models.Enums;

namespace SagraFacile.NET.API.BackgroundServices
{
    public class DataRetentionService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DataRetentionService> _logger;

        public DataRetentionService(IServiceProvider serviceProvider, ILogger<DataRetentionService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("DataRetentionService is starting.");

            // Wait a moment for the application to start up fully before the first run.
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("DataRetentionService is running its periodic check at {time}", DateTimeOffset.Now);

                try
                {
                    await ProcessPendingDeletionsAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An unhandled exception occurred in DataRetentionService.");
                }

                // Wait for the next scheduled run. e.g., run once every 24 hours.
                await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
            }

            _logger.LogInformation("DataRetentionService is stopping.");
        }

        private async Task ProcessPendingDeletionsAsync(CancellationToken stoppingToken)
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<DataRetentionService>>();

                // Process Organizations marked for deletion
                var organizationsToDelete = await context.Organizations
                    .Where(o => o.Status == OrganizationStatus.PendingDeletion && o.DeletionScheduledAt <= DateTime.UtcNow)
                    .ToListAsync(stoppingToken);

                if (organizationsToDelete.Any())
                {
                    logger.LogInformation("Found {Count} organizations to permanently delete.", organizationsToDelete.Count);
                    foreach (var org in organizationsToDelete)
                    {
                        if (stoppingToken.IsCancellationRequested) break;
                        logger.LogWarning("Permanently deleting organization {OrganizationId} ({OrganizationName}). This is a destructive action.", org.Id, org.Name);
                        context.Organizations.Remove(org);
                    }
                    await context.SaveChangesAsync(stoppingToken);
                }
                else
                {
                    logger.LogInformation("No organizations are due for permanent deletion.");
                }


                // Process Users marked for deletion
                var usersToAnonymize = await context.Users
                    .Where(u => u.Status == UserStatus.PendingDeletion && u.DeletionScheduledAt <= DateTime.UtcNow)
                    .ToListAsync(stoppingToken);

                if (usersToAnonymize.Any())
                {
                    logger.LogInformation("Found {Count} users to anonymize.", usersToAnonymize.Count);
                    foreach (var user in usersToAnonymize)
                    {
                        if (stoppingToken.IsCancellationRequested) break;
                        
                        logger.LogWarning("Anonymizing user {UserId}.", user.Id);

                        user.FirstName = "Deleted";
                        user.LastName = "User";
                        user.Email = $"deleted-{user.Id}@sagrafacile.it";
                        user.NormalizedEmail = user.Email.ToUpperInvariant();
                        user.UserName = user.Email;
                        user.NormalizedUserName = user.NormalizedEmail;
                        user.PhoneNumber = null;
                        user.PasswordHash = null; // Prevent login
                        user.SecurityStamp = Guid.NewGuid().ToString(); // Invalidate existing sessions
                        user.Status = UserStatus.Deleted;
                        user.RefreshToken = null;
                        user.RefreshTokenExpiryTime = null;

                        var result = await userManager.UpdateAsync(user);
                        if (!result.Succeeded)
                        {
                            logger.LogError("Failed to anonymize user {UserId}. Errors: {Errors}", user.Id, string.Join(", ", result.Errors.Select(e => e.Description)));
                        }
                    }
                }
                else
                {
                    logger.LogInformation("No users are due for anonymization.");
                }
            }
        }
    }
}
