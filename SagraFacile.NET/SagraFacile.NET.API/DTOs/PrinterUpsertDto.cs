using SagraFacile.NET.API.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace SagraFacile.NET.API.DTOs
{
    public class PrinterUpsertDto
    {
        // Id is typically not included in Upsert DTOs for POST/PUT
        // OrganizationId will be derived from user context or validated

        [Required]
        public int OrganizationId { get; set; } // Required, but validated against user context

        [Required]
        [StringLength(100)]
        public required string Name { get; set; }

        [Required]
        public PrinterType Type { get; set; }

        [Required]
        [StringLength(255)]
        public required string ConnectionString { get; set; }

        // [StringLength(255)] // Removed
        // public string? WindowsPrinterName { get; set; } // Removed

        [Required]
        public bool IsEnabled { get; set; } = true;

        [Required]
        public PrintMode PrintMode { get; set; } = PrintMode.Immediate; // Added PrintMode, defaults to Immediate

        // Custom validation might be needed here, e.g., in the service/controller.
        // WindowsPrinterName is now managed by the client profile.
    }
}
