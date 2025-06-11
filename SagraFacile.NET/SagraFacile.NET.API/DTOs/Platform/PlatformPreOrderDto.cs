using System.Text.Json.Serialization;
using SagraFacile.NET.API.Models; // For PreOrderStatus enum if needed (adjust if enum is defined elsewhere)

namespace SagraFacile.NET.API.DTOs.Platform
{
    public class PlatformPreOrderDto
    {
        [JsonPropertyName("id")]
        public required string Id { get; set; }

        [JsonPropertyName("customerName")]
        public required string CustomerName { get; set; }

        [JsonPropertyName("customerEmail")]
        public string? CustomerEmail { get; set; } // Assuming email is optional

        [JsonPropertyName("totalAmount")]
        public required string TotalAmount { get; set; } // Accept as string from API

        [JsonPropertyName("status")]
        public required string Status { get; set; } // Using string for flexibility, map to local enum later

        [JsonPropertyName("orderDateTime")]
        public DateTimeOffset OrderDateTime { get; set; }

        [JsonPropertyName("area")]
        public required PlatformAreaData Area { get; set; }

        [JsonPropertyName("items")]
        public List<PlatformPreOrderItemDto> Items { get; set; } = new List<PlatformPreOrderItemDto>();

        [JsonPropertyName("numberOfGuests")]
        public int NumberOfGuests { get; set; }

        [JsonPropertyName("isTakeaway")]
        public bool IsTakeaway { get; set; }
    }
} 