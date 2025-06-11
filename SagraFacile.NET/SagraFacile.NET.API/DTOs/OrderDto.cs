using SagraFacile.NET.API.Models; // For OrderStatus enum
using System;
using System.Collections.Generic;

namespace SagraFacile.NET.API.DTOs
{
    public class OrderDto
    {
        public string Id { get; set; } = string.Empty; // Changed to string
        public string? DisplayOrderNumber { get; set; } // Human-readable order number
        public int? DayId { get; set; } // Added DayId (nullable)
        // Removed OrderNumber
        public int AreaId { get; set; }
        public string AreaName { get; set; } = string.Empty; // Include name for display
        public string? CashierId { get; set; } // Nullable for pre-orders
        public string? CashierName { get; set; } // Nullable for pre-orders
        public string? WaiterId { get; set; } // Added WaiterId (nullable)
        public string? WaiterName { get; set; } // Added WaiterName (nullable)
        public DateTime OrderDateTime { get; set; }
        public OrderStatus Status { get; set; }
        public decimal TotalAmount { get; set; }
        public string? PaymentMethod { get; set; }
        public decimal? AmountPaid { get; set; }
        public string? CustomerName { get; set; } // Added for PreOrder
        public string? CustomerEmail { get; set; } // Added for PreOrder
        public string? TableNumber { get; set; } // Added for Waiter Interface
        public string? QrCodeBase64 { get; set; } // Added for QR Code image data

        public int NumberOfGuests { get; set; }
        public bool IsTakeaway { get; set; }

        public List<OrderItemDto> Items { get; set; } = new List<OrderItemDto>();
    }
}
