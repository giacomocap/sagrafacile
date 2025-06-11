using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SagraFacile.NET.API.Models
{
    public class AdAreaAssignment
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public Guid AdMediaItemId { get; set; }

        [ForeignKey("AdMediaItemId")]
        public AdMediaItem AdMediaItem { get; set; }

        [Required]
        public int AreaId { get; set; }

        [ForeignKey("AreaId")]
        public Area Area { get; set; }

        [Required]
        public int DisplayOrder { get; set; }

        public int? DurationSeconds { get; set; }

        [Required]
        public bool IsActive { get; set; }
    }
}
