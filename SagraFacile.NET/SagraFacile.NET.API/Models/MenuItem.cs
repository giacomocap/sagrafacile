using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SagraFacile.NET.API.Models
{
    public class MenuItem
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public required string Name { get; set; }

        [StringLength(255)]
        public string? Description { get; set; }

        [Required]
        [Column(TypeName = "decimal(18, 2)")] // Ensure proper precision for currency
        public decimal Price { get; set; }

        [StringLength(255)]
        public string? Variants { get; set; } // Simple text field for basic variants

        // Foreign Key for MenuCategory
        public int MenuCategoryId { get; set; }

        // Navigation property for MenuCategory
        [ForeignKey("MenuCategoryId")]
        public virtual MenuCategory? MenuCategory { get; set; }

        // Configuration for OrderItem notes
        public bool IsNoteRequired { get; set; } = false; // Default to not required

        [StringLength(255)]
        public string? NoteSuggestion { get; set; } // Suggestion text for the note field

        public int? Scorta { get; set; } // null = unlimited, integer = available quantity

        // Navigation property for OrderItems
        public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    }
}
