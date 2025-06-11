using SagraFacile.NET.API.Models.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SagraFacile.NET.API.Models
{
    public class Printer
    {
        public int Id { get; set; }

        [Required]
        public int OrganizationId { get; set; }

        [ForeignKey("OrganizationId")]
        public virtual Organization? Organization { get; set; }

        [Required]
        [StringLength(100)]
        public required string Name { get; set; } // User-friendly name

        [Required]
        public PrinterType Type { get; set; }

        [Required]
        [StringLength(255)] // Sufficient for IP:Port or GUID
        public required string ConnectionString { get; set; }

        [StringLength(255)] // Max length for printer names
        public string? WindowsPrinterName { get; set; } // Required if Type is WindowsUsb

        public bool IsEnabled { get; set; } = true;

        // Navigation property for assignments
        public virtual ICollection<PrinterCategoryAssignment> CategoryAssignments { get; set; } = new List<PrinterCategoryAssignment>();

        // Potential navigation property if Area stores ReceiptPrinterId
        // public virtual ICollection<Area> AreasUsingAsReceiptPrinter { get; set; } = new List<Area>();
    }
} 