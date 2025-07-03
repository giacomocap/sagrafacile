namespace SagraFacile.NET.API.DTOs
{
    public class OrganizationDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty; // Added Slug
        public string? SubscriptionStatus { get; set; } // Added for SaaS features

        // Add other properties as needed, but avoid navigation properties
        // that could cause cycles unless using ReferenceHandler.Preserve
    }
}
