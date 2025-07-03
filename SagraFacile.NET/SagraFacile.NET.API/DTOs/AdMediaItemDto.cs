using System;

namespace SagraFacile.NET.API.DTOs
{
    public class AdMediaItemDto
    {
        public Guid Id { get; set; }
        public Guid OrganizationId { get; set; }
        public string Name { get; set; }
        public string MediaType { get; set; }
        public string FilePath { get; set; }
        public string MimeType { get; set; }
        public DateTime UploadedAt { get; set; }
    }
}
