using System.Text.Json.Serialization;

namespace SagraFacile.NET.API.DTOs.Platform
{
    public class PlatformPreOrderPollResponse
    {
        [JsonPropertyName("preOrders")]
        public List<PlatformPreOrderDto> PreOrders { get; set; } = new List<PlatformPreOrderDto>();

        [JsonPropertyName("totalCount")]
        public int TotalCount { get; set; }
    }
} 