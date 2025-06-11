using System;
using System.ComponentModel.DataAnnotations;

namespace SagraFacile.NET.API.DTOs
{
    public class AdAreaAssignmentUpsertDto
    {
        [Required]
        public Guid AdMediaItemId { get; set; }

        [Required]
        public int AreaId { get; set; }

        [Required]
        public int DisplayOrder { get; set; }

        public int? DurationSeconds { get; set; }

        [Required]
        public bool IsActive { get; set; }
    }
}
