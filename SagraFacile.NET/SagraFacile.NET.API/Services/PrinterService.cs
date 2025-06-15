using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SagraFacile.NET.API.Data;
using SagraFacile.NET.API.DTOs;
using SagraFacile.NET.API.Models;
using SagraFacile.NET.API.Models.Enums;
using SagraFacile.NET.API.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Text;
using Microsoft.AspNetCore.SignalR;
using SagraFacile.NET.API.Hubs;
using SagraFacile.NET.API.Utils;
using ESCPOS_NET.Emitters; // Added for CodePage enum

namespace SagraFacile.NET.API.Services
{
    public class PrinterService : BaseService, IPrinterService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<PrinterService> _logger;
        private readonly IHubContext<OrderHub> _orderHubContext;

        public PrinterService(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor, ILogger<PrinterService> logger, IHubContext<OrderHub> orderHubContext)
            : base(httpContextAccessor)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _orderHubContext = orderHubContext ?? throw new ArgumentNullException(nameof(orderHubContext));
        }

        public async Task<IEnumerable<PrinterDto>> GetPrintersAsync()
        {
            var (userOrgId, isSuperAdmin) = GetUserContext();

            var query = _context.Printers.AsQueryable();

            if (!isSuperAdmin)
            {
                if (!userOrgId.HasValue)
                {
                    throw new InvalidOperationException("User organization context is missing for non-SuperAdmin.");
                }
                query = query.Where(p => p.OrganizationId == userOrgId.Value);
            }
            // SuperAdmin sees all printers without filtering

            return await query.Select(p => MapPrinterToDto(p)).ToListAsync();
        }

        public async Task<PrinterDto?> GetPrinterByIdAsync(int id)
        {
            var (userOrgId, isSuperAdmin) = GetUserContext();
            var printer = await _context.Printers.FindAsync(id);

            if (printer == null)
            {
                return null;
            }

            // Authorization check
            if (!isSuperAdmin && printer.OrganizationId != userOrgId)
            {
                return null; // Or throw UnauthorizedAccessException
            }

            return MapPrinterToDto(printer);
        }

        public async Task<(Printer? Printer, string? Error)> CreatePrinterAsync(PrinterUpsertDto printerDto)
        {
            var (userOrgId, isSuperAdmin) = GetUserContext();

            // Validation 1: Correct OrganizationId assignment/validation
            if (!isSuperAdmin)
            {
                if (!userOrgId.HasValue)
                {
                    return (null, "User organization context is missing.");
                }
                if (printerDto.OrganizationId != 0 && printerDto.OrganizationId != userOrgId.Value)
                {
                    return (null, "User is not authorized to create a printer for a different organization.");
                }
                printerDto.OrganizationId = userOrgId.Value; // Assign user's org ID
            }
            else // SuperAdmin must provide a valid OrgId
            {
                if (printerDto.OrganizationId == 0 || !await _context.Organizations.AnyAsync(o => o.Id == printerDto.OrganizationId))
                {
                    return (null, $"Invalid or non-existent OrganizationId: {printerDto.OrganizationId}");
                }
            }

            // Validation 2: Ensure organization exists (redundant for non-SuperAdmin if checks above are done)
            if (!await _context.Organizations.AnyAsync(o => o.Id == printerDto.OrganizationId))
            {
                return (null, $"Organization with ID {printerDto.OrganizationId} not found.");
            }

            var printer = new Printer
            {
                Name = printerDto.Name,
                Type = printerDto.Type,
                ConnectionString = printerDto.ConnectionString,
                WindowsPrinterName = printerDto.WindowsPrinterName,
                IsEnabled = printerDto.IsEnabled,
                OrganizationId = printerDto.OrganizationId,
                PrintMode = printerDto.PrintMode // Added PrintMode
            };

            _context.Printers.Add(printer);
            try
            {
                await _context.SaveChangesAsync();
                return (printer, null);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Error creating printer.");
                return (null, "An error occurred while saving the printer.");
            }
        }

        public async Task<(bool Success, string? Error)> UpdatePrinterAsync(int id, PrinterUpsertDto printerDto)
        {
            var (userOrgId, isSuperAdmin) = GetUserContext();

            var existingPrinter = await _context.Printers.FindAsync(id);
            if (existingPrinter == null)
            {
                return (false, "Printer not found.");
            }

            // Authorization check 1: Ownership
            if (!isSuperAdmin && existingPrinter.OrganizationId != userOrgId)
            {
                return (false, "User is not authorized to update this printer.");
            }

            // Authorization check 2: Changing OrganizationId (only SuperAdmin)
            if (!isSuperAdmin && existingPrinter.OrganizationId != printerDto.OrganizationId)
            {
                return (false, "User is not authorized to change the printer's organization.");
            }

            // Validation 1: Target OrganizationId validity (if changed by SuperAdmin)
            if (isSuperAdmin && existingPrinter.OrganizationId != printerDto.OrganizationId)
            {
                if (!await _context.Organizations.AnyAsync(o => o.Id == printerDto.OrganizationId))
                {
                    return (false, $"Target organization with ID {printerDto.OrganizationId} not found.");
                }
                existingPrinter.OrganizationId = printerDto.OrganizationId;
            }

            // Update properties
            existingPrinter.Name = printerDto.Name;
            existingPrinter.Type = printerDto.Type;
            existingPrinter.ConnectionString = printerDto.ConnectionString;
            existingPrinter.WindowsPrinterName = printerDto.WindowsPrinterName;
            existingPrinter.IsEnabled = printerDto.IsEnabled;
            existingPrinter.PrintMode = printerDto.PrintMode; // Added PrintMode
            // OrganizationId is updated above if applicable

            try
            {
                await _context.SaveChangesAsync();
                return (true, null);
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await PrinterExistsAsync(id))
                {
                    return (false, "Printer not found (concurrency issue).");
                }
                else
                {
                    _logger.LogWarning("Concurrency exception during printer update for ID {PrinterId}", id);
                    return (false, "Failed to update printer due to a concurrency conflict.");
                }
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Error updating printer with ID {PrinterId}", id);
                return (false, "An error occurred while saving the printer update.");
            }
        }

        public async Task<(bool Success, string? Error)> DeletePrinterAsync(int id)
        {
            var (userOrgId, isSuperAdmin) = GetUserContext();

            var printer = await _context.Printers.FindAsync(id);
            if (printer == null)
            {
                return (false, "Printer not found.");
            }

            // Authorization check: Ownership
            if (!isSuperAdmin && printer.OrganizationId != userOrgId)
            {
                return (false, "User is not authorized to delete this printer.");
            }

            // Check for dependencies (e.g., if used as Area Receipt Printer)
            bool isInUseAsReceipt = await _context.Areas.AnyAsync(a => a.ReceiptPrinterId == id);
            if (isInUseAsReceipt)
            {
                return (false, "Cannot delete printer because it is assigned as a Receipt Printer for one or more Areas.");
            }
            // Future: Check if used in PrinterCategoryAssignments if needed for strict delete

            _context.Printers.Remove(printer);
            try
            {
                await _context.SaveChangesAsync();
                return (true, null);
            }
            catch (DbUpdateException ex)
            {
                // This might occur if there's a race condition or unexpected FK constraint
                _logger.LogError(ex, "Error deleting printer with ID {PrinterId}", id);
                return (false, "An error occurred while deleting the printer.");
            }
        }

        public async Task<bool> PrinterExistsAsync(int id)
        {
            // Basic existence check, authorization should happen before calling this if needed.
            return await _context.Printers.AnyAsync(e => e.Id == id);
        }

        // --- Helper Methods ---

        private static PrinterDto MapPrinterToDto(Printer printer)
        {
            return new PrinterDto
            {
                Id = printer.Id,
                Name = printer.Name,
                Type = printer.Type,
                ConnectionString = printer.ConnectionString,
                WindowsPrinterName = printer.WindowsPrinterName,
                IsEnabled = printer.IsEnabled,
                OrganizationId = printer.OrganizationId,
                PrintMode = printer.PrintMode // Added PrintMode
            };
        }

        // Implementation for IPrinterService
        public async Task<(bool Success, string? Error)> SendToPrinterAsync(Printer printer, byte[] data, PrintJobType jobType) // Added jobType for context
        {
            if (!printer.IsEnabled)
            {
                _logger.LogWarning($"Attempted to send job type {jobType} to disabled printer {printer.Name} (ID: {printer.Id}).");
                return (false, "Printer is disabled.");
            }

            _logger.LogInformation($"Sending job type {jobType} to printer {printer.Name} (ID: {printer.Id}), Type: {printer.Type}");

            try
            {
                if (printer.Type == PrinterType.Network)
                {
                    if (string.IsNullOrWhiteSpace(printer.ConnectionString))
                    {
                        _logger.LogError($"Network printer {printer.Name} (ID: {printer.Id}) has no connection string.");
                        return (false, "Network printer connection string is missing.");
                    }

                    var parts = printer.ConnectionString.Split(':');
                    if (parts.Length != 2 || !int.TryParse(parts[1], out int port))
                    {
                        _logger.LogError($"Invalid network printer connection string format for {printer.Name} (ID: {printer.Id}): {printer.ConnectionString}");
                        return (false, "Invalid network printer connection string format.");
                    }
                    string ipAddress = parts[0];

                    // Using the existing SendToNetworkPrinterAsync logic if suitable, or inline it.
                    // For this example, let's refine and use a direct TCP send.
                    using (var client = new TcpClient())
                    {
                        // Set timeouts (e.g., 5 seconds for connect, 10 seconds for send/receive)
                        client.SendTimeout = 10000;
                        client.ReceiveTimeout = 10000;

                        var connectTask = client.ConnectAsync(ipAddress, port);
                        if (await Task.WhenAny(connectTask, Task.Delay(5000)) == connectTask && client.Connected)
                        {
                            using (var stream = client.GetStream())
                            {
                                await stream.WriteAsync(data, 0, data.Length);
                                await stream.FlushAsync();
                                _logger.LogInformation($"Successfully sent data to network printer {printer.Name} at {ipAddress}:{port}");
                                return (true, null);
                            }
                        }
                        else
                        {
                            _logger.LogError($"Failed to connect to network printer {printer.Name} at {ipAddress}:{port} within timeout or connection failed.");
                            return (false, "Failed to connect to network printer.");
                        }
                    }
                }
                else if (printer.Type == PrinterType.WindowsUsb)
                {
                    if (string.IsNullOrWhiteSpace(printer.ConnectionString))
                    {
                        _logger.LogError($"WindowsUSB printer {printer.Name} (ID: {printer.Id}) has no ConnectionString (GUID).");
                        return (false, "WindowsUSB printer ConnectionString (GUID) is missing.");
                    }
                    if (string.IsNullOrWhiteSpace(printer.WindowsPrinterName))
                    {
                        _logger.LogError($"WindowsUSB printer {printer.Name} (ID: {printer.Id}) has no WindowsPrinterName configured.");
                        return (false, "WindowsUSB printer WindowsPrinterName is missing.");
                    }

                    var connectionId = OrderHub.GetConnectionIdForPrinter(printer.ConnectionString);
                    if (connectionId != null)
                    {
                        string jobId = Guid.NewGuid().ToString(); // Generate a unique Job ID
                        _logger.LogInformation($"Dispatching print job (JobID: {jobId}) to WindowsUSB printer {printer.Name} (Windows Name: {printer.WindowsPrinterName}) via SignalR ConnectionId: {connectionId}");
                        //send directly byte[]
                        await _orderHubContext.Clients.Client(connectionId).SendAsync(
                            "PrintJob",                 // Method name
                            jobId,                      // First argument: Job ID
                            printer.WindowsPrinterName, // Second argument: Windows Printer Name
                            data);                // Third argument: byte[]
                        return (true, null);
                    }
                    else
                    {
                        _logger.LogWarning($"WindowsUSB printer {printer.Name} (GUID: {printer.ConnectionString}) is not currently connected or registered with the OrderHub.");
                        return (false, "WindowsUSB printer is not connected.");
                    }
                }
                else
                {
                    _logger.LogError($"Unsupported printer type: {printer.Type} for printer {printer.Name} (ID: {printer.Id})");
                    return (false, "Unsupported printer type.");
                }
            }
            catch (SocketException ex)
            {
                _logger.LogError(ex, $"SocketException while sending to printer {printer.Name} (ID: {printer.Id}): {ex.Message}");
                return (false, $"Network error while printing: {ex.Message}");
            }
            catch (System.Exception ex) // Catch-all for other unexpected errors
            {
                _logger.LogError(ex, $"Unexpected error while sending to printer {printer.Name} (ID: {printer.Id}): {ex.Message}");
                return (false, $"An unexpected error occurred during printing: {ex.Message}");
            }
        }

        // Placeholder for PrintOrderDocumentsAsync - to be implemented next
        public async Task<(bool Success, string? Error)> PrintOrderDocumentsAsync(Order order, PrintJobType jobType)
        {
            _logger.LogInformation($"Processing PrintOrderDocumentsAsync for Order ID: {order.Id}, JobType: {jobType}");

            // Ensure the order object passed has all necessary navigation properties loaded.
            // This is crucial for the logic below.
            // If 'order' comes directly from a simple DbContext.FindAsync(id), related entities might be null.
            // It's often better to fetch it with explicit .Include() calls where it's retrieved before calling this service,
            // or do it here if this service is responsible for ensuring data integrity for its operations.

            // Let's try to load or ensure related data is present.
            // Note: This assumes 'order.Id' is valid. Consider adding a check.
            var orderWithDetails = await _context.Orders
                .Include(o => o.Organization) // Needed for org context if not implicitly handled
                .Include(o => o.Area)
                    .ThenInclude(a => a!.ReceiptPrinter)
                .Include(o => o.CashierStation)
                    .ThenInclude(cs => cs!.ReceiptPrinter) // Cashier station's printer
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.MenuItem)
                        .ThenInclude(mi => mi!.MenuCategory) // For comanda routing by category
                .FirstOrDefaultAsync(o => o.Id == order.Id);

            if (orderWithDetails == null)
            {
                _logger.LogError($"Order with ID {order.Id} not found when attempting to print.");
                return (false, $"Order with ID {order.Id} not found.");
            }

            // Replace the passed 'order' with the fully loaded one.
            order = orderWithDetails;

            var printTasks = new List<Task<(bool Success, string? Error)>>();

            if (jobType == PrintJobType.Receipt)
            {
                _logger.LogInformation($"Determining printer for RECEIPT for Order ID: {order.Id}.");
                Printer? receiptPrinter = null;

                if (order.CashierStationId.HasValue && order.CashierStation != null && order.CashierStation.ReceiptPrinterId != 0)
                {
                    receiptPrinter = order.CashierStation.ReceiptPrinter;
                    if (receiptPrinter != null)
                    {
                        _logger.LogInformation($"Using Cashier Station '{order.CashierStation.Name}' (ID: {order.CashierStation.Id}) assigned receipt printer: {receiptPrinter.Name} (ID: {receiptPrinter.Id})");
                    }
                    else
                    {
                        _logger.LogWarning($"Cashier Station '{order.CashierStation.Name}' has ReceiptPrinterId {order.CashierStation.ReceiptPrinterId} but navigation property is null.");
                    }
                }
                if (receiptPrinter == null && order.Area != null && order.Area.ReceiptPrinterId != 0)
                {
                    receiptPrinter = order.Area.ReceiptPrinter;
                    if (receiptPrinter != null)
                    {
                        _logger.LogInformation($"Using Area '{order.Area.Name}' (ID: {order.Area.Id}) default receipt printer: {receiptPrinter.Name} (ID: {receiptPrinter.Id})");
                    }
                    else
                    {
                        _logger.LogWarning($"Area '{order.Area.Name}' has ReceiptPrinterId {order.Area.ReceiptPrinterId} but navigation property is null.");
                    }
                }

                if (receiptPrinter != null)
                {
                    if (receiptPrinter.IsEnabled)
                    {
                        var docBuilder = new EscPosDocumentBuilder();
                        docBuilder.InitializePrinter(); // This already sets PC858_EURO in the builder
                        // Explicitly re-select for absolute clarity in this service's logic.
                        docBuilder.SelectCharacterCodeTable(CodePage.PC858_EURO); 
                        docBuilder.SetAlignment(EscPosAlignment.Center);
                        docBuilder.SetEmphasis(true);
                        docBuilder.AppendLine(order.Organization?.Name ?? "Sagrafacile");
                        docBuilder.SetEmphasis(false);
                        docBuilder.AppendLine($"Area: {order.Area?.Name ?? "N/A"}");
                        if (order.CashierStation != null)
                        {
                            docBuilder.AppendLine($"Cassa: {order.CashierStation.Name}");
                        }
                        docBuilder.AppendLine($"Ordine N. {order.DisplayOrderNumber ?? order.Id}"); // Use DisplayOrderNumber
                        docBuilder.AppendLine(order.OrderDateTime.ToString("dd/MM/yyyy HH:mm:ss"));
                        if (order.IsTakeaway)
                        {
                            docBuilder.SetEmphasis(true);
                            docBuilder.SetFontSize(2, 2);
                            docBuilder.AppendLine("ASPORTO");
                            docBuilder.ResetFontSize();
                            docBuilder.SetEmphasis(false);
                        }
                        if (order.NumberOfGuests > 0 && !order.IsTakeaway)
                        {
                            docBuilder.AppendLine($"Coperti: {order.NumberOfGuests}");
                        }
                        docBuilder.AppendLine($"Cliente: {order.CustomerName ?? "Anonimo"}");
                        docBuilder.SetAlignment(EscPosAlignment.Left);
                        docBuilder.AppendLine("--------------------------------"); // Use a fixed width separator

                        // Group items by category for receipt
                        var itemsByCategory = order.OrderItems
                            .GroupBy(oi => oi.MenuItem?.MenuCategoryId)
                            .Select(g => new
                            {
                                CategoryId = g.Key,
                                CategoryName = g.First().MenuItem?.MenuCategory?.Name ?? "Senza Categoria",
                                Items = g.ToList()
                            })
                            .OrderBy(g => g.CategoryName);

                        foreach (var categoryGroup in itemsByCategory)
                        {
                            docBuilder.SetEmphasis(true);
                            docBuilder.AppendLine(categoryGroup.CategoryName.ToUpper());
                            docBuilder.SetEmphasis(false);
                            foreach (var item in categoryGroup.Items)
                            {
                                string itemName = item.MenuItem?.Name ?? "Articolo Sconosciuto";
                                string quantityPrice = $"{item.Quantity} x {item.UnitPrice:C}";
                                string totalPrice = (item.Quantity * item.UnitPrice).ToString("C");
                                docBuilder.AppendLine($"{itemName.PadRight(20).Substring(0, 20)} {quantityPrice.PadRight(10)} {totalPrice.PadLeft(8)}");
                                if (!string.IsNullOrWhiteSpace(item.Note))
                                {
                                    docBuilder.SetAlignment(EscPosAlignment.Left); // Ensure left alignment
                                    // Standard font size for notes on receipts, indented.
                                    docBuilder.AppendLine($"    Nota: {item.Note.Trim()}");
                                }
                            }
                        }
                        docBuilder.AppendLine("--------------------------------");

                        // Calculate and print Subtotal for items
                        decimal itemsSubtotal = order.OrderItems.Sum(oi => oi.Quantity * oi.UnitPrice);
                        docBuilder.SetAlignment(EscPosAlignment.Right);
                        docBuilder.SetEmphasis(true);
                        docBuilder.AppendLine($"SUBTOTALE: {itemsSubtotal:C}");
                        docBuilder.SetEmphasis(false);
                        docBuilder.SetAlignment(EscPosAlignment.Left); // Reset alignment for charges if any
                        docBuilder.AppendLine("--------------------------------");


                        // Add charges if applicable
                        if (order.IsTakeaway && order.Area?.TakeawayCharge > 0)
                        {
                            docBuilder.SetAlignment(EscPosAlignment.Left);
                            docBuilder.AppendLine($"Contributo Asporto: {order.Area.TakeawayCharge:C}");
                        }
                        else if (!order.IsTakeaway && order.Area?.GuestCharge > 0 && order.NumberOfGuests > 0)
                        {
                            docBuilder.SetAlignment(EscPosAlignment.Left);
                            docBuilder.AppendLine($"Coperto ({order.NumberOfGuests} x {order.Area.GuestCharge:C}): {(order.NumberOfGuests * order.Area.GuestCharge):C}");
                        }

                        docBuilder.SetAlignment(EscPosAlignment.Right);
                        docBuilder.SetEmphasis(true);
                        docBuilder.SetFontSize(1, 2); // Wider and taller
                        docBuilder.AppendLine($"TOTALE: {order.TotalAmount:C}");
                        docBuilder.ResetFontSize();
                        docBuilder.SetEmphasis(false);

                        docBuilder.SetAlignment(EscPosAlignment.Center);
                        docBuilder.NewLine();
                        // Example QR Code (Order ID for now)
                        // Ensure your printer supports QR codes and test this part.
                        // The content should ideally be a URL for pre-orders if this is a pre-order receipt,
                        // or internal lookup ID.
                        try
                        {
                            // Increased QR code module size from 6 to 8 for better scannability
                            docBuilder.PrintQRCode($"Sagrafacile_Order_{order.Id}", 8);
                            docBuilder.NewLine();
                        }
                        catch (Exception qrEx)
                        {
                            _logger.LogError(qrEx, $"Failed to generate QR code for Order ID: {order.Id}. QR generation in EscPosDocumentBuilder might need adjustment for your printer.");
                            docBuilder.AppendLine("QR Code non disponibile.");
                        }

                        docBuilder.AppendLine("Grazie e arrivederci!");
                        docBuilder.NewLine(5); // Add more space before cutting
                        docBuilder.CutPaper();

                        printTasks.Add(SendToPrinterAsync(receiptPrinter, docBuilder.Build(), PrintJobType.Receipt));

                    }
                    else
                    {
                        _logger.LogWarning($"Receipt printer '{receiptPrinter.Name}' (ID: {receiptPrinter.Id}) is disabled for Order ID: {order.Id}.");
                        // Decide if this is an error or just a skipped print
                    }
                }
                else
                {
                    _logger.LogWarning($"No receipt printer configured for Order ID: {order.Id} (CashierStation: {order.CashierStationId}, Area: {order.AreaId}).");
                    // This could be an error or expected behavior if printing is optional / not configured
                }
            }
            else if (jobType == PrintJobType.Comanda)
            {
                _logger.LogInformation($"Determining printer(s) for COMANDA for Order ID: {order.Id}.");
                var comandasToSend = new Dictionary<Printer, List<byte>>();

                // Scenario 1: Print Comandas At This (Cashier) Station's Receipt Printer
                if (order.CashierStationId.HasValue && order.CashierStation?.PrintComandasAtThisStation == true && order.CashierStation.ReceiptPrinterId != 0)
                {
                    var stationPrinter = order.CashierStation.ReceiptPrinter;
                    if (stationPrinter != null && stationPrinter.IsEnabled)
                    {
                        _logger.LogInformation($"Printing ALL comandas for Order ID: {order.Id} at Cashier Station '{order.CashierStation.Name}' printer: {stationPrinter.Name}");
                        string comandaTitle = $"COMANDA - Stazione: {order.CashierStation.Name}";
                        byte[] escPosComanda = GenerateEscPosComanda(order, order.OrderItems, comandaTitle, groupAndShowCategoriesWithinComanda: true); // Show categories even here
                        if (!comandasToSend.ContainsKey(stationPrinter)) comandasToSend[stationPrinter] = new List<byte>();
                        comandasToSend[stationPrinter].AddRange(escPosComanda);
                    }
                    else if (stationPrinter != null && !stationPrinter.IsEnabled)
                    {
                        _logger.LogWarning($"Comanda printer (station specific) '{stationPrinter.Name}' for Order {order.Id} is disabled.");
                    }
                }
                // Scenario 2: Print Comandas At Area's Default Receipt Printer (if not handled by station-specific)
                else if (order.Area?.PrintComandasAtCashier == true && order.Area.ReceiptPrinterId != 0)
                {
                    var areaDefaultPrinter = order.Area.ReceiptPrinter;
                    if (areaDefaultPrinter != null && areaDefaultPrinter.IsEnabled)
                    {
                        _logger.LogInformation($"Printing ALL comandas for Order ID: {order.Id} at Area '{order.Area.Name}' default printer: {areaDefaultPrinter.Name}");
                        string comandaTitle = $"COMANDA - Area: {order.Area.Name}";
                        byte[] escPosComanda = GenerateEscPosComanda(order, order.OrderItems, comandaTitle, groupAndShowCategoriesWithinComanda: true); // Show categories even here
                        if (!comandasToSend.ContainsKey(areaDefaultPrinter)) comandasToSend[areaDefaultPrinter] = new List<byte>();
                        comandasToSend[areaDefaultPrinter].AddRange(escPosComanda);
                    }
                    else if (areaDefaultPrinter != null && !areaDefaultPrinter.IsEnabled)
                    {
                        _logger.LogWarning($"Comanda printer (area default) '{areaDefaultPrinter.Name}' for Order {order.Id} is disabled.");
                    }
                }
                // Scenario 3: Print Comandas based on Category Assignments
                else
                {
                    _logger.LogInformation($"Printing comandas based on category assignments for Order ID: {order.Id}.");
                    var allCategoryAssignments = await _context.PrinterCategoryAssignments
                        .Include(pca => pca.Printer)
                        .Include(pca => pca.MenuCategory) // Ensure MenuCategory is loaded for GroupItemsByComandaPrinter
                        .Where(pca => pca.Printer.OrganizationId == order.OrganizationId && pca.Printer.IsEnabled)
                        .ToListAsync();

                    Dictionary<Printer, List<OrderItem>> comandaItemGroups = GroupItemsByComandaPrinter(order, allCategoryAssignments);

                    foreach (var kvp in comandaItemGroups)
                    {
                        Printer printer = kvp.Key;
                        List<OrderItem> itemsForPrinter = kvp.Value;

                        if (itemsForPrinter.Any())
                        {
                            _logger.LogInformation($"Generating comanda for printer '{printer.Name}' (ID: {printer.Id}) with {itemsForPrinter.Count} items for Order {order.Id}.");
                            string comandaTitle = $"COMANDA - {printer.Name}";
                            byte[] escPosComanda = GenerateEscPosComanda(order, itemsForPrinter, comandaTitle, groupAndShowCategoriesWithinComanda: true);
                            if (!comandasToSend.ContainsKey(printer)) comandasToSend[printer] = new List<byte>();
                            comandasToSend[printer].AddRange(escPosComanda);
                        }
                    }
                }

                // Dispatch all collected comandas
                foreach (var entry in comandasToSend)
                {
                    if (entry.Value.Count > 0) // Ensure there's something to print
                    {
                        printTasks.Add(SendToPrinterAsync(entry.Key, entry.Value.ToArray(), PrintJobType.Comanda));
                    }
                }
                if (!comandasToSend.Any() && order.OrderItems.Any())
                {
                    _logger.LogWarning($"No comanda printers were identified or all were disabled for Order ID: {order.Id}, but order has items.");
                }
            }
            else if (jobType == PrintJobType.TestPrint)
            {
                // This requires a specific printer to be identified, perhaps passed in or configured for testing.
                // For now, let's assume a test print is requested for a known printer ID if we were to implement this fully.
                // This example will not send a test print unless explicitly told which printer.
                _logger.LogInformation($"TestPrint job type received for Order ID: {order.Id}. Logic to select a printer for test not implemented here.");
                // Example: Find first available printer in the org.
                var testPrinter = await _context.Printers
                                    .Where(p => p.OrganizationId == order.OrganizationId && p.IsEnabled)
                                    .FirstOrDefaultAsync();
                if (testPrinter != null)
                {
            var docBuilder = new EscPosDocumentBuilder();
            docBuilder.InitializePrinter(); // This already sets PC858_EURO in the builder
            // Explicitly re-select for absolute clarity in this service's logic.
            docBuilder.SelectCharacterCodeTable(CodePage.PC858_EURO);
            docBuilder.SetAlignment(EscPosAlignment.Center);
            docBuilder.SetEmphasis(true);
                    docBuilder.SetFontSize(2, 1);
                    docBuilder.AppendLine("--- TEST PRINT ---");
                    docBuilder.ResetFontSize();
                    docBuilder.SetEmphasis(false);
                    docBuilder.SetAlignment(EscPosAlignment.Left);
                    docBuilder.AppendLine($"Stampante: {testPrinter.Name} (ID: {testPrinter.Id})");
                    docBuilder.AppendLine($"Tipo: {testPrinter.Type}");
                    docBuilder.AppendLine($"Stringa Connessione: {testPrinter.ConnectionString}");
                    if (testPrinter.Type == PrinterType.WindowsUsb)
                    {
                        docBuilder.AppendLine($"Nome Windows: {testPrinter.WindowsPrinterName ?? "N/A"}");
                    }
                    docBuilder.AppendLine($"Ora Test: {DateTime.UtcNow:dd/MM/yyyy HH:mm:ss UTC}");
                    docBuilder.NewLine();
                    docBuilder.AppendLine("--------------------------------");
                    docBuilder.AppendLine("Test caratteri standard.");
                    docBuilder.SetEmphasis(true);
                    docBuilder.AppendLine("Test caratteri grassetto.");
                    docBuilder.SetEmphasis(false);
                    docBuilder.SetFontSize(2, 1);
                    docBuilder.AppendLine("Test Larga x1 Alt x1");
                    docBuilder.SetFontSize(1, 2);
                    docBuilder.AppendLine("Test Larga x1 Alt x2");
                    docBuilder.SetFontSize(2, 2);
                    docBuilder.AppendLine("Test Larga x2 Alt x2");
                    docBuilder.ResetFontSize();
                    docBuilder.NewLine();
                    try
                    {
                        docBuilder.PrintQRCode($"TestQR_{testPrinter.Id}_{DateTime.UtcNow.Ticks}");
                        docBuilder.AppendLine("QR Code Test OK (sopra)");
                    }
                    catch (Exception qrEx)
                    {
                        _logger.LogError(qrEx, $"Failed to generate QR code for Test Print on printer ID: {testPrinter.Id}.");
                        docBuilder.AppendLine("QR Code Test Fallito.");
                    }
                    docBuilder.NewLine(3);
                    docBuilder.CutPaper();

                    printTasks.Add(SendToPrinterAsync(testPrinter, docBuilder.Build(), PrintJobType.TestPrint));
                    _logger.LogInformation($"Sending Test Print to printer '{testPrinter.Name}'.");
                }
                else
                {
                    _logger.LogWarning($"No enabled printer found in organization {order.OrganizationId} to send a Test Print to.");
                }

            }

            if (!printTasks.Any())
            {
                _logger.LogInformation($"No print tasks generated for Order ID: {order.Id}, JobType: {jobType}. This might be normal if no printers are configured/enabled or no items for comanda.");
                return (true, "No print tasks generated (e.g., no printers configured or no items for comanda)."); // Successfully processed, but nothing to print.
            }

            var results = await Task.WhenAll(printTasks);

            bool allSuccess = true;
            List<string> errors = new List<string>();

            foreach (var result in results)
            {
                if (!result.Success)
                {
                    allSuccess = false;
                    if (result.Error != null) errors.Add(result.Error);
                }
            }

            if (allSuccess)
            {
                _logger.LogInformation($"All print jobs for Order ID: {order.Id}, JobType: {jobType} completed successfully.");
                return (true, null);
            }
            else
            {
                string combinedErrors = string.Join("; ", errors);
                _logger.LogError($"One or more print jobs failed for Order ID: {order.Id}, JobType: {jobType}. Errors: {combinedErrors}");
                return (false, $"One or more print jobs failed: {combinedErrors}");
            }
        }

        // Private helper methods for ESC/POS generation and logic
        private byte[] GenerateEscPosComanda(
            Order order,
            IEnumerable<OrderItem> items,
            string comandaTitle,
            bool groupAndShowCategoriesWithinComanda = false)
        {
            var docBuilder = new EscPosDocumentBuilder();
            docBuilder.InitializePrinter();
            docBuilder.SetAlignment(EscPosAlignment.Center);
            docBuilder.SetEmphasis(true);
            docBuilder.SetFontSize(2, 2);
            docBuilder.AppendLine(comandaTitle);
            docBuilder.ResetFontSize();
            docBuilder.SetEmphasis(false);
            docBuilder.AppendLine($"Ordine: {order.DisplayOrderNumber ?? order.Id} - {order.OrderDateTime:HH:mm}"); // Use DisplayOrderNumber
            if (!string.IsNullOrEmpty(order.TableNumber)) docBuilder.AppendLine($"Tavolo: {order.TableNumber}");
            if (!string.IsNullOrEmpty(order.CustomerName)) docBuilder.AppendLine($"Cliente: {order.CustomerName}");
            if (order.IsTakeaway) docBuilder.AppendLine("**ASPORTO**");
            docBuilder.SetAlignment(EscPosAlignment.Left);
            docBuilder.AppendLine("--------------------------------");

            if (groupAndShowCategoriesWithinComanda)
            {
                var itemsByCategory = items
                    .Where(i => i.MenuItem != null) // Ensure MenuItem is not null
                    .GroupBy(i => i.MenuItem!.MenuCategoryId)
                    .Select(g => new
                    {
                        CategoryName = g.First().MenuItem?.MenuCategory?.Name ?? "Varie",
                        Items = g.ToList()
                    })
                    .OrderBy(g => g.CategoryName);

                foreach (var catGroup in itemsByCategory)
                {
                    docBuilder.SetEmphasis(true).SetFontSize(1, 1); // Smaller font for category header
                    docBuilder.AppendLine($"--- {catGroup.CategoryName.ToUpper()} ---");
                    docBuilder.ResetFontSize().SetEmphasis(false);
                    foreach (var item in catGroup.Items)
                    {
                        docBuilder.SetFontSize(1, 2); // Item font
                        docBuilder.AppendLine($"{item.Quantity} x {item.MenuItem?.Name ?? "N/A"}");
                        docBuilder.ResetFontSize();
                        if (!string.IsNullOrWhiteSpace(item.Note))
                        {
                            docBuilder.SetEmphasis(false); // Ensure note is not emphasized
                            // Potentially use a slightly smaller font for notes
                            docBuilder.AppendLine($"    >> {item.Note.Trim()}"); // Indent note
                        }
                    }
                }
            }
            else // Print items plain if not grouping by category
            {
                foreach (var item in items)
                {
                    docBuilder.SetFontSize(1, 2);
                    docBuilder.AppendLine($"{item.Quantity} x {item.MenuItem?.Name ?? "N/A"}");
                    docBuilder.ResetFontSize();
                    if (!string.IsNullOrWhiteSpace(item.Note))
                    {
                        docBuilder.SetEmphasis(false);
                        docBuilder.AppendLine($"    >> {item.Note.Trim()}"); // Indent note
                    }
                }
            }

            docBuilder.AppendLine("--------------------------------");
            docBuilder.NewLine(3);
            docBuilder.CutPaper();
            return docBuilder.Build();
        }

        private Dictionary<Printer, List<OrderItem>> GroupItemsByComandaPrinter(Order order, List<PrinterCategoryAssignment> allCategoryAssignments)
        {
            var itemsByPrinter = new Dictionary<Printer, List<OrderItem>>();

            // Ensure Printer and MenuCategory are loaded for assignments to avoid null issues later
            var validAssignments = allCategoryAssignments
                .Where(pca => pca.Printer != null && pca.Printer.IsEnabled &&
                              pca.Printer.OrganizationId == order.OrganizationId &&
                              pca.MenuCategory != null)
                .ToList();

            foreach (var item in order.OrderItems.Where(oi => oi.MenuItem?.MenuCategoryId != null))
            {
                var printersForItem = validAssignments
                    .Where(pca => pca.MenuCategoryId == item.MenuItem!.MenuCategoryId)
                    .Select(pca => pca.Printer)
                    .Distinct()
                    .ToList();

                if (!printersForItem.Any())
                {
                    _logger.LogWarning($"No enabled printer assigned for category '{item.MenuItem!.MenuCategory?.Name}' (ID: {item.MenuItem.MenuCategoryId}) for Order Item '{item.MenuItem.Name}'.");
                    continue;
                }

                foreach (var printer in printersForItem)
                {
                    if (printer == null) continue;

                    if (!itemsByPrinter.ContainsKey(printer))
                    {
                        itemsByPrinter[printer] = new List<OrderItem>();
                    }
                    itemsByPrinter[printer].Add(item);
                }
            }
            return itemsByPrinter;
        }

        public async Task<(bool Success, string? Error)> ReprintOrderDocumentsAsync(string orderId, ReprintRequestDto reprintRequest)
        {
            _logger.LogInformation($"Processing ReprintOrderDocumentsAsync for Order ID: {orderId}, ReprintJobType: {reprintRequest.ReprintJobType}, Specified Printer ID: {reprintRequest.PrinterId}");

            var order = await _context.Orders
                .Include(o => o.Organization)
                .Include(o => o.Area)
                    .ThenInclude(a => a!.ReceiptPrinter)
                .Include(o => o.CashierStation)
                    .ThenInclude(cs => cs!.ReceiptPrinter)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.MenuItem)
                        .ThenInclude(mi => mi!.MenuCategory)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null)
            {
                _logger.LogError($"Order with ID {orderId} not found when attempting to reprint.");
                return (false, $"Order with ID {orderId} not found.");
            }

            Printer? targetPrinter = null;

            if (reprintRequest.PrinterId.HasValue)
            {
                _logger.LogInformation($"Reprint request for Order ID: {orderId} specified Printer ID: {reprintRequest.PrinterId.Value}. Attempting to use this printer.");
                var specifiedPrinter = await _context.Printers
                    .FirstOrDefaultAsync(p => p.Id == reprintRequest.PrinterId.Value && p.OrganizationId == order.OrganizationId);

                if (specifiedPrinter != null)
                {
                    if (specifiedPrinter.IsEnabled)
                    {
                        targetPrinter = specifiedPrinter;
                        _logger.LogInformation($"Using specified printer for reprint (Order ID: {orderId}): {targetPrinter.Name} (ID: {targetPrinter.Id})");
                    }
                    else
                    {
                        _logger.LogWarning($"Specified printer ID {reprintRequest.PrinterId.Value} for Order ID {orderId} was found but is disabled. Falling back to default logic.");
                    }
                }
                else
                {
                    _logger.LogWarning($"Specified printer ID {reprintRequest.PrinterId.Value} for Order ID {orderId} not found or not in the same organization. Falling back to default logic.");
                }
            }

            if (targetPrinter == null) // If no specified printer or specified printer was invalid, use default logic
            {
                _logger.LogInformation($"No valid specified printer for Order ID: {orderId}. Using default printer logic (Cashier Station / Area).");
                if (order.CashierStationId.HasValue && order.CashierStation?.ReceiptPrinter != null && order.CashierStation.ReceiptPrinter.IsEnabled)
                {
                    targetPrinter = order.CashierStation.ReceiptPrinter;
                    _logger.LogInformation($"Target printer for reprint (Order ID: {orderId}) is Cashier Station '{order.CashierStation.Name}' printer: {targetPrinter.Name} (ID: {targetPrinter.Id})");
                }
                else if (order.Area?.ReceiptPrinter != null && order.Area.ReceiptPrinter.IsEnabled)
                {
                    targetPrinter = order.Area.ReceiptPrinter;
                    _logger.LogInformation($"Target printer for reprint (Order ID: {orderId}) is Area '{order.Area.Name}' default printer: {targetPrinter.Name} (ID: {targetPrinter.Id})");
                }
            }

            if (targetPrinter == null)
            {
                _logger.LogWarning($"No enabled target printer found for reprint (CashierStation or Area default) for Order ID: {orderId}.");
                return (false, "No enabled printer found at cashier station or area to handle the reprint.");
            }

            var printTasks = new List<Task<(bool Success, string? Error)>>();
            var docBuilder = new EscPosDocumentBuilder(); // Reusable builder

            // --- Receipt Reprinting --- (Always done if a printer is found)
            docBuilder.InitializePrinter(); // This already sets PC858_EURO in the builder
            // Explicitly re-select for absolute clarity in this service's logic.
            docBuilder.SelectCharacterCodeTable(CodePage.PC858_EURO);
            docBuilder.SetAlignment(EscPosAlignment.Center);
            docBuilder.SetEmphasis(true);
            docBuilder.SetFontSize(1, 2); // Slightly larger for "RISTAMPA"
            docBuilder.AppendLine("--- RISTAMPA SCONTRINO ---");
            docBuilder.ResetFontSize();
            docBuilder.SetEmphasis(false);
            docBuilder.AppendLine(order.Organization?.Name ?? "SagraFacile");
            docBuilder.AppendLine($"Area: {order.Area?.Name ?? "N/A"}");
            if (order.CashierStation != null)
            {
                docBuilder.AppendLine($"Cassa: {order.CashierStation.Name}");
            }
            docBuilder.AppendLine($"Ordine N. {order.DisplayOrderNumber ?? order.Id}"); // Use DisplayOrderNumber
            docBuilder.AppendLine(order.OrderDateTime.ToString("dd/MM/yyyy HH:mm:ss"));
            if (order.IsTakeaway)
            {
                docBuilder.SetEmphasis(true);
                docBuilder.SetFontSize(2, 2);
                docBuilder.AppendLine("ASPORTO");
                docBuilder.ResetFontSize();
                docBuilder.SetEmphasis(false);
            }
            if (order.NumberOfGuests > 0 && !order.IsTakeaway)
            {
                docBuilder.AppendLine($"Coperti: {order.NumberOfGuests}");
            }
            docBuilder.AppendLine($"Cliente: {order.CustomerName ?? "Anonimo"}");
            docBuilder.SetAlignment(EscPosAlignment.Left);
            docBuilder.AppendLine("--------------------------------");

            var itemsByCategory = order.OrderItems
                .GroupBy(oi => oi.MenuItem?.MenuCategoryId)
                .Select(g => new
                {
                    CategoryName = g.First().MenuItem?.MenuCategory?.Name ?? "Senza Categoria",
                    Items = g.ToList()
                })
                .OrderBy(g => g.CategoryName);

            foreach (var categoryGroup in itemsByCategory)
            {
                docBuilder.SetEmphasis(true);
                docBuilder.AppendLine(categoryGroup.CategoryName.ToUpper());
                docBuilder.SetEmphasis(false);
                foreach (var item in categoryGroup.Items)
                {
                    string itemName = item.MenuItem?.Name ?? "Articolo Sconosciuto";
                    string quantityPrice = $"{item.Quantity} x {item.UnitPrice:C}";
                    string totalPrice = (item.Quantity * item.UnitPrice).ToString("C");
                    docBuilder.AppendLine($"{itemName.PadRight(20).Substring(0, 20)} {quantityPrice.PadRight(10)} {totalPrice.PadLeft(8)}");
                    if (!string.IsNullOrWhiteSpace(item.Note))
                    {
                        docBuilder.SetAlignment(EscPosAlignment.Left); // Ensure left alignment
                        // Standard font size for notes on receipts, indented.
                        docBuilder.AppendLine($"    Nota: {item.Note.Trim()}");
                    }
                }
            }
            docBuilder.AppendLine("--------------------------------");

            // Calculate and print Subtotal for items
            decimal itemsSubtotalReprint = order.OrderItems.Sum(oi => oi.Quantity * oi.UnitPrice);
            docBuilder.SetAlignment(EscPosAlignment.Right);
            docBuilder.SetEmphasis(true);
            docBuilder.AppendLine($"SUBTOTALE: {itemsSubtotalReprint:C}");
            docBuilder.SetEmphasis(false);
            docBuilder.SetAlignment(EscPosAlignment.Left); // Reset alignment for charges if any

            // Add charges if applicable
            if (order.IsTakeaway && order.Area?.TakeawayCharge > 0)
            {
                docBuilder.SetAlignment(EscPosAlignment.Left);
                docBuilder.AppendLine($"Contributo Asporto: {order.Area.TakeawayCharge:C}");
            }
            else if (!order.IsTakeaway && order.Area?.GuestCharge > 0 && order.NumberOfGuests > 0)
            {
                docBuilder.SetAlignment(EscPosAlignment.Left);
                docBuilder.AppendLine($"Coperto ({order.NumberOfGuests} x {order.Area.GuestCharge:C}): {(order.NumberOfGuests * order.Area.GuestCharge):C}");
            }

            docBuilder.SetAlignment(EscPosAlignment.Right);
            docBuilder.SetEmphasis(true);
            docBuilder.SetFontSize(1, 2);
            docBuilder.AppendLine($"TOTALE: {order.TotalAmount:C}");
            docBuilder.ResetFontSize();
            docBuilder.SetEmphasis(false);

            docBuilder.SetAlignment(EscPosAlignment.Center);
            docBuilder.NewLine();
            try
            {
                // Use the same content for QR as in original printing to ensure consistency
                // Increased QR code module size to 12 for better scannability
                docBuilder.PrintQRCode($"SagraFacile_Order_{order.Id}", 8);
                docBuilder.NewLine();
            }
            catch (Exception qrEx)
            {
                _logger.LogError(qrEx, $"Failed to re-generate QR code for reprint of Order ID: {order.Id}.");
                docBuilder.AppendLine("QR Code non disponibile.");
            }

            docBuilder.AppendLine("Grazie e arrivederci!");
            docBuilder.NewLine(5); // Add more space before cutting
            docBuilder.CutPaper();
            printTasks.Add(SendToPrinterAsync(targetPrinter, docBuilder.Build(), PrintJobType.Receipt));
            _logger.LogInformation($"Added reprint task for RECEIPT for Order ID: {orderId} to printer {targetPrinter.Name}.");


            // --- Comanda Reprinting (to the same targetPrinter) ---
            if (reprintRequest.ReprintJobType == ReprintType.ReceiptAndComandas)
            {
                if (order.OrderItems.Any())
                {
                    _logger.LogInformation($"Generating consolidated comanda for reprint (Order ID: {orderId}) to printer {targetPrinter.Name}.");
                    string comandaTitle = $"RISTAMPA COMANDA - CASSA ({targetPrinter.Name})";
                    // Use the existing GenerateEscPosComanda helper. 
                    // The `groupAndShowCategoriesWithinComanda` is true by default in helper, which is fine.
                    byte[] escPosComanda = GenerateEscPosComanda(order, order.OrderItems, comandaTitle, groupAndShowCategoriesWithinComanda: true);

                    printTasks.Add(SendToPrinterAsync(targetPrinter, escPosComanda, PrintJobType.Comanda));
                    _logger.LogInformation($"Added reprint task for COMANDAS for Order ID: {orderId} to printer {targetPrinter.Name}.");
                }
                else
                {
                    _logger.LogInformation($"Reprint of comandas requested for Order ID: {orderId}, but order has no items. Skipping comanda reprint.");
                }
            }

            if (!printTasks.Any())
            {
                _logger.LogWarning($"No print tasks generated for reprint of Order ID: {orderId}. This is unexpected if a printer was found.");
                return (true, "No print tasks generated for reprint (e.g. error in document generation).");
            }

            var results = await Task.WhenAll(printTasks);
            bool allSuccess = results.All(r => r.Success);
            string? combinedErrors = allSuccess ? null : string.Join("; ", results.Where(r => !r.Success).Select(r => r.Error));

            if (allSuccess)
            {
                _logger.LogInformation($"All reprint jobs for Order ID: {orderId} completed successfully to printer {targetPrinter.Name}.");
                return (true, null);
            }
            else
            {
                _logger.LogError($"One or more reprint jobs failed for Order ID: {orderId} to printer {targetPrinter.Name}. Errors: {combinedErrors}");
                return (false, $"One or more reprint jobs failed: {combinedErrors}");
            }
        }

        // New method for Windows Companion App to get its configuration
        public async Task<(PrintMode PrintMode, string? WindowsPrinterName)?> GetPrinterConfigAsync(string instanceGuid)
        {
            if (string.IsNullOrWhiteSpace(instanceGuid))
            {
                _logger.LogWarning("GetPrinterConfigAsync called with empty instanceGuid.");
                return null;
            }

            // Assuming instanceGuid is the Printer.ConnectionString for WindowsUsb printers
            var printer = await _context.Printers
                .AsNoTracking() // Read-only operation
                .FirstOrDefaultAsync(p => p.Type == PrinterType.WindowsUsb && p.ConnectionString == instanceGuid);

            if (printer == null)
            {
                _logger.LogWarning($"No WindowsUsb printer found with ConnectionString (instanceGuid): {instanceGuid}");
                return null;
            }

            if (!printer.IsEnabled)
            {
                _logger.LogWarning($"WindowsUsb printer {printer.Name} (GUID: {instanceGuid}) is found but disabled. Configuration not returned.");
                return null; // Or perhaps return config but indicate disabled status if frontend can handle
            }

            _logger.LogInformation($"Returning configuration for printer {printer.Name} (GUID: {instanceGuid}): PrintMode={printer.PrintMode}, WindowsPrinterName={printer.WindowsPrinterName}");
            return (printer.PrintMode, printer.WindowsPrinterName);
        }
    }
}
