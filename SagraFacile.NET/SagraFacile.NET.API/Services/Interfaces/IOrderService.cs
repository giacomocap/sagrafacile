using SagraFacile.NET.API.DTOs;
using SagraFacile.NET.API.Models;
using System.Security.Claims;

namespace SagraFacile.NET.API.Services.Interfaces
{
    public interface IOrderService
    {
        // Existing methods - Keep original signatures where possible to minimize breaking changes
        Task<OrderDto?> CreateOrderAsync(CreateOrderDto orderDto, string cashierId);
        Task<OrderDto?> CreatePreOrderAsync(PreOrderDto preOrderDto);
        Task<OrderDto?> GetOrderByIdAsync(string id);
        Task<IEnumerable<OrderDto>> GetOrdersByAreaAsync(int areaId); // Keep for specific area fetching if needed elsewhere, but new method is primary
        Task<PaginatedResult<OrderDto>> GetOrdersAsync(OrderQueryParameters queryParameters, ClaimsPrincipal user);
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
