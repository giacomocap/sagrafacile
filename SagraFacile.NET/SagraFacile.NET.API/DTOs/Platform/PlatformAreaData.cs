using System.Text.Json.Serialization;

namespace SagraFacile.NET.API.DTOs.Platform
{
    public class PlatformAreaData
    {
        // Note: Platform Area ID is not used for import, relying on localAreaId
        // [JsonPropertyName("id")]
        // public int Id { get; set; }

        [JsonPropertyName("name")]
        public required string Name { get; set; }

        [JsonPropertyName("localAreaId")]
        public int LocalAreaId { get; set; }

        [JsonPropertyName("slug")]
        public required string Slug { get; set; }
    }
} 