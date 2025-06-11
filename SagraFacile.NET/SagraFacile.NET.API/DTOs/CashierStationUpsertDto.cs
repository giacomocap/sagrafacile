using System.ComponentModel.DataAnnotations;

namespace SagraFacile.NET.API.DTOs
{
    public class CashierStationUpsertDto
    {
        [Required]
        public int? AreaId { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; }

        [Required]
        public int? ReceiptPrinterId { get; set; }

        [Required]
        public bool PrintComandasAtThisStation { get; set; }

        [Required]
        public bool IsEnabled { get; set; }
    }
} 