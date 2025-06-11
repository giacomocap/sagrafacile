using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SagraFacile.NET.API.DTOs
{
    /// <summary>
    /// Represents the data submitted for a public pre-order.
    /// </summary>
    public class PreOrderDto
    {
        [Required]
        public int OrganizationId { get; set; } // Need to know which org

        [Required]
        public int AreaId { get; set; } // Need to know which area

        [Required]
        [StringLength(100)]
        public required string CustomerName { get; set; }

        [Required]
        [EmailAddress]
        [StringLength(255)]
        public required string CustomerEmail { get; set; }

        [Required]
        [MinLength(1, ErrorMessage = "Pre-order must contain at least one item.")]
        public required List<PreOrderItemDto> Items { get; set; }

        // New fields for Coperti (NumberOfGuests) and Asporto (IsTakeaway)
        [Range(1, 100, ErrorMessage = "Number of guests must be between 1 and 100.")]
        public int NumberOfGuests { get; set; } = 1; // Default to 1 guest

        public bool IsTakeaway { get; set; } = false; // Default to not takeaway
    }
}
