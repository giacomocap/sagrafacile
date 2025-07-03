using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SagraFacile.NET.API.Models
{
    public class Area
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public required string Name { get; set; }

        // URL-friendly identifier
        [StringLength(100)] // Keep length consistent with Name
        public string Slug { get; set; } = string.Empty; // Will be configured as required and unique in DbContext

        // Foreign Key for Organization
        public Guid OrganizationId { get; set; }

        // Navigation property for Organization
        [ForeignKey("OrganizationId")]
        public virtual Organization? Organization { get; set; }

        // Workflow Configuration Flags (See WorkflowArchitecture.md)
        public bool EnableWaiterConfirmation { get; set; } = false;
        public bool EnableKds { get; set; } = false;
        public bool EnableCompletionConfirmation { get; set; } = false;

        // Printing Configuration (See PrinterArchitecture.md)
        public int? ReceiptPrinterId { get; set; } // Nullable FK to Printer

        [ForeignKey("ReceiptPrinterId")]
        public virtual Printer? ReceiptPrinter { get; set; }

        // If true, comandas print at ReceiptPrinterId; if false, they print at assigned station printers.
        public bool PrintComandasAtCashier { get; set; } = false;

        // Customer Queue System
        public bool EnableQueueSystem { get; set; } = false;

        // --- New Charges ---
        [Column(TypeName = "decimal(18, 2)")]
        public decimal GuestCharge { get; set; } = 0;

        [Column(TypeName = "decimal(18, 2)")]
        public decimal TakeawayCharge { get; set; } = 0;


        // Navigation properties
        public virtual ICollection<MenuCategory> MenuCategories { get; set; } = new List<MenuCategory>();
        public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
    }
}
