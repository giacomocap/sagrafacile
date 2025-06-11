using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SagraFacile.NET.API.Models
{
    // Join table for Many-to-Many between Printer and MenuCategory
    public class PrinterCategoryAssignment
    {
        [Required]
        public int PrinterId { get; set; }

        [ForeignKey("PrinterId")]
        public virtual Printer? Printer { get; set; }

        [Required]
        public int MenuCategoryId { get; set; }

        [ForeignKey("MenuCategoryId")]
        public virtual MenuCategory? MenuCategory { get; set; }
    }
} 