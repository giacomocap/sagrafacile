using SagraFacile.NET.API.Models.Enums;

namespace SagraFacile.NET.API.DTOs
{
    public class PrintTemplateDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public Guid OrganizationId { get; set; }
        public PrintJobType TemplateType { get; set; }
        public DocumentType DocumentType { get; set; }
        public string? HtmlContent { get; set; }
        public string? EscPosHeader { get; set; }
        public string? EscPosFooter { get; set; }
        public bool IsDefault { get; set; }
    }
}
