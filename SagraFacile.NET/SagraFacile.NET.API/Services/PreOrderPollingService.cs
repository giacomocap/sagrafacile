using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SagraFacile.NET.API.Data;
using SagraFacile.NET.API.DTOs.Platform;
using SagraFacile.NET.API.Models;
using SagraFacile.NET.API.Services.Interfaces;
using SagraFacile.NET.API.Utils;

namespace SagraFacile.NET.API.Services
{
    public class PreOrderPollingService : IPreOrderPollingService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<PreOrderPollingService> _logger;

        // Define JSON serialization options matching the platform API
        private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true, // Be flexible with casing from platform
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase // Assume platform uses camelCase
        };

        public PreOrderPollingService(
            ApplicationDbContext context,
            IHttpClientFactory httpClientFactory,
            ILogger<PreOrderPollingService> logger)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task PollAndImportPreOrdersAsync(Guid organizationId, SyncConfiguration syncConfig, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting preorder poll for Organization {OrganizationId}.", organizationId);

            List<PlatformPreOrderDto>? fetchedPreOrders;
            try
            {
                fetchedPreOrders = await FetchNewPreOrdersAsync(syncConfig, cancellationToken);
                if (fetchedPreOrders == null || !fetchedPreOrders.Any())
                {
                    _logger.LogInformation("No new preorders found for Organization {OrganizationId}.", organizationId);
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching preorders for Organization {OrganizationId} from {PlatformUrl}.",
                    organizationId, syncConfig.PlatformBaseUrl);
                return; // Stop processing if fetching failed
            }

            _logger.LogInformation("Fetched {Count} new preorders for Organization {OrganizationId}. Processing import...", fetchedPreOrders.Count, organizationId);

            var successfullyImportedIds = new List<string>();
            var importTasks = new List<Task>();

            foreach (var platformPreOrder in fetchedPreOrders)
            {
                // Add each import attempt as a separate task to run concurrently
                importTasks.Add(ImportSinglePreOrderAsync(platformPreOrder, organizationId, successfullyImportedIds));
            }

            // Wait for all import attempts to complete
            await Task.WhenAll(importTasks);

            // Mark successfully imported orders as fetched on the platform
            if (successfullyImportedIds.Any())
            {
                await MarkOrdersAsFetchedAsync(syncConfig, successfullyImportedIds, cancellationToken);
            }

            _logger.LogInformation("Finished preorder poll for Organization {OrganizationId}. Imported {ImportedCount} orders.", organizationId, successfullyImportedIds.Count);
        }

        private async Task<List<PlatformPreOrderDto>?> FetchNewPreOrdersAsync(SyncConfiguration syncConfig, CancellationToken cancellationToken)
        {
            var client = _httpClientFactory.CreateClient("PreOrderPlatformClient"); // Consider using a named client
            var pollUrl = $"{syncConfig.PlatformBaseUrl.TrimEnd('/')}/api/preorders/poll?status=New"; // Fetch only 'New' status, adjust limit if needed

            var request = new HttpRequestMessage(HttpMethod.Get, pollUrl);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.Add("Authorization", $"ApiKey {syncConfig.ApiKey}");

            var response = await client.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Failed to fetch preorders. Status: {StatusCode}, Response: {ErrorContent}", response.StatusCode, errorContent);
                response.EnsureSuccessStatusCode(); // Throw exception to be caught by caller
            }

            var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var pollResponse = await JsonSerializer.DeserializeAsync<PlatformPreOrderPollResponse>(contentStream, _jsonOptions, cancellationToken);

            return pollResponse?.PreOrders;
        }

        private async Task ImportSinglePreOrderAsync(PlatformPreOrderDto platformPreOrder, Guid organizationId, List<string> successfullyImportedIds)
        {
            try
            {
                // 1. Check if already imported
                var alreadyExists = await _context.Orders
                    .AnyAsync(o => o.PreOrderPlatformId == platformPreOrder.Id);

                if (alreadyExists)
                {
                    _logger.LogWarning("Preorder {PlatformPreOrderId} already exists locally. Skipping import.", platformPreOrder.Id);
                    // Optionally: Still mark as fetched if status on platform is 'New'? Depends on desired logic.
                    // lock (successfullyImportedIds) { successfullyImportedIds.Add(platformPreOrder.Id); }
                    return;
                }

                // 2. Validate Area
                var localArea = await _context.Areas
                    .FirstOrDefaultAsync(a => a.Id == platformPreOrder.Area.LocalAreaId && a.OrganizationId == organizationId);

                if (localArea == null)
                {
                    _logger.LogError("Could not find local Area with ID {LocalAreaId} for Organization {OrganizationId}. Skipping preorder {PlatformPreOrderId}.",
                        platformPreOrder.Area.LocalAreaId, organizationId, platformPreOrder.Id);
                    return;
                }

                // 3. Validate Menu Items and Prepare OrderItems
                var orderItems = new List<OrderItem>();
                var validationFailed = false;
                decimal calculatedTotal = 0m;

                // Fetch all required menu items in one go for efficiency
                var requiredMenuItemIds = platformPreOrder.Items.Select(i => i.LocalMenuItemId).Distinct().ToList();
                var localMenuItems = await _context.MenuItems
                    .Where(mi => mi.MenuCategory.AreaId == localArea.Id && requiredMenuItemIds.Contains(mi.Id))
                    .ToDictionaryAsync(mi => mi.Id);

                foreach (var platformItem in platformPreOrder.Items)
                {
                    if (!localMenuItems.TryGetValue(platformItem.LocalMenuItemId, out var localMenuItem))
                    {
                        _logger.LogError("Could not find local MenuItem with ID {LocalMenuItemId} in Area {AreaId} for Organization {OrganizationId}. Skipping preorder {PlatformPreOrderId}.",
                            platformItem.LocalMenuItemId, localArea.Id, organizationId, platformPreOrder.Id);
                        validationFailed = true;
                        break; // Stop processing items for this order if one is invalid
                    }

                    // Try parsing the UnitPrice string from the platform item
                    if (!decimal.TryParse(platformItem.UnitPrice, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal itemUnitPrice))
                    {
                        _logger.LogError("Could not parse UnitPrice string \"{PlatformItemUnitPriceString}\" to decimal for item {ItemName} in Preorder {PlatformPreOrderId}. Skipping preorder.",
                            platformItem.UnitPrice, platformItem.Name, platformPreOrder.Id);
                        validationFailed = true;
                        break; // Stop processing items for this order if parsing fails
                    }

                    // Use the local item's current price, but store the platform price for reference/audit if needed?
                    // Currently, using platform price as per PlatformPreOrderItemDto
                    // var unitPriceToUse = localMenuItem.Price; // Option to use local price instead
                    calculatedTotal += platformItem.Quantity * itemUnitPrice;

                    orderItems.Add(new OrderItem
                    {
                        MenuItemId = localMenuItem.Id,
                        Quantity = platformItem.Quantity,
                        UnitPrice = itemUnitPrice, // Use the parsed decimal value
                        Note = platformItem.Note,
                        KdsStatus = KdsStatus.Pending // Default KDS status
                    });
                }

                if (validationFailed)
                {
                    return; // Don't import if item validation failed
                }

                // Try parsing the TotalAmount string from the platform
                if (!decimal.TryParse(platformPreOrder.TotalAmount, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal platformTotalAmount))
                {
                    _logger.LogError("Could not parse TotalAmount string \"{PlatformTotalAmountString}\" to decimal for Preorder {PlatformPreOrderId}. Skipping import.",
                        platformPreOrder.TotalAmount, platformPreOrder.Id);
                    return;
                }

                // Optional: Compare calculated total with platform total
                if (Math.Abs(calculatedTotal - platformTotalAmount) > 0.01m) // Allow small tolerance
                {
                    _logger.LogWarning("Calculated total ({CalculatedTotal}) differs from platform total ({PlatformTotal}) for Preorder {PlatformPreOrderId}. Importing anyway.",
                        calculatedTotal, platformTotalAmount, platformPreOrder.Id);
                }

                // 4. Create Local Order
                var newOrder = new Order
                {
                    Id = OrderIdGenerator.Generate(organizationId, localArea.Id), // Generate ID
                    PreOrderPlatformId = platformPreOrder.Id,
                    OrganizationId = organizationId,
                    AreaId = localArea.Id,
                    OrderDateTime = platformPreOrder.OrderDateTime.UtcDateTime, // Use platform time (convert to UTC DateTime)
                    Status = OrderStatus.PreOrder, // Set initial status
                    TotalAmount = calculatedTotal, // Use the parsed decimal value
                    CustomerName = platformPreOrder.CustomerName,
                    CustomerEmail = platformPreOrder.CustomerEmail, // Add email
                    // DayId = null, // Cannot associate with a Day until confirmed/paid locally
                    OrderItems = orderItems, // Corrected property name
                    NumberOfGuests = platformPreOrder.NumberOfGuests,
                    IsTakeaway = platformPreOrder.IsTakeaway
                };

                _context.Orders.Add(newOrder);
                await _context.SaveChangesAsync(); // Save the single order

                _logger.LogInformation("Successfully imported Preorder {PlatformPreOrderId} as local Order {LocalOrderId}.", platformPreOrder.Id, newOrder.Id);

                // Add ID to the list for marking as fetched (use lock for thread safety)
                lock (successfullyImportedIds)
                {
                    successfullyImportedIds.Add(platformPreOrder.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing Preorder {PlatformPreOrderId} for Organization {OrganizationId}.", platformPreOrder.Id, organizationId);
                // Do not add to successfullyImportedIds if an error occurred during import
            }
        }

        private async Task MarkOrdersAsFetchedAsync(SyncConfiguration syncConfig, List<string> preOrderIds, CancellationToken cancellationToken)
        {
            if (!preOrderIds.Any()) return;

            _logger.LogInformation("Marking {Count} preorders as fetched on the platform...", preOrderIds.Count);

            var client = _httpClientFactory.CreateClient("PreOrderPlatformClient");
            var markFetchedUrl = $"{syncConfig.PlatformBaseUrl.TrimEnd('/')}/api/preorders/mark-fetched";

            var payload = new { PreOrderIds = preOrderIds };

            var request = new HttpRequestMessage(HttpMethod.Post, markFetchedUrl);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.Add("Authorization", $"ApiKey {syncConfig.ApiKey}");
            request.Content = new StringContent(JsonSerializer.Serialize(payload, _jsonOptions), Encoding.UTF8, "application/json");
            // Alternative using JsonContent for simpler serialization:
            // request.Content = JsonContent.Create(payload, options: _jsonOptions);

            try
            {
                var response = await client.SendAsync(request, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError("Failed to mark preorders as fetched. Status: {StatusCode}, Response: {ErrorContent}, IDs: {PreOrderIds}",
                        response.StatusCode, errorContent, string.Join(", ", preOrderIds));
                }
                else
                {
                    var responseContent = await response.Content.ReadFromJsonAsync<PlatformMarkFetchedResponse>(_jsonOptions, cancellationToken);
                    _logger.LogInformation("Successfully marked preorders as fetched. Response: Updated={UpdatedCount}, AlreadyFetched={AlreadyFetchedCount}, TotalRequested={TotalRequested}",
                        responseContent?.UpdatedCount ?? 0,
                        responseContent?.AlreadyFetchedCount ?? 0,
                        responseContent?.TotalRequested ?? 0);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending mark-fetched request for PreOrder IDs: {PreOrderIds}", string.Join(", ", preOrderIds));
            }
        }
    }

    // Helper DTO for the mark-fetched response
    internal class PlatformMarkFetchedResponse
    {
        public bool Success { get; set; }
        public int UpdatedCount { get; set; }
        public int AlreadyFetchedCount { get; set; }
        public int TotalRequested { get; set; }
    }
}
