using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace SagraFacile.WindowsPrinterService.Printing
{
    public class RawPrinterHelperService : IRawPrinter
    {
        private readonly ILogger<RawPrinterHelperService> _logger;

        public RawPrinterHelperService(ILogger<RawPrinterHelperService> logger)
        {
            _logger = logger;
        }

        public Task<bool> PrintRawAsync(string printerName, byte[] rawData)
        {
            _logger.LogInformation("Attempting to print raw byte data to printer: {PrinterName}, Data Length: {DataLength} bytes", printerName, rawData?.Length ?? 0);
            
            if (string.IsNullOrWhiteSpace(printerName))
            {
                _logger.LogError("Printer name is null or empty");
                return Task.FromResult(false);
            }

            if (rawData == null || rawData.Length == 0)
            {
                _logger.LogError("Raw data is null or empty for printer: {PrinterName}", printerName);
                return Task.FromResult(false);
            }

            // Log first few bytes for debugging (ESC/POS commands)
            if (rawData.Length > 0)
            {
                var firstBytes = rawData.Length >= 10 ? rawData[..10] : rawData;
                var hexString = Convert.ToHexString(firstBytes);
                _logger.LogDebug("First {Count} bytes of raw data for printer {PrinterName}: {HexData}", firstBytes.Length, printerName, hexString);
            }

            try
            {
                bool success = RawPrinterHelper.SendBytesToPrinter(printerName, rawData);
                if (success)
                {
                    _logger.LogInformation("Successfully sent {DataLength} bytes to printer: {PrinterName}", rawData.Length, printerName);
                }
                else
                {
                    int errorCode = Marshal.GetLastWin32Error();
                    _logger.LogError("Failed to send raw byte data to printer {PrinterName}. Win32 Error Code: {ErrorCode} (0x{ErrorCodeHex:X8})", printerName, errorCode, errorCode);
                    
                    // Log common error codes for better debugging
                    string errorDescription = GetWin32ErrorDescription(errorCode);
                    if (!string.IsNullOrEmpty(errorDescription))
                    {
                        _logger.LogError("Win32 Error Description: {ErrorDescription}", errorDescription);
                    }
                }
                return Task.FromResult(success);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while sending raw byte data to printer: {PrinterName}", printerName);
                return Task.FromResult(false);
            }
        }

        public Task<bool> PrintRawAsync(string printerName, string rawData)
        {
            _logger.LogInformation("Attempting to print raw string data to printer: {PrinterName}, Data Length: {DataLength} characters", printerName, rawData?.Length ?? 0);
            
            if (string.IsNullOrWhiteSpace(printerName))
            {
                _logger.LogError("Printer name is null or empty");
                return Task.FromResult(false);
            }

            if (string.IsNullOrEmpty(rawData))
            {
                _logger.LogError("Raw data string is null or empty for printer: {PrinterName}", printerName);
                return Task.FromResult(false);
            }

            // Log first part of string for debugging
            var preview = rawData.Length > 50 ? rawData[..50] + "..." : rawData;
            _logger.LogDebug("Raw string data preview for printer {PrinterName}: {DataPreview}", printerName, preview.Replace("\n", "\\n").Replace("\r", "\\r"));

            try
            {
                bool success = RawPrinterHelper.SendStringToPrinter(printerName, rawData);
                if (success)
                {
                    _logger.LogInformation("Successfully sent {DataLength} characters to printer: {PrinterName}", rawData.Length, printerName);
                }
                else
                {
                    int errorCode = Marshal.GetLastWin32Error();
                    _logger.LogError("Failed to send raw string data to printer {PrinterName}. Win32 Error Code: {ErrorCode} (0x{ErrorCodeHex:X8})", printerName, errorCode, errorCode);
                    
                    // Log common error codes for better debugging
                    string errorDescription = GetWin32ErrorDescription(errorCode);
                    if (!string.IsNullOrEmpty(errorDescription))
                    {
                        _logger.LogError("Win32 Error Description: {ErrorDescription}", errorDescription);
                    }
                }
                return Task.FromResult(success);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while sending raw string data to printer: {PrinterName}", printerName);
                return Task.FromResult(false);
            }
        }

        private static string GetWin32ErrorDescription(int errorCode)
        {
            return errorCode switch
            {
                2 => "ERROR_FILE_NOT_FOUND - The printer name was not found",
                5 => "ERROR_ACCESS_DENIED - Access denied to the printer",
                87 => "ERROR_INVALID_PARAMETER - Invalid parameter passed to printer API",
                1801 => "ERROR_INVALID_PRINTER_NAME - The printer name is invalid",
                1802 => "ERROR_PRINTER_DRIVER_ALREADY_INSTALLED - Printer driver issue",
                1803 => "ERROR_PRINTER_DRIVER_IN_USE - Printer driver in use",
                1804 => "ERROR_SPOOL_FILE_NOT_FOUND - Print spooler file not found",
                1805 => "ERROR_SPL_NO_STARTDOC - StartDoc was not called",
                1806 => "ERROR_SPL_NO_ADDJOB - AddJob was not called",
                1807 => "ERROR_PRINT_PROCESSOR_ALREADY_INSTALLED - Print processor issue",
                1808 => "ERROR_PRINT_MONITOR_ALREADY_INSTALLED - Print monitor issue",
                1809 => "ERROR_INVALID_PRINT_MONITOR - Invalid print monitor",
                1810 => "ERROR_PRINT_MONITOR_IN_USE - Print monitor in use",
                1811 => "ERROR_PRINTER_HAS_JOBS_QUEUED - Printer has jobs queued",
                1812 => "ERROR_SUCCESS_REBOOT_REQUIRED - Success but reboot required",
                1813 => "ERROR_SUCCESS_RESTART_REQUIRED - Success but restart required",
                1814 => "ERROR_PRINTER_NOT_FOUND - Printer not found",
                1815 => "ERROR_PRINTER_DRIVER_WARNED - Printer driver warning",
                1816 => "ERROR_PRINTER_DRIVER_BLOCKED - Printer driver blocked",
                _ => string.Empty
            };
        }
    }
}
