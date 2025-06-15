using System.Text.Json.Serialization;

namespace SagraFacile.WindowsPrinterService.Models
{
    public class PrinterConfigDto
    {
        [JsonPropertyName("printMode")]
        public PrintMode PrintMode { get; set; }

        // [JsonPropertyName("windowsPrinterName")] // Removed
        // public string? WindowsPrinterName { get; set; } // Removed
    }
}
