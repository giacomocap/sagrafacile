using System.ComponentModel.DataAnnotations;

namespace SagraFacile.NET.API.DTOs
{
    /// <summary>
    /// DTO for creating or updating an Area.
    /// </summary>
    public class AreaUpsertDto
    {
        [Required]
        [StringLength(100)]
        public required string Name { get; set; }

        // OrganizationId is needed to associate the area correctly,
        // especially during creation or if re-assigning (though re-assigning isn't typical).
        // For updates, the service layer should verify the user has access
        // to the organization of the Area being updated (identified by ID in the route).
        [Required]
        public int OrganizationId { get; set; }

        public bool EnableCompletionConfirmation { get; set; }
        public bool EnableKds { get; set; }
        public bool EnableWaiterConfirmation { get; set; }
        public bool EnableQueueSystem { get; set; }

        // Printing fields
        // ReceiptPrinterId selection will likely be handled separately or maybe added here
        public bool PrintComandasAtCashier { get; set; }
        public int? ReceiptPrinterId { get; set; }

        // New Charges
        public decimal GuestCharge { get; set; }
        public decimal TakeawayCharge { get; set; }
    }
}
