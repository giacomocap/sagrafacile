using System;
using System.Collections.Generic;

namespace SagraFacile.NET.API.DTOs
{
    /// <summary>
    /// Represents an order as displayed on the KDS main list,
    /// containing only the items relevant to that station.
    /// </summary>
    public class KdsOrderDto
    {
        public string OrderId { get; set; } = null!;
        public string? DisplayOrderNumber { get; set; } // Added DisplayOrderNumber
        public int? DayId { get; set; } // Added DayId (nullable)
        public DateTime OrderDateTime { get; set; }
        public string? TableNumber { get; set; } // Added TableNumber
        public string? CustomerName { get; set; } // Added CustomerName

        // New fields for Coperti (NumberOfGuests) and Asporto (IsTakeaway)
        public int NumberOfGuests { get; set; }
        public bool IsTakeaway { get; set; }

        public List<KdsOrderItemDto> Items { get; set; } = new List<KdsOrderItemDto>();
    }
}
