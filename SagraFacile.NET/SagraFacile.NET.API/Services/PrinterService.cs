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
            _logger.LogInformation("Fetching all printers.");
            var (userOrgId, isSuperAdmin) = GetUserContext();

            var query = _context.Printers.AsQueryable();

            if (!isSuperAdmin)
            {
                if (!userOrgId.HasValue)
                {
                    _logger.LogError("User organization context is missing for non-SuperAdmin during GetPrintersAsync.");
                    throw new InvalidOperationException("User organization context is missing for non-SuperAdmin.");
                }
                query = query.Where(p => p.OrganizationId == userOrgId.Value);
                _logger.LogInformation("Filtering printers by Organization ID: {OrganizationId}.", userOrgId.Value);
            }
            else
            {
                _logger.LogInformation("SuperAdmin fetching all printers across all organizations.");
            }

            var printers = await query.Select(p => MapPrinterToDto(p)).ToListAsync();
            _logger.LogInformation("Retrieved {PrinterCount} printers.", printers.Count);
            return printers;
        }

        public async Task<PrinterDto?> GetPrinterByIdAsync(int id)
        {
            _logger.LogInformation("Fetching printer by ID: {PrinterId}.", id);
            var (userOrgId, isSuperAdmin) = GetUserContext();
            var printer = await _context.Printers.FindAsync(id);

            if (printer == null)
            {
                _logger.LogWarning("Printer with ID {PrinterId} not found.", id);
                return null;
            }

            // Authorization check
            if (!isSuperAdmin && printer.OrganizationId != userOrgId)
            {
                _logger.LogWarning("User (OrgId: {UserOrgId}) not authorized to access printer ID {PrinterId} (OrgId: {PrinterOrgId}).", userOrgId, id, printer.OrganizationId);
                return null; // Or throw UnauthorizedAccessException
            }

            _logger.LogInformation("Retrieved printer {PrinterId}.", id);
            return MapPrinterToDto(printer);
        }

        public async Task<(Printer? Printer, string? Error)> CreatePrinterAsync(PrinterUpsertDto printerDto)
        {
            _logger.LogInformation("Attempting to create printer: {PrinterName}, Type: {PrinterType}.", printerDto.Name, printerDto.Type);
            var (userOrgId, isSuperAdmin) = GetUserContext();

            // Validation 1: Correct OrganizationId assignment/validation
            if (!isSuperAdmin)
            {
                if (!userOrgId.HasValue)
                {
                    _logger.LogError("User organization context is missing for non-SuperAdmin during CreatePrinterAsync.");
                    return (null, "User organization context is missing.");
                }
                if (printerDto.OrganizationId != 0 && printerDto.OrganizationId != userOrgId.Value)
                {
                    _logger.LogWarning("User (OrgId: {UserOrgId}) attempted to create printer for different organization ({RequestedOrgId}).", userOrgId.Value, printerDto.OrganizationId);
                    return (null, "User is not authorized to create a printer for a different organization.");
                }
                printerDto.OrganizationId = userOrgId.Value; // Assign user's org ID
                _logger.LogDebug("Assigned printer to user's organization ID: {OrganizationId}.", userOrgId.Value);
            }
            else // SuperAdmin must provide a valid OrgId
            {
                if (printerDto.OrganizationId == 0 || !await _context.Organizations.AnyAsync(o => o.Id == printerDto.OrganizationId))
                {
                    _logger.LogWarning("SuperAdmin attempted to create printer without specifying OrganizationId.");
                    return (null, "SuperAdmin must specify an OrganizationId.");
                }
                if (!await _context.Organizations.AnyAsync(o => o.Id == printerDto.OrganizationId))
                {
                    _logger.LogWarning("SuperAdmin attempted to create printer for non-existent OrganizationId: {OrganizationId}.", printerDto.OrganizationId);
                    return (null, $"Invalid or non-existent OrganizationId: {printerDto.OrganizationId}");
                }
                _logger.LogDebug("SuperAdmin creating printer for specified Organization ID: {OrganizationId}.", printerDto.OrganizationId);
            }

            var printer = new Printer
            {
                Name = printerDto.Name,
                Type = printerDto.Type,
                ConnectionString = printerDto.ConnectionString,
                IsEnabled = printerDto.IsEnabled,
                OrganizationId = printerDto.OrganizationId,
                PrintMode = printerDto.PrintMode
            };

            _context.Printers.Add(printer);
            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Printer '{PrinterName}' (ID: {PrinterId}) created successfully for Organization {OrganizationId}.", printer.Name, printer.Id, printer.OrganizationId);
                return (printer, null);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Error creating printer '{PrinterName}'.", printerDto.Name);
                return (null, "An error occurred while saving the printer.");
            }
        }

        public async Task<(bool Success, string? Error)> UpdatePrinterAsync(int id, PrinterUpsertDto printerDto)
        {
            _logger.LogInformation("Attempting to update printer ID: {PrinterId}.", id);
            var (userOrgId, isSuperAdmin) = GetUserContext();

            var existingPrinter = await _context.Printers.FindAsync(id);
            if (existingPrinter == null)
            {
                _logger.LogWarning("Update printer failed: Printer with ID {PrinterId} not found.", id);
                return (false, "Printer not found.");
            }

            // Authorization check 1: Ownership
            if (!isSuperAdmin && existingPrinter.OrganizationId != userOrgId)
            {
                _logger.LogWarning("User (OrgId: {UserOrgId}) not authorized to update printer ID {PrinterId} (OrgId: {PrinterOrgId}).", userOrgId, id, existingPrinter.OrganizationId);
                return (false, "User is not authorized to update this printer.");
            }

            // Authorization check 2: Changing OrganizationId (only SuperAdmin)
            if (!isSuperAdmin && existingPrinter.OrganizationId != printerDto.OrganizationId)
            {
                _logger.LogWarning("User (OrgId: {UserOrgId}) attempted to change printer {PrinterId} to different organization ({RequestedOrgId}).", userOrgId, id, printerDto.OrganizationId);
                return (false, "User is not authorized to change the printer's organization.");
            }

            // Validation 1: Target OrganizationId validity (if changed by SuperAdmin)
            if (isSuperAdmin && existingPrinter.OrganizationId != printerDto.OrganizationId)
            {
                if (!await _context.Organizations.AnyAsync(o => o.Id == printerDto.OrganizationId))
                {
                    _logger.LogWarning("SuperAdmin attempted to update printer {PrinterId} to non-existent OrganizationId: {OrganizationId}.", id, printerDto.OrganizationId);
                    return (false, $"Target organization with ID {printerDto.OrganizationId} not found.");
                }
                existingPrinter.OrganizationId = printerDto.OrganizationId;
                _logger.LogInformation("SuperAdmin changed printer {PrinterId} organization to {OrganizationId}.", id, printerDto.OrganizationId);
            }

            // Update properties
            existingPrinter.Name = printerDto.Name;
            existingPrinter.Type = printerDto.Type;
            existingPrinter.ConnectionString = printerDto.ConnectionString;
            existingPrinter.IsEnabled = printerDto.IsEnabled;
            existingPrinter.PrintMode = printerDto.PrintMode;

            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Printer {PrinterId} updated successfully.", id);
                return (true, null);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(ex, "Concurrency exception during printer update for ID {PrinterId}.", id);
                if (!await PrinterExistsAsync(id))
                {
                    return (false, "Printer not found (concurrency issue).");
                }
                else
                {
                    return (false, "Failed to update printer due to a concurrency conflict.");
                }
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Error updating printer with ID {PrinterId}.", id);
                return (false, "An error occurred while saving the printer update.");
            }
        }

        public async Task<(bool Success, string? Error)> DeletePrinterAsync(int id)
        {
            _logger.LogInformation("Attempting to delete printer ID: {PrinterId}.", id);
            var (userOrgId, isSuperAdmin) = GetUserContext();

            var printer = await _context.Printers.FindAsync(id);
            if (printer == null)
            {
                _logger.LogWarning("Delete printer failed: Printer with ID {PrinterId} not found.", id);
                return (false, "Printer not found.");
            }

            // Authorization check: Ownership
            if (!isSuperAdmin && printer.OrganizationId != userOrgId)
            {
                _logger.LogWarning("User (OrgId: {UserOrgId}) not authorized to delete printer ID {PrinterId} (OrgId: {PrinterOrgId}).", userOrgId, id, printer.OrganizationId);
                return (false, "User is not authorized to delete this printer.");
            }

            // Check for dependencies (e.g., if used as Area Receipt Printer)
            bool isInUseAsReceipt = await _context.Areas.AnyAsync(a => a.ReceiptPrinterId == id);
            if (isInUseAsReceipt)
            {
                _logger.LogWarning("Delete printer failed for ID {PrinterId}: It is assigned as a Receipt Printer for one or more Areas.", id);
                return (false, "Cannot delete printer because it is assigned as a Receipt Printer for one or more Areas.");
            }
            // Future: Check if used in PrinterCategoryAssignments if needed for strict delete

            _context.Printers.Remove(printer);
            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Printer {PrinterId} deleted successfully.", id);
                return (true, null);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Error deleting printer with ID {PrinterId}.", id);
                return (false, "An error occurred while deleting the printer.");
            }
        }

        public async Task<bool> PrinterExistsAsync(int id)
        {
            _logger.LogDebug("Checking if printer ID {PrinterId} exists.", id);
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
                IsEnabled = printer.IsEnabled,
                OrganizationId = printer.OrganizationId,
                PrintMode = printer.PrintMode
            };
        }

        // Implementation for IPrinterService
        public async Task<(bool Success, string? Error)> SendToPrinterAsync(Printer printer, byte[] data, PrintJobType jobType) // Added jobType for context
        {
            if (!printer.IsEnabled)
            {
                _logger.LogWarning("Attempted to send job type {JobType} to disabled printer {PrinterName} (ID: {PrinterId}).", jobType, printer.Name, printer.Id);
                return (false, "Printer is disabled.");
            }

            _logger.LogInformation("Sending job type {JobType} to printer {PrinterName} (ID: {PrinterId}), Type: {PrinterType}", jobType, printer.Name, printer.Type);

            try
            {
                if (printer.Type == PrinterType.Network)
                {
                    if (string.IsNullOrWhiteSpace(printer.ConnectionString))
                    {
                        _logger.LogError("Network printer {PrinterName} (ID: {PrinterId}) has no connection string.", printer.Name, printer.Id);
                        return (false, "Network printer connection string is missing.");
                    }

                    var parts = printer.ConnectionString.Split(':');
                    if (parts.Length != 2 || !int.TryParse(parts[1], out int port))
                    {
                        _logger.LogError("Invalid network printer connection string format for {PrinterName} (ID: {PrinterId}): {ConnectionString}", printer.Name, printer.Id, printer.ConnectionString);
                        return (false, "Invalid network printer connection string format.");
                    }
                    string ipAddress = parts[0];

                    using (var client = new TcpClient())
                    {
                        client.SendTimeout = 10000;
                        client.ReceiveTimeout = 10000;

                        var connectTask = client.ConnectAsync(ipAddress, port);
                        if (await Task.WhenAny(connectTask, Task.Delay(5000)) == connectTask && client.Connected)
                        {
                            using (var stream = client.GetStream())
                            {
                                await stream.WriteAsync(data, 0, data.Length);
                                await stream.FlushAsync();
                                _logger.LogInformation("Successfully sent data to network printer {PrinterName} at {IpAddress}:{Port}", printer.Name, ipAddress, port);
                                return (true, null);
                            }
                        }
                        else
                        {
                            _logger.LogError("Failed to connect to network printer {PrinterName} at {IpAddress}:{Port} within timeout or connection failed.", printer.Name, ipAddress, port);
                            return (false, "Failed to connect to network printer.");
                        }
                    }
                }
                else if (printer.Type == PrinterType.WindowsUsb)
                {
                    if (string.IsNullOrWhiteSpace(printer.ConnectionString))
                    {
                        _logger.LogError("WindowsUSB printer {PrinterName} (ID: {PrinterId}) has no ConnectionString (GUID).", printer.Name, printer.Id);
                        return (false, "WindowsUSB printer ConnectionString (GUID) is missing.");
                    }
                    

                    var connectionId = OrderHub.GetConnectionIdForPrinter(printer.ConnectionString);
                    if (connectionId != null)
                    {
                        string jobId = Guid.NewGuid().ToString(); // Generate a unique Job ID
                        _logger.LogInformation("Dispatching print job (JobID: {JobId}) to WindowsUSB printer {PrinterName} (GUID: {PrinterConnectionString}) via SignalR ConnectionId: {ConnectionId}", jobId, printer.Name, printer.ConnectionString, connectionId);
                        await _orderHubContext.Clients.Client(connectionId).SendAsync(
                            "PrintJob",
                            jobId,
                            data);
                        return (true, null);
                    }
                    else
                    {
                        _logger.LogWarning("WindowsUSB printer {PrinterName} (GUID: {PrinterConnectionString}) is not currently connected or registered with the OrderHub.", printer.Name, printer.ConnectionString);
                        return (false, "WindowsUSB printer is not connected.");
                    }
                }
                else
                {
                    _logger.LogError("Unsupported printer type: {PrinterType} for printer {PrinterName} (ID: {PrinterId})", printer.Type, printer.Name, printer.Id);
                    return (false, "Unsupported printer type.");
                }
            }
            catch (SocketException ex)
            {
                _logger.LogError(ex, "SocketException while sending to printer {PrinterName} (ID: {PrinterId}): {ErrorMessage}", printer.Name, printer.Id, ex.Message);
                return (false, $"Network error while printing: {ex.Message}");
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while sending to printer {PrinterName} (ID: {PrinterId}): {ErrorMessage}", printer.Name, printer.Id, ex.Message);
                return (false, $"An unexpected error occurred during printing: {ex.Message}");
            }
        }

        public async Task<(bool Success, string? Error)> PrintOrderDocumentsAsync(Order order, PrintJobType jobType)
        {
            _logger.LogInformation("Processing PrintOrderDocumentsAsync for Order ID: {OrderId}, JobType: {JobType}", order.Id, jobType);

            var orderWithDetails = await _context.Orders
                .Include(o => o.Organization)
                .Include(o => o.Area)
                    .ThenInclude(a => a!.ReceiptPrinter)
                .Include(o => o.CashierStation)
                    .ThenInclude(cs => cs!.ReceiptPrinter)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.MenuItem)
                        .ThenInclude(mi => mi!.MenuCategory)
                .FirstOrDefaultAsync(o => o.Id == order.Id);

            if (orderWithDetails == null)
            {
                _logger.LogError("Order with ID {OrderId} not found when attempting to print.", order.Id);
                return (false, $"Order with ID {order.Id} not found.");
            }

            order = orderWithDetails;

            var printTasks = new List<Task<(bool Success, string? Error)>>();

            if (jobType == PrintJobType.Receipt)
            {
                _logger.LogInformation("Determining printer for RECEIPT for Order ID: {OrderId}.", order.Id);
                Printer? receiptPrinter = null;

                if (order.CashierStationId.HasValue && order.CashierStation != null && order.CashierStation.ReceiptPrinterId != 0)
                {
                    receiptPrinter = order.CashierStation.ReceiptPrinter;
                    if (receiptPrinter != null)
                    {
                        _logger.LogInformation("Using Cashier Station '{CashierStationName}' (ID: {CashierStationId}) assigned receipt printer: {PrinterName} (ID: {PrinterId})", order.CashierStation.Name, order.CashierStation.Id, receiptPrinter.Name, receiptPrinter.Id);
                    }
                    else
                    {
                        _logger.LogWarning("Cashier Station '{CashierStationName}' has ReceiptPrinterId {ReceiptPrinterId} but navigation property is null.", order.CashierStation.Name, order.CashierStation.ReceiptPrinterId);
                    }
                }
                if (receiptPrinter == null && order.Area != null && order.Area.ReceiptPrinterId != 0)
                {
                    receiptPrinter = order.Area.ReceiptPrinter;
                    if (receiptPrinter != null)
                    {
                        _logger.LogInformation("Using Area '{AreaName}' (ID: {AreaId}) default receipt printer: {PrinterName} (ID: {PrinterId})", order.Area.Name, order.Area.Id, receiptPrinter.Name, receiptPrinter.Id);
                    }
                    else
                    {
                        _logger.LogWarning("Area '{AreaName}' has ReceiptPrinterId {ReceiptPrinterId} but navigation property is null.", order.Area.Name, order.Area.ReceiptPrinterId);
                    }
                }

                if (receiptPrinter != null)
                {
                    if (receiptPrinter.IsEnabled)
                    {
                        var docBuilder = new EscPosDocumentBuilder();
                        docBuilder.InitializePrinter();
                        docBuilder.SetAlignment(EscPosAlignment.Center);
                        docBuilder.SetEmphasis(true);
                        docBuilder.AppendLine(order.Organization?.Name ?? "Sagrafacile");
                        docBuilder.SetEmphasis(false);
                        docBuilder.AppendLine($"Area: {order.Area?.Name ?? "N/A"}");
                        if (order.CashierStation != null)
                        {
                            docBuilder.AppendLine($"Cassa: {order.CashierStation.Name}");
                        }
                        docBuilder.AppendLine($"Ordine N. {order.DisplayOrderNumber ?? order.Id}");
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
                                    docBuilder.SetAlignment(EscPosAlignment.Left);
                                    docBuilder.AppendLine($"    Nota: {item.Note.Trim()}");
                                }
                            }
                        }
                        docBuilder.AppendLine("--------------------------------");

                        decimal itemsSubtotal = order.OrderItems.Sum(oi => oi.Quantity * oi.UnitPrice);
                        docBuilder.SetAlignment(EscPosAlignment.Right);
                        docBuilder.SetEmphasis(true);
                        docBuilder.AppendLine($"SUBTOTALE: {itemsSubtotal:C}");
                        docBuilder.SetEmphasis(false);
                        docBuilder.SetAlignment(EscPosAlignment.Left);
                        docBuilder.AppendLine("--------------------------------");


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
                            docBuilder.PrintQRCode($"Sagrafacile_Order_{order.Id}", 8);
                            docBuilder.NewLine();
                        }
                        catch (Exception qrEx)
                        {
                            _logger.LogError(qrEx, "Failed to generate QR code for Order ID: {OrderId}. QR generation in EscPosDocumentBuilder might need adjustment for your printer.", order.Id);
                            docBuilder.AppendLine("QR Code non disponibile.");
                        }

                        docBuilder.AppendLine("Grazie e arrivederci!");
                        docBuilder.NewLine(5);
                        docBuilder.CutPaper();

                        printTasks.Add(SendToPrinterAsync(receiptPrinter, docBuilder.Build(), PrintJobType.Receipt));

                    }
                    else
                    {
                        _logger.LogWarning("Receipt printer '{PrinterName}' (ID: {PrinterId}) is disabled for Order ID: {OrderId}.", receiptPrinter.Name, receiptPrinter.Id, order.Id);
                    }
                }
                else
                {
                    _logger.LogWarning("No receipt printer configured for Order ID: {OrderId} (CashierStation: {CashierStationId}, Area: {AreaId}).", order.Id, order.CashierStationId, order.AreaId);
                }
            }
            else if (jobType == PrintJobType.Comanda)
            {
                _logger.LogInformation("Determining printer(s) for COMANDA for Order ID: {OrderId}.", order.Id);
                var comandasToSend = new Dictionary<Printer, List<byte>>();

                if (order.CashierStationId.HasValue && order.CashierStation?.PrintComandasAtThisStation == true && order.CashierStation.ReceiptPrinterId != 0)
                {
                    var stationPrinter = order.CashierStation.ReceiptPrinter;
                    if (stationPrinter != null && stationPrinter.IsEnabled)
                    {
                        _logger.LogInformation("Printing ALL comandas for Order ID: {OrderId} at Cashier Station '{CashierStationName}' printer: {PrinterName}", order.Id, order.CashierStation.Name, stationPrinter.Name);
                        string comandaTitle = $"COMANDA - Stazione: {order.CashierStation.Name}";
                        byte[] escPosComanda = GenerateEscPosComanda(order, order.OrderItems, comandaTitle, groupAndShowCategoriesWithinComanda: true);
                        if (!comandasToSend.ContainsKey(stationPrinter)) comandasToSend[stationPrinter] = new List<byte>();
                        comandasToSend[stationPrinter].AddRange(escPosComanda);
                    }
                    else if (stationPrinter != null && !stationPrinter.IsEnabled)
                    {
                        _logger.LogWarning("Comanda printer (station specific) '{PrinterName}' for Order {OrderId} is disabled.", stationPrinter.Name, order.Id);
                    }
                }
                else if (order.Area?.PrintComandasAtCashier == true && order.Area.ReceiptPrinterId != 0)
                {
                    var areaDefaultPrinter = order.Area.ReceiptPrinter;
                    if (areaDefaultPrinter != null && areaDefaultPrinter.IsEnabled)
                    {
                        _logger.LogInformation("Printing ALL comandas for Order ID: {OrderId} at Area '{AreaName}' default printer: {PrinterName}", order.Id, order.Area.Name, areaDefaultPrinter.Name);
                        string comandaTitle = $"COMANDA - Area: {order.Area.Name}";
                        byte[] escPosComanda = GenerateEscPosComanda(order, order.OrderItems, comandaTitle, groupAndShowCategoriesWithinComanda: true);
                        if (!comandasToSend.ContainsKey(areaDefaultPrinter)) comandasToSend[areaDefaultPrinter] = new List<byte>();
                        comandasToSend[areaDefaultPrinter].AddRange(escPosComanda);
                    }
                    else if (areaDefaultPrinter != null && !areaDefaultPrinter.IsEnabled)
                    {
                        _logger.LogWarning("Comanda printer (area default) '{PrinterName}' for Order {OrderId} is disabled.", areaDefaultPrinter.Name, order.Id);
                    }
                }
                else
                {
                    _logger.LogInformation("Printing comandas based on category assignments for Order ID: {OrderId}.", order.Id);
                    var allCategoryAssignments = await _context.PrinterCategoryAssignments
                        .Include(pca => pca.Printer)
                        .Include(pca => pca.MenuCategory)
                        .Where(pca => pca.Printer.OrganizationId == order.OrganizationId && pca.Printer.IsEnabled)
                        .ToListAsync();

                    Dictionary<Printer, List<OrderItem>> comandaItemGroups = GroupItemsByComandaPrinter(order, allCategoryAssignments);

                    foreach (var kvp in comandaItemGroups)
                    {
                        Printer printer = kvp.Key;
                        List<OrderItem> itemsForPrinter = kvp.Value;

                        if (itemsForPrinter.Any())
                        {
                            _logger.LogInformation("Generating comanda for printer '{PrinterName}' (ID: {PrinterId}) with {ItemCount} items for Order {OrderId}.", printer.Name, printer.Id, itemsForPrinter.Count, order.Id);
                            string comandaTitle = $"COMANDA - {printer.Name}";
                            byte[] escPosComanda = GenerateEscPosComanda(order, itemsForPrinter, comandaTitle, groupAndShowCategoriesWithinComanda: true);
                            if (!comandasToSend.ContainsKey(printer)) comandasToSend[printer] = new List<byte>();
                            comandasToSend[printer].AddRange(escPosComanda);
                        }
                    }
                }

                foreach (var entry in comandasToSend)
                {
                    if (entry.Value.Count > 0)
                    {
                        printTasks.Add(SendToPrinterAsync(entry.Key, entry.Value.ToArray(), PrintJobType.Comanda));
                    }
                }
                if (!comandasToSend.Any() && order.OrderItems.Any())
                {
                    _logger.LogWarning("No comanda printers were identified or all were disabled for Order ID: {OrderId}, but order has items.", order.Id);
                }
            }

            if (!printTasks.Any())
            {
                _logger.LogInformation("No print tasks generated for Order ID: {OrderId}, JobType: {JobType}. This might be normal if no printers are configured/enabled or no items for comanda.", order.Id, jobType);
                return (true, "No print tasks generated (e.g., no printers configured or no items for comanda).");
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
                _logger.LogInformation("All print jobs for Order ID: {OrderId}, JobType: {JobType} completed successfully.", order.Id, jobType);
                return (true, null);
            }
            else
            {
                string combinedErrors = string.Join("; ", errors);
                _logger.LogError("One or more print jobs failed for Order ID: {OrderId}, JobType: {JobType}. Errors: {Errors}", order.Id, jobType, combinedErrors);
                return (false, $"One or more print jobs failed: {combinedErrors}");
            }
        }

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
            docBuilder.AppendLine($"Ordine: {order.DisplayOrderNumber ?? order.Id} - {order.OrderDateTime:HH:mm}");
            if (!string.IsNullOrEmpty(order.TableNumber)) docBuilder.AppendLine($"Tavolo: {order.TableNumber}");
            if (!string.IsNullOrEmpty(order.CustomerName)) docBuilder.AppendLine($"Cliente: {order.CustomerName}");
            if (order.IsTakeaway) docBuilder.AppendLine("**ASPORTO**");
            docBuilder.SetAlignment(EscPosAlignment.Left);
            docBuilder.AppendLine("--------------------------------");

            if (groupAndShowCategoriesWithinComanda)
            {
                var itemsByCategory = items
                    .Where(i => i.MenuItem != null)
                    .GroupBy(i => i.MenuItem!.MenuCategoryId)
                    .Select(g => new
                    {
                        CategoryName = g.First().MenuItem?.MenuCategory?.Name ?? "Varie",
                        Items = g.ToList()
                    })
                    .OrderBy(g => g.CategoryName);

                foreach (var catGroup in itemsByCategory)
                {
                    docBuilder.SetEmphasis(true).SetFontSize(1, 1);
                    docBuilder.AppendLine($"--- {catGroup.CategoryName.ToUpper()} ---");
                    docBuilder.ResetFontSize().SetEmphasis(false);
                    foreach (var item in catGroup.Items)
                    {
                        docBuilder.SetFontSize(1, 2);
                        docBuilder.AppendLine($"{item.Quantity} x {item.MenuItem?.Name ?? "N/A"}");
                        docBuilder.ResetFontSize();
                        if (!string.IsNullOrWhiteSpace(item.Note))
                        {
                            docBuilder.SetEmphasis(false);
                            docBuilder.AppendLine($"    >> {item.Note.Trim()}");
                        }
                    }
                }
            }
            else
            {
                foreach (var item in items)
                {
                    docBuilder.SetFontSize(1, 2);
                    docBuilder.AppendLine($"{item.Quantity} x {item.MenuItem?.Name ?? "N/A"}");
                    docBuilder.ResetFontSize();
                    if (!string.IsNullOrWhiteSpace(item.Note))
                    {
                        docBuilder.SetEmphasis(false);
                        docBuilder.AppendLine($"    >> {item.Note.Trim()}");
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
                    _logger.LogWarning("No enabled printer assigned for category '{CategoryName}' (ID: {CategoryId}) for Order Item '{MenuItemName}' (Order ID: {OrderId}).", item.MenuItem!.MenuCategory?.Name, item.MenuItem.MenuCategoryId, item.MenuItem.Name, order.Id);
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
            _logger.LogInformation("Processing ReprintOrderDocumentsAsync for Order ID: {OrderId}, ReprintJobType: {ReprintJobType}, Specified Printer ID: {PrinterId}", orderId, reprintRequest.ReprintJobType, reprintRequest.PrinterId);

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
                _logger.LogError("Order with ID {OrderId} not found when attempting to reprint.", orderId);
                return (false, $"Order with ID {orderId} not found.");
            }

            Printer? targetPrinter = null;

            if (reprintRequest.PrinterId.HasValue)
            {
                _logger.LogInformation("Reprint request for Order ID: {OrderId} specified Printer ID: {SpecifiedPrinterId}. Attempting to use this printer.", orderId, reprintRequest.PrinterId.Value);
                var specifiedPrinter = await _context.Printers
                    .FirstOrDefaultAsync(p => p.Id == reprintRequest.PrinterId.Value && p.OrganizationId == order.OrganizationId);

                if (specifiedPrinter != null)
                {
                    if (specifiedPrinter.IsEnabled)
                    {
                        targetPrinter = specifiedPrinter;
                        _logger.LogInformation("Using specified printer for reprint (Order ID: {OrderId}): {PrinterName} (ID: {PrinterId})", orderId, targetPrinter.Name, targetPrinter.Id);
                    }
                    else
                    {
                        _logger.LogWarning("Specified printer ID {SpecifiedPrinterId} for Order ID {OrderId} was found but is disabled. Falling back to default logic.", reprintRequest.PrinterId.Value, order.Id);
                    }
                }
                else
                {
                    _logger.LogWarning("Specified printer ID {SpecifiedPrinterId} for Order ID {OrderId} not found or not in the same organization. Falling back to default logic.", reprintRequest.PrinterId.Value, order.Id);
                }
            }

            if (targetPrinter == null)
            {
                _logger.LogInformation("No valid specified printer for Order ID: {OrderId}. Using default printer logic (Cashier Station / Area).", order.Id);
                if (order.CashierStationId.HasValue && order.CashierStation?.ReceiptPrinter != null && order.CashierStation.ReceiptPrinter.IsEnabled)
                {
                    targetPrinter = order.CashierStation.ReceiptPrinter;
                    _logger.LogInformation("Target printer for reprint (Order ID: {OrderId}) is Cashier Station '{CashierStationName}' printer: {PrinterName} (ID: {PrinterId})", order.Id, order.CashierStation.Name, targetPrinter.Name, targetPrinter.Id);
                }
                else if (order.Area?.ReceiptPrinter != null && order.Area.ReceiptPrinter.IsEnabled)
                {
                    targetPrinter = order.Area.ReceiptPrinter;
                    _logger.LogInformation("Target printer for reprint (Order ID: {OrderId}) is Area '{AreaName}' default printer: {PrinterName} (ID: {PrinterId})", order.Id, order.Area.Name, targetPrinter.Name, targetPrinter.Id);
                }
            }

            if (targetPrinter == null)
            {
                _logger.LogWarning("No enabled target printer found for reprint (CashierStation or Area default) for Order ID: {OrderId}.", order.Id);
                return (false, "No enabled printer found at cashier station or area to handle the reprint.");
            }

            var printTasks = new List<Task<(bool Success, string? Error)>>();
            var docBuilder = new EscPosDocumentBuilder();

            docBuilder.InitializePrinter();
            docBuilder.SetAlignment(EscPosAlignment.Center);
            docBuilder.SetEmphasis(true);
            docBuilder.SetFontSize(1, 2);
            docBuilder.AppendLine("--- RISTAMPA SCONTRINO ---");
            docBuilder.ResetFontSize();
            docBuilder.SetEmphasis(false);
            docBuilder.AppendLine(order.Organization?.Name ?? "SagraFacile");
            docBuilder.AppendLine($"Area: {order.Area?.Name ?? "N/A"}");
            if (order.CashierStation != null)
            {
                docBuilder.AppendLine($"Cassa: {order.CashierStation.Name}");
            }
            docBuilder.AppendLine($"Ordine N. {order.DisplayOrderNumber ?? order.Id}");
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
                        docBuilder.SetAlignment(EscPosAlignment.Left);
                        docBuilder.AppendLine($"    Nota: {item.Note.Trim()}");
                    }
                }
            }
            docBuilder.AppendLine("--------------------------------");

            decimal itemsSubtotalReprint = order.OrderItems.Sum(oi => oi.Quantity * oi.UnitPrice);
            docBuilder.SetAlignment(EscPosAlignment.Right);
            docBuilder.SetEmphasis(true);
            docBuilder.AppendLine($"SUBTOTALE: {itemsSubtotalReprint:C}");
            docBuilder.SetEmphasis(false);
            docBuilder.SetAlignment(EscPosAlignment.Left);

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
                docBuilder.PrintQRCode($"SagraFacile_Order_{order.Id}", 8);
                docBuilder.NewLine();
            }
            catch (Exception qrEx)
            {
                _logger.LogError(qrEx, "Failed to re-generate QR code for reprint of Order ID: {OrderId}.", order.Id);
                docBuilder.AppendLine("QR Code non disponibile.");
            }

            docBuilder.AppendLine("Grazie e arrivederci!");
            docBuilder.NewLine(5);
            docBuilder.CutPaper();
            printTasks.Add(SendToPrinterAsync(targetPrinter, docBuilder.Build(), PrintJobType.Receipt));
            _logger.LogInformation("Added reprint task for RECEIPT for Order ID: {OrderId} to printer {PrinterName}.", orderId, targetPrinter.Name);


            if (reprintRequest.ReprintJobType == ReprintType.ReceiptAndComandas)
            {
                if (order.OrderItems.Any())
                {
                    _logger.LogInformation("Generating consolidated comanda for reprint (Order ID: {OrderId}) to printer {PrinterName}.", orderId, targetPrinter.Name);
                    string comandaTitle = $"RISTAMPA COMANDA - CASSA ({targetPrinter.Name})";
                    byte[] escPosComanda = GenerateEscPosComanda(order, order.OrderItems, comandaTitle, groupAndShowCategoriesWithinComanda: true);

                    printTasks.Add(SendToPrinterAsync(targetPrinter, escPosComanda, PrintJobType.Comanda));
                    _logger.LogInformation("Added reprint task for COMANDAS for Order ID: {OrderId} to printer {PrinterName}.", orderId, targetPrinter.Name);
                }
                else
                {
                    _logger.LogInformation("Reprint of comandas requested for Order ID: {OrderId}, but order has no items. Skipping comanda reprint.", orderId);
                }
            }

            if (!printTasks.Any())
            {
                _logger.LogWarning("No print tasks generated for reprint of Order ID: {OrderId}. This is unexpected if a printer was found.", orderId);
                return (true, "No print tasks generated for reprint (e.g. error in document generation).");
            }

            var results = await Task.WhenAll(printTasks);
            bool allSuccess = results.All(r => r.Success);
            string? combinedErrors = allSuccess ? null : string.Join("; ", results.Where(r => !r.Success).Select(r => r.Error));

            if (allSuccess)
            {
                _logger.LogInformation("All reprint jobs for Order ID: {OrderId} completed successfully to printer {PrinterName}.", orderId, targetPrinter.Name);
                return (true, null);
            }
            else
            {
                _logger.LogError("One or more reprint jobs failed for Order ID: {OrderId} to printer {PrinterName}. Errors: {Errors}", orderId, targetPrinter.Name, combinedErrors);
                return (false, $"One or more reprint jobs failed: {combinedErrors}");
            }
        }

        public async Task<PrintMode?> GetPrinterConfigAsync(string instanceGuid)
        {
            _logger.LogInformation("Fetching printer config for instance GUID: {InstanceGuid}.", instanceGuid);
            if (string.IsNullOrWhiteSpace(instanceGuid))
            {
                _logger.LogWarning("GetPrinterConfigAsync called with empty instanceGuid.");
                return null;
            }

            var printer = await _context.Printers
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Type == PrinterType.WindowsUsb && p.ConnectionString == instanceGuid);

            if (printer == null)
            {
                _logger.LogWarning("No WindowsUsb printer found with ConnectionString (instanceGuid): {InstanceGuid}", instanceGuid);
                return null;
            }

            if (!printer.IsEnabled)
            {
                _logger.LogWarning("WindowsUsb printer {PrinterName} (GUID: {InstanceGuid}) is found but disabled. Configuration not returned.", printer.Name, instanceGuid);
                return null;
            }

            _logger.LogInformation("Returning configuration for printer {PrinterName} (GUID: {InstanceGuid}): PrintMode={PrintMode}", printer.Name, instanceGuid, printer.PrintMode);
            return printer.PrintMode;
        }

        public async Task<(bool Success, string? Error)> SendTestPrintAsync(int printerId)
        {
            var (userOrgId, isSuperAdmin) = GetUserContext();
            _logger.LogInformation("Attempting to send test print for printer ID: {PrinterId} by user with OrgId: {UserOrgId}, IsSuperAdmin: {IsSuperAdmin}", printerId, userOrgId?.ToString() ?? "N/A", isSuperAdmin);

            var printer = await _context.Printers.FindAsync(printerId);

            if (printer == null)
            {
                _logger.LogWarning("Test print failed: Printer with ID {PrinterId} not found.", printerId);
                return (false, "Printer not found.");
            }

            if (!isSuperAdmin && printer.OrganizationId != userOrgId)
            {
                _logger.LogWarning("Test print failed: User (OrgId: {UserOrgId}) not authorized for printer ID {PrinterId} (OrgId: {PrinterOrgId}).", userOrgId, printerId, printer.OrganizationId);
                return (false, "User not authorized for this printer.");
            }

            if (!printer.IsEnabled)
            {
                _logger.LogWarning("Test print failed: Printer '{PrinterName}' (ID: {PrinterId}) is disabled.", printer.Name, printerId);
                return (false, "Printer is disabled.");
            }

            _logger.LogInformation("Generating test print document for printer '{PrinterName}' (ID: {PrinterId}).", printer.Name, printerId);
            var docBuilder = new EscPosDocumentBuilder();
            docBuilder.InitializePrinter();
            docBuilder.SetAlignment(EscPosAlignment.Center);
            docBuilder.SetEmphasis(true);
            docBuilder.SetFontSize(2, 1);
            docBuilder.AppendLine("--- TEST PRINT ---");
            docBuilder.ResetFontSize();
            docBuilder.SetEmphasis(false);
            docBuilder.SetAlignment(EscPosAlignment.Left);
            docBuilder.AppendLine($"Stampante: {printer.Name} (ID: {printer.Id})");
            docBuilder.AppendLine($"Tipo: {printer.Type}");
            docBuilder.AppendLine($"Stringa Connessione: {printer.ConnectionString}");
            docBuilder.AppendLine($"Ora Test: {DateTime.UtcNow:dd/MM/yyyy HH:mm:ss UTC}");
            docBuilder.NewLine();
            docBuilder.AppendLine("--------------------------------");
            docBuilder.AppendLine("Test caratteri speciali:");
            docBuilder.AppendLine("Euro: €");
            docBuilder.AppendLine("Accenti minuscoli: à è ì ò ù");
            docBuilder.AppendLine("Accenti MAIUSCOLI: À È Ì Ò Ù");
            docBuilder.AppendLine("Altri comuni: ç ñ ¿ ¡ ß");
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
                docBuilder.PrintQRCode($"TestQR_{printer.Id}_{DateTime.UtcNow.Ticks}");
                docBuilder.AppendLine("QR Code Test OK (sopra)");
            }
            catch (Exception qrEx)
            {
                _logger.LogError(qrEx, "Failed to generate QR code for Test Print on printer ID: {PrinterId}.", printer.Id);
                docBuilder.AppendLine("QR Code Test Fallito.");
            }
            docBuilder.NewLine(3);
            docBuilder.CutPaper();

            byte[] testData = docBuilder.Build();
            _logger.LogInformation("Sending test print data (length: {DataLength} bytes) to printer '{PrinterName}'.", testData.Length, printer.Name);
            return await SendToPrinterAsync(printer, testData, PrintJobType.TestPrint);
        }
    }
}
