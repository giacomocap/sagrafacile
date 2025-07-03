using SagraFacile.NET.API.Models;
using System;

namespace SagraFacile.NET.API.DTOs
{
    public class OrderStatusBroadcastDto
    {
        public required string OrderId { get; set; }
        public string? DisplayOrderNumber { get; set; } // Human-readable order number
        public OrderStatus NewStatus { get; set; }
        public Guid OrganizationId { get; set; }
        public int AreaId { get; set; }
        public string? CustomerName { get; set; } // For display and announcement
        public string? TableNumber { get; set; } // Optional, for context
        public DateTime StatusChangeTime { get; set; } = DateTime.UtcNow;
    }
}
