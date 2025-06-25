using SagraFacile.NET.API.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace SagraFacile.NET.API.DTOs
{
    public class PrintTemplateUpsertDto
    {
        [Required]
        [StringLength(100)]
        public string Name { get; set; }

        [Required]
        public int OrganizationId { get; set; }

        [Required]
        public PrintJobType TemplateType { get; set; }

        [Required]
        public DocumentType DocumentType { get; set; }

        public string? HtmlContent { get; set; }

        public string? EscPosHeader { get; set; }

        public string? EscPosFooter { get; set; }

        public bool IsDefault { get; set; }
    }
}
