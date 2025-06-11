using System.ComponentModel.DataAnnotations;

namespace SagraFacile.NET.API.DTOs
{
    /// <summary>
    /// Represents a single item within a public pre-order request.
    /// </summary>
    public class PreOrderItemDto
    {
        [Required]
        public int MenuItemId { get; set; }

        [Required]
        [Range(1, 100, ErrorMessage = "Quantity must be between 1 and 100.")] // Example range
        public int Quantity { get; set; }

        // Optional note for the item, if needed for pre-orders
        [StringLength(255)]
        public string? Note { get; set; }
    }
}
