using System;

namespace SagraFacile.NET.API.DTOs.Analytics
{
    public class OrderStatusTimelineEventDto
    {
        public string OrderId { get; set; }
        public string DisplayOrderNumber { get; set; }
        public string Status { get; set; }
        public DateTime Timestamp { get; set; }
        public string? PreviousStatus { get; set; }
        public double? DurationInPreviousStatusMinutes { get; set; } // Duration in minutes the order spent in the PreviousStatus
    }
}
