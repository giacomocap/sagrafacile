using SagraFacile.NET.API.Models; // For KdsStatus enum

namespace SagraFacile.NET.API.DTOs
{
    /// <summary>
    /// Represents an order item as displayed on the KDS,
    /// filtered for the specific station.
    /// </summary>
    public class KdsOrderItemDto
    {
        public int OrderItemId { get; set; }
        public string MenuItemName { get; set; } = null!;
        public int Quantity { get; set; }
        public string? Note { get; set; }
        public KdsStatus KdsStatus { get; set; }
    }
}
