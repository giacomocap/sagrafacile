using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SagraFacile.NET.API.Models
{
    public class MenuCategory
    {
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public required string Name { get; set; }

        // Foreign Key for Area
        public int AreaId { get; set; }

        // Navigation property for Area
        [ForeignKey("AreaId")]
        public virtual Area? Area { get; set; }

        // Navigation property for MenuItems
        public virtual ICollection<MenuItem> MenuItems { get; set; } = new List<MenuItem>();

        // Navigation property for Printer Assignments
        public virtual ICollection<PrinterCategoryAssignment> PrinterAssignments { get; set; } = new List<PrinterCategoryAssignment>();
    }
}
