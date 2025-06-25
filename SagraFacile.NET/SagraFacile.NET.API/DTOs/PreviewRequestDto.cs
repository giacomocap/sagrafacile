using SagraFacile.NET.API.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace SagraFacile.NET.API.DTOs
{
    public class PreviewRequestDto
    {
        [Required]
        public string HtmlContent { get; set; }

        [Required]
        public PrintJobType TemplateType { get; set; }
    }
}
