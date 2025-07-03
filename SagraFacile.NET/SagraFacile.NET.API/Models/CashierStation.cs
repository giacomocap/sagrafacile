using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SagraFacile.NET.API.Models
{
    public class CashierStation
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public Guid OrganizationId { get; set; }
        [ForeignKey("OrganizationId")]
        public Organization Organization { get; set; }

        [Required]
        public int AreaId { get; set; }
        [ForeignKey("AreaId")]
        public Area Area { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; }

        [Required]
        public int ReceiptPrinterId { get; set; }
        [ForeignKey("ReceiptPrinterId")]
        public Printer ReceiptPrinter { get; set; }

        [Required]
        public bool PrintComandasAtThisStation { get; set; } = false;

        [Required]
        public bool IsEnabled { get; set; } = true;
    }
}
