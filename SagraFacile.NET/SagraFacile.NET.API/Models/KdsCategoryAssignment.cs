using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SagraFacile.NET.API.Models
{
    // Join table for the many-to-many relationship between KdsStation and MenuCategory
    public class KdsCategoryAssignment
    {
        [Required]
        public int KdsStationId { get; set; }
        [ForeignKey("KdsStationId")]
        public virtual KdsStation KdsStation { get; set; } = null!;

        [Required]
        public int MenuCategoryId { get; set; }
        [ForeignKey("MenuCategoryId")]
        public virtual MenuCategory MenuCategory { get; set; } = null!;
    }
}
