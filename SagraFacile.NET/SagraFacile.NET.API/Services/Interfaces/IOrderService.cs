using SagraFacile.NET.API.DTOs;
using SagraFacile.NET.API.Models;
using System.Security.Claims;
using System.ComponentModel.DataAnnotations; // Add this for validation attributes

namespace SagraFacile.NET.API.Services.Interfaces
{
    // Define DTOs for input/output if they differ significantly from models
    // MOVED: CreateOrderItemDto to SagraFacile.NET.API.DTOs/CreateOrderItemDto.cs

    // MOVED: CreateOrderDto to SagraFacile.NET.API.DTOs/CreateOrderDto.cs

    // DTO for confirming pre-order payment, potentially with modifications
    public class ConfirmPreOrderPaymentDto
    {
        [Required]
        [MinLength(1, ErrorMessage = "Order must contain at least one item.")]
        public required List<CreateOrderItemDto> Items { get; set; }

        [Required(ErrorMessage = "Payment method is required.")]
        public string PaymentMethod { get; set; } = string.Empty;

        [Range(0, (double)decimal.MaxValue, ErrorMessage = "Amount paid cannot be negative.")]
        public decimal? AmountPaid { get; set; }

        [StringLength(100, ErrorMessage = "Customer name cannot exceed 100 characters.")]
        public string? CustomerName { get; set; } // Made nullable to match frontend flexibility, service validates final state

        [Range(1, int.MaxValue, ErrorMessage = "Number of guests must be at least 1 unless it's takeaway.")] // Adjusted Range for NumberOfGuests
        public int NumberOfGuests { get; set; }

        public bool IsTakeaway { get; set; }

        public int? CashierStationId { get; set; } // Added CashierStationId
    }


    public interface IOrderService
    {
        // Existing methods - Keep original signatures where possible to minimize breaking changes
        Task<OrderDto?> CreateOrderAsync(CreateOrderDto orderDto, string cashierId);
        Task<OrderDto?> CreatePreOrderAsync(PreOrderDto preOrderDto);
        Task<OrderDto?> GetOrderByIdAsync(string id);
        Task<IEnumerable<OrderDto>> GetOrdersByAreaAsync(int areaId); // Keep for specific area fetching if needed elsewhere, but new method is primary
        Task<IEnumerable<OrderDto>> GetOrdersAsync(int? organizationId, int? areaId, List<OrderStatus>? statuses, int? dayId, ClaimsPrincipal user); // Added dayId filter
        Task<IEnumerable<OrderDto>> GetOrdersByStatusAsync(int areaId, OrderStatus status); // New method for public display
        Task<IEnumerable<OrderDto>> GetPublicOrdersByStatusAsync(int areaId, OrderStatus status); // New method for public display without auth context

        // Modified method - Necessary change for authorization
        Task<OrderDto?> ConfirmOrderPreparationAsync(string orderId, string tableNumber, ClaimsPrincipal user); // Changed waiterId to ClaimsPrincipal

        // --- KDS Methods ---
        Task<IEnumerable<KdsOrderDto>> GetActiveOrdersForKdsStationAsync(int kdsStationId, ClaimsPrincipal user, bool includeCompleted = false); // Added includeCompleted parameter
        Task<bool> UpdateOrderItemKdsStatusAsync(string orderId, int orderItemId, KdsStatus newStatus, ClaimsPrincipal user);
        // Modified method to include KDS station ID for targeted item confirmation
        Task<bool> ConfirmKdsOrderCompletionAsync(string orderId, int kdsStationId, ClaimsPrincipal user);

        // --- Pre-Order Confirmation ---
        // Updated to accept DTO with potentially modified items and payment details
        Task<OrderDto?> ConfirmPreOrderPaymentAsync(string orderId, ConfirmPreOrderPaymentDto paymentDto, ClaimsPrincipal user);

        // --- Final Order Completion ---
        Task<OrderDto?> ConfirmOrderPickupAsync(string orderId, ClaimsPrincipal user); // New method for final pickup confirmation
    }
}
