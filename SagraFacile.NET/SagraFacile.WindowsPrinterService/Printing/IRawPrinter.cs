using System.Threading.Tasks;

namespace SagraFacile.WindowsPrinterService.Printing
{
    public interface IRawPrinter
    {
        Task<bool> PrintRawAsync(string printerName, byte[] rawData);
        Task<bool> PrintRawAsync(string printerName, string rawData); // Keep for compatibility with test button
    }
}
