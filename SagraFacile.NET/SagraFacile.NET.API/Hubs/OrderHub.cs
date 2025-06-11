using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace SagraFacile.NET.API.Hubs
{
    // Basic SignalR Hub for broadcasting order-related events.
    // Specific methods for client-to-server calls can be added if needed,
    // but primary use here is server-to-client via IHubContext.
    // Also handles registration of WindowsPrinterService instances.
    public class OrderHub : Hub
    {
        // Thread-safe dictionary to map Printer.ConnectionString (GUID for USB printers) to SignalR ConnectionId.
        // This allows the PrintService to send targeted messages to specific WindowsPrinterService instances.
        private static readonly ConcurrentDictionary<string, string> _printerConnections = new ConcurrentDictionary<string, string>();
        private readonly ILogger<OrderHub> _logger;

        public OrderHub(ILogger<OrderHub> logger)
        {
            _logger = logger;
        }

        // Example: Client could potentially join groups based on KDS station ID
        // public async Task JoinKdsStationGroup(string kdsStationId)
        // {
        //     await Groups.AddToGroupAsync(Context.ConnectionId, $"KDS_{kdsStationId}");
        // }

        // public async Task LeaveKdsStationGroup(string kdsStationId)
        // {
        //     await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"KDS_{kdsStationId}");
        // }

        // Method for clients (Cashier, Queue Display) to join a group for a specific Area's queue updates.
        public async Task JoinAreaQueueGroup(string areaId)
        {
            if (string.IsNullOrWhiteSpace(areaId))
            {
                _logger.LogWarning($"Attempt to join AreaQueueGroup with null or empty areaId. ConnectionId: {Context.ConnectionId}");
                return;
            }
            string groupName = $"Area-{areaId}";
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
            _logger.LogInformation($"Client {Context.ConnectionId} joined group {groupName} for Area ID {areaId}.");
        }

        // Method for clients to leave a group for a specific Area's queue updates.
        public async Task LeaveAreaQueueGroup(string areaId)
        {
            if (string.IsNullOrWhiteSpace(areaId))
            {
                _logger.LogWarning($"Attempt to leave AreaQueueGroup with null or empty areaId. ConnectionId: {Context.ConnectionId}");
                return;
            }
            string groupName = $"Area-{areaId}";
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
            _logger.LogInformation($"Client {Context.ConnectionId} left group {groupName} for Area ID {areaId}.");
        }

        /// <summary>
        /// Called by the SagraFacile.WindowsPrinterService to register itself with the hub.
        /// This allows the backend to send print jobs to this specific printer client.
        /// </summary>
        /// <param name="printerGuid">The unique GUID assigned to the printer configuration in SagraFacile,
        /// which corresponds to Printer.ConnectionString for WindowsUsb printers.</param>
        public async Task RegisterPrinterClient(string printerGuid)
        {
            if (string.IsNullOrWhiteSpace(printerGuid))
            {
                _logger.LogWarning($"Attempt to register printer client with null or empty GUID. ConnectionId: {Context.ConnectionId}");
                // Optionally, you could send an error back to the client or close the connection.
                return;
            }

            _printerConnections[printerGuid] = Context.ConnectionId;
            _logger.LogInformation($"Printer client registered. GUID: {printerGuid}, ConnectionId: {Context.ConnectionId}");
            // Optional: Send a confirmation back to the client
            // await Clients.Caller.SendAsync("RegistrationConfirmed", $"Successfully registered printer GUID: {printerGuid}");
        }

        public override async Task OnConnectedAsync()
        {
            // Optional: Logic when a client connects
            _logger.LogInformation($"Client connected: {Context.ConnectionId}");
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(System.Exception? exception)
        {
            // Optional: Logic when a client disconnects
            _logger.LogInformation($"Client disconnected: {Context.ConnectionId}. Exception: {exception?.Message}");

            // If the disconnected client was a registered printer, remove it from the dictionary.
            // This involves iterating because we have ConnectionId and need to find the GUID.
            // For performance, if many printers are expected, consider a reverse mapping or a more complex structure.
            string? printerGuidToRemove = null;
            foreach (var entry in _printerConnections)
            {
                if (entry.Value == Context.ConnectionId)
                {
                    printerGuidToRemove = entry.Key;
                    break;
                }
            }

            if (printerGuidToRemove != null)
            {
                if (_printerConnections.TryRemove(printerGuidToRemove, out _))
                {
                    _logger.LogInformation($"Printer client unregistered due to disconnection. GUID: {printerGuidToRemove}, ConnectionId: {Context.ConnectionId}");
                }
                else
                {
                    // This might happen if the connection was removed by another thread or if the dictionary state is unexpected.
                    _logger.LogWarning($"Failed to unregister printer client on disconnection. GUID: {printerGuidToRemove}, ConnectionId: {Context.ConnectionId}. It might have been already removed.");
                }
            }

            await base.OnDisconnectedAsync(exception);
        }

        // Method for the PrintService to get the ConnectionId for a registered printer.
        // This is not directly callable by clients but used internally by services that have access to IHubContext<OrderHub>.
        // However, a better approach is for the PrintService to directly access _printerConnections
        // or for this Hub to expose a service that PrintService can use.
        // For simplicity and since _printerConnections is static, PrintService can be designed to access it
        // or we can inject a service that manages these connections.
        // For now, PrintService will need a way to get this.
        // A simple static accessor method could be:
        public static string? GetConnectionIdForPrinter(string printerGuid)
        {
            _printerConnections.TryGetValue(printerGuid, out var connectionId);
            return connectionId;
        }
    }
}
