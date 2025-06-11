using System;

namespace SagraFacile.NET.API.DTOs
{
    public class AdAreaAssignmentDto
    {
        public Guid Id { get; set; }
        public Guid AdMediaItemId { get; set; }
        public int AreaId { get; set; }
        public int DisplayOrder { get; set; }
        public int? DurationSeconds { get; set; }
        public bool IsActive { get; set; }
        public AdMediaItemDto AdMediaItem { get; set; }
    }
}
