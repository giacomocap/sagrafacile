using System.Text.Json.Serialization;

namespace SagraFacile.NET.API.DTOs.Platform
{
    public class PlatformPreOrderItemDto
    {
        // Note: Platform Item ID is not used for import, relying on localMenuItemId
        // [JsonPropertyName("id")]
        // public int Id { get; set; }

        [JsonPropertyName("localMenuItemId")]
        public int LocalMenuItemId { get; set; }

        [JsonPropertyName("name")]
        public required string Name { get; set; }

        [JsonPropertyName("quantity")]
        public int Quantity { get; set; }

        [JsonPropertyName("unitPrice")]
        public required string UnitPrice { get; set; }

        [JsonPropertyName("note")]
        public string? Note { get; set; }
    }
} 