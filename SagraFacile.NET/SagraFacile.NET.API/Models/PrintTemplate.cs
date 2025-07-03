using SagraFacile.NET.API.Models.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SagraFacile.NET.API.Models
{
    public class PrintTemplate
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public Guid OrganizationId { get; set; }

        [ForeignKey("OrganizationId")]
        public virtual Organization? Organization { get; set; }

        [Required]
        [StringLength(100)]
        public required string Name { get; set; }

        [Required]
        public PrintJobType TemplateType { get; set; }

        [Required]
        public DocumentType DocumentType { get; set; }

        public string? HtmlContent { get; set; }

        [StringLength(500)]
        public string? EscPosHeader { get; set; }

        [StringLength(500)]
        public string? EscPosFooter { get; set; }

        public bool IsDefault { get; set; }
    }
}
