using SagraFacile.WindowsPrinterService.Models; // For PrintMode enum
using System.Text.Json.Serialization;

namespace SagraFacile.WindowsPrinterService.DTOs
{
    public class PrinterConfigDto
    {
        [JsonPropertyName("printMode")]
        public PrintMode PrintMode { get; set; }

        [JsonPropertyName("windowsPrinterName")]
        public string? WindowsPrinterName { get; set; }
    }
}
