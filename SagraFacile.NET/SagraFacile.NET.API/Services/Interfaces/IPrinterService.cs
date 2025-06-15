using SagraFacile.NET.API.DTOs;
using SagraFacile.NET.API.Models; // Required for Printer, Order models
using SagraFacile.NET.API.Models.Enums; // Required for PrintJobType
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SagraFacile.NET.API.Services.Interfaces
{
    public interface IPrinterService
    {
        Task<IEnumerable<PrinterDto>> GetPrintersAsync();
        Task<PrinterDto?> GetPrinterByIdAsync(int id);
        Task<(Printer? Printer, string? Error)> CreatePrinterAsync(PrinterUpsertDto printerDto);
        Task<(bool Success, string? Error)> UpdatePrinterAsync(int id, PrinterUpsertDto printerDto);
        Task<(bool Success, string? Error)> DeletePrinterAsync(int id);
        Task<bool> PrinterExistsAsync(int id); // Consider if needed publicly

        // Methods for orchestrating and dispatching print jobs
        Task<(bool Success, string? Error)> PrintOrderDocumentsAsync(Order order, PrintJobType jobType);
        Task<(bool Success, string? Error)> SendToPrinterAsync(Printer printer, byte[] data, PrintJobType jobType); // Added jobType for context

        Task<(bool Success, string? Error)> ReprintOrderDocumentsAsync(string orderId, ReprintRequestDto reprintRequest);

        // New method for Windows Companion App to get its configuration
        // WindowsPrinterName is no longer returned as it's managed by the client profile.
        Task<PrintMode?> GetPrinterConfigAsync(string instanceGuid);

        // Method to trigger a test print directly to a printer
        Task<(bool Success, string? Error)> SendTestPrintAsync(int printerId);

        // Placeholder for network printing logic (details TBD)
        // Task SendToNetworkPrinterAsync(int printerId, byte[] data); // Example
    }
}
