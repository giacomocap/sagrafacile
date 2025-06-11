namespace SagraFacile.NET.API.DTOs
{
    public class KdsStationDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public int AreaId { get; set; }
        public int OrganizationId { get; set; }
        // Consider adding assigned category IDs or names if needed directly in this DTO later
    }
}
