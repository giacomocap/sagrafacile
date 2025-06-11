using System.ComponentModel.DataAnnotations;

namespace SagraFacile.NET.API.DTOs
{
    public class CreateOrderItemDto
    {
        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "MenuItemId must be a positive integer.")]
        public int MenuItemId { get; set; }

        [Required]
        [Range(1, 100, ErrorMessage = "Quantity must be between 1 and 100.")] // Assuming a max quantity of 100
        public int Quantity { get; set; }

        [StringLength(255, ErrorMessage = "Note cannot exceed 255 characters.")]
        public string? Note { get; set; } // Include the note
    }
} 