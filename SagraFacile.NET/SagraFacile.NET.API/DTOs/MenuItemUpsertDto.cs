using System.ComponentModel.DataAnnotations;

namespace SagraFacile.NET.API.DTOs
{
    /// <summary>
    /// Data Transfer Object for creating or updating MenuItems.
    /// Does not include the ID as it's typically provided via the route or generated on creation.
    /// </summary>
    public class MenuItemUpsertDto
    {
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        [Required]
        [Range(0, 9999.99)] // Example range, adjust as needed
        public decimal Price { get; set; }

        [Required]
        public int MenuCategoryId { get; set; }

        public bool IsNoteRequired { get; set; }

        [StringLength(200)]
        public string? NoteSuggestion { get; set; }

        public int? Scorta { get; set; }
    }
}
