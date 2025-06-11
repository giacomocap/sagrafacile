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
            _logger.LogInformation("Attempting to print raw byte data to printer: {PrinterName}", printerName);
            try
            {
                bool success = RawPrinterHelper.SendBytesToPrinter(printerName, rawData);
                if (success)
                {
                    _logger.LogInformation("Successfully sent raw byte data to printer: {PrinterName}", printerName);
                }
                else
                {
                    int errorCode = Marshal.GetLastWin32Error();
                    _logger.LogError("Failed to send raw byte data to printer {PrinterName}. Win32 Error Code: {ErrorCode}", printerName, errorCode);
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
            _logger.LogInformation("Attempting to print raw string data to printer: {PrinterName}", printerName);
            try
            {
                bool success = RawPrinterHelper.SendStringToPrinter(printerName, rawData);
                if (success)
                {
                    _logger.LogInformation("Successfully sent raw string data to printer: {PrinterName}", printerName);
                }
                else
                {
                    int errorCode = Marshal.GetLastWin32Error();
                    _logger.LogError("Failed to send raw string data to printer {PrinterName}. Win32 Error Code: {ErrorCode}", printerName, errorCode);
                }
                return Task.FromResult(success);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while sending raw string data to printer: {PrinterName}", printerName);
                return Task.FromResult(false);
            }
        }
    }
}
