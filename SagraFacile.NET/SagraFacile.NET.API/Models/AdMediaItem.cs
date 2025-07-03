using SagraFacile.NET.API.Models.Enums;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SagraFacile.NET.API.Models
{
    public class AdMediaItem
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public Guid OrganizationId { get; set; }

        [ForeignKey("OrganizationId")]
        public Organization Organization { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; }

        [Required]
        public MediaType MediaType { get; set; }

        [Required]
        public string FilePath { get; set; }

        [Required]
        public string MimeType { get; set; }

        [Required]
        public DateTime UploadedAt { get; set; }

        public ICollection<AdAreaAssignment> Assignments { get; set; } = new List<AdAreaAssignment>();
    }
}
