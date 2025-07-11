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
using ESCPOS_NET.Emitters;
using SagraFacile.NET.API.BackgroundServices;
using System.Reflection;
using System.IO;
using Scriban;

namespace SagraFacile.NET.API.Services
{
    public class PrinterService : BaseService, IPrinterService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<PrinterService> _logger;
        private readonly IHubContext<OrderHub> _orderHubContext;
        private readonly IPdfService _pdfService;

        public PrinterService(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor, ILogger<PrinterService> logger, IHubContext<OrderHub> orderHubContext, IPdfService pdfService)
            : base(httpContextAccessor)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _orderHubContext = orderHubContext ?? throw new ArgumentNullException(nameof(orderHubContext));
            _pdfService = pdfService ?? throw new ArgumentNullException(nameof(pdfService));
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

            if (!isSuperAdmin && printer.OrganizationId != userOrgId)
            {
                _logger.LogWarning("User (OrgId: {UserOrgId}) not authorized to access printer ID {PrinterId} (OrgId: {PrinterOrgId}).", userOrgId, id, printer.OrganizationId);
                return null;
            }

            _logger.LogInformation("Retrieved printer {PrinterId}.", id);
            return MapPrinterToDto(printer);
        }

        public async Task<(Printer? Printer, string? Error)> CreatePrinterAsync(PrinterUpsertDto printerDto)
        {
            _logger.LogInformation("Attempting to create printer: {PrinterName}, Type: {PrinterType}.", printerDto.Name, printerDto.Type);
            var (userOrgId, isSuperAdmin) = GetUserContext();

            if (!isSuperAdmin)
            {
                if (!userOrgId.HasValue)
                {
                    _logger.LogError("User organization context is missing for non-SuperAdmin during CreatePrinterAsync.");
                    return (null, "User organization context is missing.");
                }
                if (printerDto.OrganizationId != Guid.Empty && printerDto.OrganizationId != userOrgId.Value)
                {
                    _logger.LogWarning("User (OrgId: {UserOrgId}) attempted to create printer for different organization ({RequestedOrgId}).", userOrgId.Value, printerDto.OrganizationId);
                    return (null, "User is not authorized to create a printer for a different organization.");
                }
                printerDto.OrganizationId = userOrgId.Value; // Assign user's org ID
                _logger.LogDebug("Assigned printer to user's organization ID: {OrganizationId}.", userOrgId.Value);
            }
            else
            {
                if (printerDto.OrganizationId == Guid.Empty || !await _context.Organizations.AnyAsync(o => o.Id == printerDto.OrganizationId))
                {
                    _logger.LogWarning("SuperAdmin attempted to create printer without specifying OrganizationId.");
                    return (null, "SuperAdmin must specify an OrganizationId.");
                }
                if (!await _context.Organizations.AnyAsync(o => o.Id == printerDto.OrganizationId))
                {
                    _logger.LogWarning("SuperAdmin attempted to create printer for non-existent OrganizationId: {OrganizationId}.", printerDto.OrganizationId);
                    return (null, $"Invalid or non-existent OrganizationId: {printerDto.OrganizationId}");
                }
            }

            var printer = new Printer
            {
                Name = printerDto.Name,
                Type = printerDto.Type,
                ConnectionString = printerDto.ConnectionString,
                IsEnabled = printerDto.IsEnabled,
                OrganizationId = printerDto.OrganizationId,
                PrintMode = printerDto.PrintMode,
                DocumentType = printerDto.DocumentType,
                PaperSize = printerDto.PaperSize
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

            if (!isSuperAdmin && existingPrinter.OrganizationId != userOrgId)
            {
                _logger.LogWarning("User (OrgId: {UserOrgId}) not authorized to update printer ID {PrinterId} (OrgId: {PrinterOrgId}).", userOrgId, id, existingPrinter.OrganizationId);
                return (false, "User is not authorized to update this printer.");
            }

            if (!isSuperAdmin && existingPrinter.OrganizationId != printerDto.OrganizationId)
            {
                _logger.LogWarning("User (OrgId: {UserOrgId}) attempted to change printer {PrinterId} to different organization ({RequestedOrgId}).", userOrgId, id, printerDto.OrganizationId);
                return (false, "User is not authorized to change the printer's organization.");
            }

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

            existingPrinter.Name = printerDto.Name;
            existingPrinter.Type = printerDto.Type;
            existingPrinter.ConnectionString = printerDto.ConnectionString;
            existingPrinter.IsEnabled = printerDto.IsEnabled;
            existingPrinter.PrintMode = printerDto.PrintMode;
            existingPrinter.DocumentType = printerDto.DocumentType;
            existingPrinter.PaperSize = printerDto.PaperSize;

            try
            {
                await _context.SaveChangesAsync();
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

            if (!isSuperAdmin && printer.OrganizationId != userOrgId)
            {
                _logger.LogWarning("User (OrgId: {UserOrgId}) not authorized to delete printer ID {PrinterId} (OrgId: {PrinterOrgId}).", userOrgId, id, printer.OrganizationId);
                return (false, "User is not authorized to delete this printer.");
            }

            bool isInUseAsReceipt = await _context.Areas.AnyAsync(a => a.ReceiptPrinterId == id);
            if (isInUseAsReceipt)
            {
                _logger.LogWarning("Delete printer failed for ID {PrinterId}: It is assigned as a Receipt Printer for one or more Areas.", id);
                return (false, "Cannot delete printer because it is assigned as a Receipt Printer for one or more Areas.");
            }

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
                PrintMode = printer.PrintMode,
                DocumentType = printer.DocumentType,
                PaperSize = printer.PaperSize
            };
        }

        public async Task<(bool Success, string? Error)> SendToPrinterAsync(Printer printer, byte[] data, PrintJobType jobType)
        {
            if (!printer.IsEnabled)
            {
                _logger.LogWarning("Attempted to send job type {JobType} to disabled printer {PrinterName} (ID: {PrinterId}).", jobType, printer.Name, printer.Id);
                return (false, "Printer is disabled.");
            }

            _logger.LogInformation("Sending job type {JobType} to printer {PrinterName} (ID: {PrinterId}), Type: {PrinterType}", jobType, printer.Name, printer.Id, printer.Type);

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
                        return (false, "WindowsUSB printer ConnectionString (GUID) is missing.");
                    }

                    var connectionId = OrderHub.GetConnectionIdForPrinter(printer.ConnectionString);
                    if (connectionId != null)
                    {
                        var job = await _context.PrintJobs.FirstOrDefaultAsync(j => j.PrinterId == printer.Id && j.Status == PrintJobStatus.Processing);
                        if (job == null)
                        {
                            return (false, "Could not find a processing job to send.");
                        }
                        
                        // Determine content type based on printer document type
                        string contentType = printer.DocumentType == DocumentType.HtmlPdf ? "application/pdf" : "application/vnd.escpos";
                        
                        _logger.LogInformation("Sending SignalR message to Windows app. JobId: {JobId}, ContentType: {ContentType}, DataSize: {DataSize} bytes", 
                            job.Id, contentType, data.Length);
                        
                        await _orderHubContext.Clients.Client(connectionId).SendAsync("PrintJob", job.Id, data, contentType);
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while sending to printer {PrinterName} (ID: {PrinterId})", printer.Name, printer.Id);
                return (false, $"An unexpected error occurred during printing: {ex.Message}");
            }
        }

        public async Task<(bool Success, string? Error)> PrintOrderDocumentsAsync(Order order, PrintJobType jobType)
        {
            _logger.LogInformation("Queueing print jobs for Order ID: {OrderId}, JobType: {JobType}", order.Id, jobType);

            var orderWithDetails = await _context.Orders
                .Include(o => o.Organization)
                .Include(o => o.Area).ThenInclude(a => a!.ReceiptPrinter)
                .Include(o => o.CashierStation).ThenInclude(cs => cs!.ReceiptPrinter)
                .Include(o => o.OrderItems).ThenInclude(oi => oi.MenuItem).ThenInclude(mi => mi!.MenuCategory)
                .FirstOrDefaultAsync(o => o.Id == order.Id);

            if (orderWithDetails == null)
            {
                _logger.LogError("Order with ID {OrderId} not found when attempting to print.", order.Id);
                return (false, $"Order with ID {order.Id} not found.");
            }
            order = orderWithDetails;

            var jobsToCreate = new List<PrintJob>();

            if (jobType == PrintJobType.Receipt)
            {
                Printer? receiptPrinter = DetermineReceiptPrinter(order);
                if (receiptPrinter != null)
                {
                    byte[] content = await GeneratePrintContentAsync(order, receiptPrinter, PrintJobType.Receipt);
                    jobsToCreate.Add(CreateJob(order, receiptPrinter, PrintJobType.Receipt, content));
                }
            }
            else if (jobType == PrintJobType.Comanda)
            {
                var comandaPrinters = await DetermineComandaPrinters(order);
                foreach (var kvp in comandaPrinters)
                {
                    var printer = kvp.Key;
                    var items = kvp.Value;
                    byte[] content = await GeneratePrintContentAsync(order, printer, PrintJobType.Comanda, items);
                    jobsToCreate.Add(CreateJob(order, printer, PrintJobType.Comanda, content));
                }
            }

            if (!jobsToCreate.Any())
            {
                return (true, "No print jobs generated.");
            }

            _context.PrintJobs.AddRange(jobsToCreate);
            await _context.SaveChangesAsync();

            if (jobsToCreate.Any(j => j.JobType == PrintJobType.Receipt))
            {
                PrintJobProcessor.Trigger();
            }

            return (true, null);
        }

        public async Task<(bool Success, string? Error)> ReprintOrderDocumentsAsync(string orderId, ReprintRequestDto reprintRequest)
        {
            var order = await _context.Orders
                .Include(o => o.Organization)
                .Include(o => o.Area).ThenInclude(a => a!.ReceiptPrinter)
                .Include(o => o.CashierStation).ThenInclude(cs => cs!.ReceiptPrinter)
                .Include(o => o.OrderItems).ThenInclude(oi => oi.MenuItem).ThenInclude(mi => mi!.MenuCategory)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null)
            {
                return (false, $"Order with ID {orderId} not found.");
            }

            Printer? targetPrinter = await DetermineReprintTargetPrinter(order, reprintRequest.PrinterId);
            if (targetPrinter == null)
            {
                return (false, "No enabled printer found to handle the reprint.");
            }

            var jobsToCreate = new List<PrintJob>();

            // Generate and add receipt job
            byte[] receiptContent = await GeneratePrintContentAsync(order, targetPrinter, PrintJobType.Receipt, null, true);
            jobsToCreate.Add(CreateJob(order, targetPrinter, PrintJobType.Receipt, receiptContent));

            // Generate and add consolidated comanda job if requested
            if (reprintRequest.ReprintJobType == ReprintType.ReceiptAndComandas && order.OrderItems.Any())
            {
                byte[] comandaContent = await GeneratePrintContentAsync(order, targetPrinter, PrintJobType.Comanda, order.OrderItems, true);
                jobsToCreate.Add(CreateJob(order, targetPrinter, PrintJobType.Comanda, comandaContent));
            }

            _context.PrintJobs.AddRange(jobsToCreate);
            await _context.SaveChangesAsync();
            PrintJobProcessor.Trigger();

            return (true, null);
        }

        public async Task<(bool Success, string? Error)> SendTestPrintAsync(int printerId)
        {
            var printer = await _context.Printers.FindAsync(printerId);
            if (printer == null || !printer.IsEnabled)
            {
                return (false, "Printer not found or is disabled.");
            }

            // Test prints don't have an order, so we pass null
            byte[] content = await GeneratePrintContentAsync(null, printer, PrintJobType.TestPrint);
            var job = CreateJob(null, printer, PrintJobType.TestPrint, content);

            _context.PrintJobs.Add(job);
            await _context.SaveChangesAsync();
            PrintJobProcessor.Trigger();

            return (true, null);
        }

        public async Task UpdatePrintJobStatusAsync(Guid jobId, bool success, string? errorMessage)
        {
            var job = await _context.PrintJobs.FindAsync(jobId);
            if (job == null)
            {
                _logger.LogWarning("Received status update for non-existent job ID: {JobId}", jobId);
                return;
            }

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
                job.ErrorMessage = errorMessage;
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Updated status for job {JobId} to {Status}.", jobId, job.Status);
        }

        private async Task<byte[]> GeneratePrintContentAsync(Order? order, Printer printer, PrintJobType jobType, IEnumerable<OrderItem>? items = null, bool isReprint = false)
        {
            if (printer.DocumentType == DocumentType.HtmlPdf)
            {
                // Handle test prints for HTML/PDF printers with a simple test HTML
                if (jobType == PrintJobType.TestPrint)
                {
                    var testHtml = await GenerateTestHtmlContent(printer);
                    var sampleOrder = CreateSampleOrderForTest(printer.OrganizationId);
                    return await _pdfService.CreatePdfFromHtmlAsync(sampleOrder, testHtml, printer.PaperSize);
                }

                var template = await _context.PrintTemplates
                    .Where(t => t.OrganizationId == printer.OrganizationId && t.TemplateType == jobType && t.DocumentType == DocumentType.HtmlPdf && t.IsDefault)
                    .FirstOrDefaultAsync();

                if (template == null || string.IsNullOrWhiteSpace(template.HtmlContent))
                {
                    _logger.LogWarning("No default HTML template found for JobType {JobType} and Organization {OrgId}. Cannot generate PDF.", jobType, printer.OrganizationId);
                    return Array.Empty<byte>();
                }

                if (order == null)
                {
                    _logger.LogWarning("Cannot generate HTML/PDF content for JobType {JobType} without an order.", jobType);
                    return Array.Empty<byte>();
                }

                return await _pdfService.CreatePdfFromHtmlAsync(order, template.HtmlContent, printer.PaperSize);
            }
            else // Default to EscPos
            {
                var template = await _context.PrintTemplates
                    .Where(t => t.OrganizationId == printer.OrganizationId && t.TemplateType == jobType && t.DocumentType == DocumentType.EscPos && t.IsDefault)
                    .FirstOrDefaultAsync();

                EscPosDocumentBuilder docBuilder;
                switch (jobType)
                {
                    case PrintJobType.Receipt:
                        if (order == null) return Array.Empty<byte>();
                        docBuilder = BuildReceiptDocument(order, isReprint, template?.EscPosHeader, template?.EscPosFooter);
                        break;
                    case PrintJobType.Comanda:
                        if (order == null || items == null) return Array.Empty<byte>();
                        string title = isReprint ? $"RISTAMPA COMANDA - CASSA ({printer.Name})" : $"COMANDA - {printer.Name}";
                        docBuilder = BuildComandaDocument(order, items, title, template?.EscPosHeader, template?.EscPosFooter);
                        break;
                    case PrintJobType.TestPrint:
                        docBuilder = BuildTestDocument(printer);
                        break;
                    default:
                        return Array.Empty<byte>();
                }
                return docBuilder.Build();
            }
        }

        // Private helper methods for document generation and printer determination
        private Printer? DetermineReceiptPrinter(Order order)
        {
            if (order.CashierStationId.HasValue && order.CashierStation?.ReceiptPrinter != null && order.CashierStation.ReceiptPrinter.IsEnabled)
            {
                return order.CashierStation.ReceiptPrinter;
            }
            if (order.Area?.ReceiptPrinter != null && order.Area.ReceiptPrinter.IsEnabled)
            {
                return order.Area.ReceiptPrinter;
            }
            return null;
        }

        private async Task<Dictionary<Printer, List<OrderItem>>> DetermineComandaPrinters(Order order)
        {
            var comandaPrinters = new Dictionary<Printer, List<OrderItem>>();

            if (order.CashierStationId.HasValue && order.CashierStation?.PrintComandasAtThisStation == true && order.CashierStation.ReceiptPrinter != null)
            {
                comandaPrinters.Add(order.CashierStation.ReceiptPrinter, order.OrderItems.ToList());
                return comandaPrinters;
            }
            
            if (order.Area?.PrintComandasAtCashier == true && order.Area.ReceiptPrinter != null)
            {
                comandaPrinters.Add(order.Area.ReceiptPrinter, order.OrderItems.ToList());
                return comandaPrinters;
            }

            var categoryAssignments = await _context.PrinterCategoryAssignments
                .Include(pca => pca.Printer)
                .Where(pca => pca.Printer.OrganizationId == order.OrganizationId && pca.Printer.IsEnabled)
                .ToListAsync();

            foreach (var item in order.OrderItems)
            {
                var printers = categoryAssignments
                    .Where(pca => pca.MenuCategoryId == item.MenuItem.MenuCategoryId)
                    .Select(pca => pca.Printer);
                
                foreach (var printer in printers)
                {
                    if (!comandaPrinters.ContainsKey(printer))
                    {
                        comandaPrinters[printer] = new List<OrderItem>();
                    }
                    comandaPrinters[printer].Add(item);
                }
            }
            return comandaPrinters;
        }

        private async Task<Printer?> DetermineReprintTargetPrinter(Order order, int? specifiedPrinterId)
        {
            if (specifiedPrinterId.HasValue)
            {
                var printer = await _context.Printers.FirstOrDefaultAsync(p => p.Id == specifiedPrinterId.Value && p.OrganizationId == order.OrganizationId && p.IsEnabled);
                if (printer != null) return printer;
            }
            return DetermineReceiptPrinter(order);
        }

        private EscPosDocumentBuilder BuildReceiptDocument(Order order, bool isReprint = false, string? header = null, string? footer = null)
        {
            var docBuilder = new EscPosDocumentBuilder();
            docBuilder.InitializePrinter();
            docBuilder.SetAlignment(EscPosAlignment.Center);

            if (!string.IsNullOrWhiteSpace(header))
            {
                docBuilder.AppendLine(header);
            }

            if (isReprint)
            {
                docBuilder.SetEmphasis(true).SetFontSize(1, 2).AppendLine("--- RISTAMPA SCONTRINO ---").ResetFontSize().SetEmphasis(false);
            }
            docBuilder.SetEmphasis(true).AppendLine(order.Organization?.Name ?? "Sagrafacile").SetEmphasis(false);
            docBuilder.AppendLine($"Area: {order.Area?.Name ?? "N/A"}");
            if (order.CashierStation != null) docBuilder.AppendLine($"Cassa: {order.CashierStation.Name}");
            docBuilder.AppendLine($"Ordine N. {order.DisplayOrderNumber ?? order.Id}");
            docBuilder.AppendLine(order.OrderDateTime.ToString("dd/MM/yyyy HH:mm:ss"));
            if (order.IsTakeaway) docBuilder.SetEmphasis(true).SetFontSize(2, 2).AppendLine("ASPORTO").ResetFontSize().SetEmphasis(false);
            if (order.NumberOfGuests > 0 && !order.IsTakeaway) docBuilder.AppendLine($"Coperti: {order.NumberOfGuests}");
            docBuilder.AppendLine($"Cliente: {order.CustomerName ?? "Anonimo"}");
            docBuilder.SetAlignment(EscPosAlignment.Left).AppendLine("--------------------------------");

            var itemsByCategory = order.OrderItems.GroupBy(oi => oi.MenuItem?.MenuCategory?.Name ?? "Senza Categoria").OrderBy(g => g.Key);
            foreach (var group in itemsByCategory)
            {
                docBuilder.SetEmphasis(true).AppendLine(group.Key.ToUpper()).SetEmphasis(false);
                foreach (var item in group)
                {
                    string itemName = item.MenuItem?.Name ?? "Articolo Sconosciuto";
                    string quantityPrice = $"{item.Quantity} x {item.UnitPrice:C}";
                    string totalPrice = (item.Quantity * item.UnitPrice).ToString("C");
                    docBuilder.AppendLine($"{itemName.PadRight(20).Substring(0, 20)} {quantityPrice.PadRight(10)} {totalPrice.PadLeft(8)}");
                    if (!string.IsNullOrWhiteSpace(item.Note)) docBuilder.AppendLine($"    Nota: {item.Note.Trim()}");
                }
            }
            docBuilder.AppendLine("--------------------------------");
            decimal itemsSubtotal = order.OrderItems.Sum(oi => oi.Quantity * oi.UnitPrice);
            docBuilder.SetAlignment(EscPosAlignment.Right).SetEmphasis(true).AppendLine($"SUBTOTALE: {itemsSubtotal:C}").SetEmphasis(false);
            docBuilder.SetAlignment(EscPosAlignment.Left).AppendLine("--------------------------------");

            if (order.IsTakeaway && order.Area?.TakeawayCharge > 0) docBuilder.AppendLine($"Contributo Asporto: {order.Area.TakeawayCharge:C}");
            else if (!order.IsTakeaway && order.Area?.GuestCharge > 0 && order.NumberOfGuests > 0) docBuilder.AppendLine($"Coperto ({order.NumberOfGuests} x {order.Area.GuestCharge:C}): {(order.NumberOfGuests * order.Area.GuestCharge):C}");

            docBuilder.SetAlignment(EscPosAlignment.Right).SetEmphasis(true).SetFontSize(1, 2).AppendLine($"TOTALE: {order.TotalAmount:C}").ResetFontSize().SetEmphasis(false);
            docBuilder.SetAlignment(EscPosAlignment.Center).NewLine();
            try
            {
                docBuilder.PrintQRCode($"Sagrafacile_Order_{order.Id}", 8);
                docBuilder.NewLine();
            }
            catch (Exception qrEx)
            {
                _logger.LogError(qrEx, "Failed to generate QR code for Order ID: {OrderId}", order.Id);
                docBuilder.AppendLine("QR Code non disponibile.");
            }
            
            if (!string.IsNullOrWhiteSpace(footer))
            {
                docBuilder.AppendLine(footer);
            }
            else
            {
                docBuilder.AppendLine("Grazie e arrivederci!");
            }

            docBuilder.NewLine(5).CutPaper();
            return docBuilder;
        }

        private EscPosDocumentBuilder BuildComandaDocument(Order order, IEnumerable<OrderItem> items, string title, string? header = null, string? footer = null)
        {
            var docBuilder = new EscPosDocumentBuilder();
            docBuilder.InitializePrinter();
            docBuilder.SetAlignment(EscPosAlignment.Center);

            if (!string.IsNullOrWhiteSpace(header))
            {
                docBuilder.AppendLine(header);
            }

            docBuilder.SetEmphasis(true).SetFontSize(2, 2).AppendLine(title).ResetFontSize().SetEmphasis(false);
            docBuilder.AppendLine($"Ordine: {order.DisplayOrderNumber ?? order.Id} - {order.OrderDateTime:HH:mm}");
            if (!string.IsNullOrEmpty(order.TableNumber)) docBuilder.AppendLine($"Tavolo: {order.TableNumber}");
            if (!string.IsNullOrEmpty(order.CustomerName)) docBuilder.AppendLine($"Cliente: {order.CustomerName}");
            if (order.IsTakeaway) docBuilder.AppendLine("**ASPORTO**");
            docBuilder.SetAlignment(EscPosAlignment.Left).AppendLine("--------------------------------");

            foreach (var item in items)
            {
                docBuilder.SetFontSize(1, 2).AppendLine($"{item.Quantity} x {item.MenuItem?.Name ?? "N/A"}").ResetFontSize();
                if (!string.IsNullOrWhiteSpace(item.Note)) docBuilder.SetEmphasis(false).AppendLine($"    >> {item.Note.Trim()}");
            }

            docBuilder.AppendLine("--------------------------------");

            if (!string.IsNullOrWhiteSpace(footer))
            {
                docBuilder.SetAlignment(EscPosAlignment.Center).AppendLine(footer);
            }

            docBuilder.NewLine(3).CutPaper();
            return docBuilder;
        }

        private EscPosDocumentBuilder BuildTestDocument(Printer printer)
        {
            var docBuilder = new EscPosDocumentBuilder();
            docBuilder.InitializePrinter();
            docBuilder.SetAlignment(EscPosAlignment.Center).SetEmphasis(true).SetFontSize(2, 1).AppendLine("--- TEST PRINT ---").ResetFontSize().SetEmphasis(false);
            docBuilder.SetAlignment(EscPosAlignment.Left);
            docBuilder.AppendLine($"Stampante: {printer.Name} (ID: {printer.Id})");
            docBuilder.AppendLine($"Tipo: {printer.Type}");
            docBuilder.AppendLine($"Stringa Connessione: {printer.ConnectionString}");
            docBuilder.AppendLine($"Ora Test: {DateTime.UtcNow:dd/MM/yyyy HH:mm:ss UTC}");
            docBuilder.NewLine().AppendLine("--------------------------------");
            docBuilder.AppendLine("Test caratteri speciali: Euro: €, Accenti: à è ì ò ù");
            docBuilder.AppendLine("--------------------------------");
            docBuilder.NewLine(3).CutPaper();
            return docBuilder;
        }

        private PrintJob CreateJob(Order? order, Printer printer, PrintJobType jobType, byte[] content)
        {
            return new PrintJob
            {
                Id = Guid.NewGuid(),
                OrganizationId = order?.OrganizationId ?? printer.OrganizationId,
                AreaId = order?.AreaId, // This is now nullable
                OrderId = order?.Id,
                PrinterId = printer.Id,
                JobType = jobType,
                Status = PrintJobStatus.Pending,
                Content = content,
                CreatedAt = DateTime.UtcNow
            };
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

        private async Task<string> GenerateTestHtmlContent(Printer printer)
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var testTemplateName = "SagraFacile.NET.API.PrintTemplates.Html.test-print.html";
                await using var stream = assembly.GetManifestResourceStream(testTemplateName);
                if (stream == null)
                {
                    _logger.LogWarning("Embedded test print template not found: {TemplateName}", testTemplateName);
                    return GenerateFallbackTestHtml(printer);
                }
                using var reader = new StreamReader(stream);
                var templateHtml = await reader.ReadToEndAsync();

                // Parse the template with Scriban and populate with printer data
                var template = Template.Parse(templateHtml);
                var model = new
                {
                    printer = new
                    {
                        name = printer.Name,
                        id = printer.Id,
                        type = printer.Type.ToString(),
                        document_type = printer.DocumentType.ToString(),
                        connection_string = printer.ConnectionString,
                        paper_size = printer.PaperSize
                    },
                    test_datetime = DateTime.Now
                };

                return await template.RenderAsync(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate test HTML content for printer {PrinterId}", printer.Id);
                return GenerateFallbackTestHtml(printer);
            }
        }

        private string GenerateFallbackTestHtml(Printer printer)
        {
            return $@"
<!DOCTYPE html>
<html lang='it'>
<head>
    <meta charset='UTF-8'>
    <title>Test Stampa - {printer.Name}</title>
    <style>
        body {{ font-family: Arial, sans-serif; padding: 20px; text-align: center; }}
        .test-container {{ max-width: 400px; margin: 0 auto; padding: 20px; border: 2px solid #000; }}
        .header {{ font-size: 24px; font-weight: bold; margin-bottom: 20px; }}
        .info {{ text-align: left; margin: 10px 0; padding: 10px; background-color: #f8f8f8; }}
    </style>
</head>
<body>
    <div class='test-container'>
        <div class='header'>🖨️ TEST STAMPA</div>
        <div class='info'><strong>Stampante:</strong> {printer.Name} (ID: {printer.Id})</div>
        <div class='info'><strong>Tipo:</strong> {printer.Type}</div>
        <div class='info'><strong>Documento:</strong> {printer.DocumentType}</div>
        <div class='info'><strong>Connessione:</strong> {printer.ConnectionString}</div>
        <div class='info'><strong>Data Test:</strong> {DateTime.Now:dd/MM/yyyy HH:mm:ss}</div>
        <p>✅ Test di stampa completato con successo!</p>
    </div>
</body>
</html>";
        }

        private Order CreateSampleOrderForTest(Guid organizationId)
        {
            var orderId = $"TEST_{Guid.NewGuid().ToString().Substring(0, 8)}";
            return new Order
            {
                Id = orderId,
                DisplayOrderNumber = "T-001",
                OrganizationId = organizationId,
                Organization = new Organization { Name = "Test Organization" },
                Area = new Area { Name = "Test Area", GuestCharge = 1.0m, TakeawayCharge = 0.5m },
                CashierStation = new CashierStation { Name = "Test Cassa" },
                OrderDateTime = DateTime.Now,
                CustomerName = "Test Customer",
                NumberOfGuests = 1,
                TotalAmount = 10.00m,
                IsTakeaway = false,
                OrderItems = new List<OrderItem>
                {
                    new OrderItem
                    {
                        MenuItem = new MenuItem { Name = "Test Item", MenuCategory = new MenuCategory { Name = "Test Category" } },
                        Quantity = 1,
                        UnitPrice = 10.00m
                    }
                }
            };
        }
    }
}
