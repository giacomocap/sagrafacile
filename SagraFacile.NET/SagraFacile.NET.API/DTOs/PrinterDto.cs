using SagraFacile.NET.API.Models.Enums;

namespace SagraFacile.NET.API.DTOs
{
    public class PrinterDto
    {
        public int Id { get; set; }
        public int OrganizationId { get; set; }
        public required string Name { get; set; }
        public PrinterType Type { get; set; }
        public required string ConnectionString { get; set; }
        // public string? WindowsPrinterName { get; set; } // Removed
        public bool IsEnabled { get; set; }
        public PrintMode PrintMode { get; set; } // Added PrintMode
    }
}
