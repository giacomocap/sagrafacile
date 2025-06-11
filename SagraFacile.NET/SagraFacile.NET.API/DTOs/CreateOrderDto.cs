using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SagraFacile.NET.API.DTOs
{
    public class CreateOrderDto
    {
        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "L'ID dell'area deve essere un intero positivo.")]
        public int AreaId { get; set; }

        [Required(ErrorMessage = "Il nome del cliente è obbligatorio.")]
        [StringLength(100, MinimumLength = 1, ErrorMessage = "Il nome del cliente deve essere tra 1 e 100 caratteri.")]
        public string CustomerName { get; set; } = string.Empty;

        [Required]
        [MinLength(1, ErrorMessage = "L'ordine deve contenere almeno un articolo.")]
        public required List<CreateOrderItemDto> Items { get; set; }

        [StringLength(50, ErrorMessage = "Il metodo di pagamento non può superare i 50 caratteri.")]
        public string? PaymentMethod { get; set; } // es: "Contanti", "POS"
        
        [Range(0, (double)decimal.MaxValue, ErrorMessage = "L'importo pagato non può essere negativo.")]
        public decimal? AmountPaid { get; set; } // Opzionale per pagamenti in contanti

        // Nuovi campi per Coperti (NumberOfGuests) e Asporto (IsTakeaway)
        [Range(0, 100, ErrorMessage = "Il numero di coperti deve essere tra 0 e 100.")] // Predefinito 1 coperto, massimo 100
        public int NumberOfGuests { get; set; } = 1;

        public bool IsTakeaway { get; set; } = false; // Predefinito non asporto

        [StringLength(50, ErrorMessage = "Il numero del tavolo non può superare i 50 caratteri.")]
        public string? TableNumber { get; set; }

        public int? CashierStationId { get; set; }
        
        // CashierId sarà determinato dall'utente autenticato in seguito
        // OrganizationId sarà determinato dall'Area in seguito
    }
}
