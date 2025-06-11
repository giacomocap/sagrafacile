namespace SagraFacile.NET.API.DTOs
{
    public class OrganizationDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty; // Added Slug

        // Add other properties as needed, but avoid navigation properties
        // that could cause cycles unless using ReferenceHandler.Preserve
    }
}
