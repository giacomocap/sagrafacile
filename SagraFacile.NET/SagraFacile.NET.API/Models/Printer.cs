using SagraFacile.NET.API.Models.Enums;
using System.ComponentModel.DataAnnotations;
// using SagraFacile.NET.API.Models.Enums; // Duplicate removed
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

        public bool IsEnabled { get; set; } = true;

        // New property for On-Demand Printing
        public PrintMode PrintMode { get; set; } = PrintMode.Immediate; // Default to immediate printing

        // New property for Standard Printers
        public DocumentType DocumentType { get; set; } = DocumentType.EscPos; // Default to ESC/POS for backward compatibility

        [StringLength(50)]
        public string? PaperSize { get; set; } // e.g., "A4", "A5", "Letter"

        // Navigation property for assignments
        public virtual ICollection<PrinterCategoryAssignment> CategoryAssignments { get; set; } = new List<PrinterCategoryAssignment>();

        // Potential navigation property if Area stores ReceiptPrinterId
        // public virtual ICollection<Area> AreasUsingAsReceiptPrinter { get; set; } = new List<Area>();
    }
}
