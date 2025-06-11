using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SagraFacile.NET.API.DTOs; // Added for DTOs
using SagraFacile.NET.API.Models;
using SagraFacile.NET.API.Services.Interfaces;
using System; // Added for Exception types
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace SagraFacile.NET.API.Controllers
{
[Route("api/[controller]")]
[ApiController]
[Authorize] // Require authentication for all actions in this controller
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;
    private readonly ILogger<OrdersController> _logger; // Added logger
    private readonly IPrinterService _printerService; // Added IPrinterService

    public OrdersController(IOrderService orderService, ILogger<OrdersController> logger, IPrinterService printerService) // Added IPrinterService
    {
        _orderService = orderService;
        _logger = logger; // Assign logger
        _printerService = printerService; // Assign IPrinterService
    }

// POST: api/Orders
[HttpPost]
[Authorize(Roles = "Cashier, AreaAdmin, Admin, SuperAdmin")] // Define who can create orders
public async Task<ActionResult<OrderDto>> CreateOrder([FromBody] CreateOrderDto orderDto) // Return OrderDto
{
    if (!ModelState.IsValid)
    {
                return BadRequest(ModelState);
            }

            // --- Get Cashier ID from Authenticated User ---
            // In a real scenario, you'd get the user ID from the claims principal
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
            {
                 // This shouldn't happen if [Authorize] is working correctly
                return Unauthorized("Cannot identify cashier.");
            }
            string cashierId = userId;
            // --- End Get Cashier ID ---

            try
            {
                var createdOrderDto = await _orderService.CreateOrderAsync(orderDto, cashierId);

                if (createdOrderDto == null)
                {
                    // This might indicate an internal service error not caught as a specific exception
                    return BadRequest("Failed to create order due to an unexpected issue.");
                }

                // Return the created OrderDto
                return CreatedAtAction(nameof(GetOrder), new { id = createdOrderDto.Id }, createdOrderDto);
            }
            catch (KeyNotFoundException ex)
            {
                // e.g., AreaId or MenuItemId not found
                return NotFound(ex.Message);
            }
            catch (UnauthorizedAccessException) // Removed unused 'ex'
            {
                // e.g., User trying to access area/item from another org
                return Forbid(); // Corrected: No message argument
            }
            catch (InvalidOperationException ex)
            {
                // e.g., Item not in area, missing required note
                return BadRequest(ex.Message);
            }
            catch (Exception ex) // Catch unexpected errors
            {
                // Log the exception ex
                Console.WriteLine($"Unexpected error in CreateOrder: {ex}"); // Replace with proper logging
                return StatusCode(500, "An unexpected error occurred while creating the order.");
            }
        }

         // GET: api/Orders/{id} (e.g., api/Orders/1-1-1713189311000-ABCDEF12)
         [HttpGet("{id}")]
         [Authorize(Roles = "Waiter, Cashier, AreaAdmin, Admin, SuperAdmin")] // Allow Waiter and others to fetch specific orders
         public async Task<ActionResult<OrderDto>> GetOrder(string id) // Changed id to string
         {
             // Service layer now handles the security check (returns null if not found or not accessible)
            var orderDto = await _orderService.GetOrderByIdAsync(id); // Pass string id

            if (orderDto == null)
            {
                // Handles both "not found" and "access denied" scenarios from the user's perspective
                return NotFound($"Order with ID {id} not found or access denied.");
            }

            return Ok(orderDto);
         }

        // GET: api/Orders?areaId=123 (Optional areaId)
        // GET: api/Orders?organizationId=1&areaId=123&statuses=Paid&statuses=PreOrder (All optional)
        [HttpGet]
        [Authorize(Roles = "Waiter, Cashier, AreaAdmin, Admin, SuperAdmin")] // Added Waiter role
        public async Task<ActionResult<IEnumerable<OrderDto>>> GetOrders(
            [FromQuery] int? organizationId,
            [FromQuery] int? areaId,
            // Rename parameter to match the incoming query string format statuses[]=...
            [FromQuery(Name = "statuses[]")] List<OrderStatus>? statuses,
            [FromQuery] int? dayId) // Added dayId parameter
        {
            // organizationId: Optional override for SuperAdmins. Ignored for other roles.
            // areaId: Optional filter within the determined organization context.
            // dayId: Optional filter for a specific operational day. Defaults to current open day if null.
            // statuses: Optional filter for specific order statuses.
            try
            {
                // Service method needs to handle optional orgId, areaId, statuses, and dayId filtering
                var orderDtos = await _orderService.GetOrdersAsync(organizationId, areaId, statuses, dayId, User); // Pass all params + dayId + User context
                return Ok(orderDtos);
            }
            catch (KeyNotFoundException ex) // Could be thrown if orgId/areaId/dayId is provided but not found/accessible
            {
                // Area not found
                return NotFound(ex.Message);
            }
            catch (UnauthorizedAccessException) // Removed unused 'ex'
            {
                // User doesn't have access to the area
                return Forbid(); // Corrected: No message argument
            }
            catch (Exception ex) // Catch unexpected errors
            {
                // Log the exception ex
                _logger.LogError(ex, "Unexpected error in GetOrdersAsync for Org {OrgId}, Area {AreaId}", organizationId, areaId); // Use logger
                return StatusCode(500, "An unexpected error occurred while retrieving orders.");
             }
         }

        // PUT: api/Orders/{orderId}/confirm-preparation
        [HttpPut("{orderId}/confirm-preparation")]
        [Authorize(Roles = "Waiter, AreaAdmin, Admin, SuperAdmin")] // Primarily for Waiter, admins for potential overrides
        public async Task<ActionResult<OrderDto>> ConfirmOrderPreparation(string orderId, [FromBody] ConfirmPreparationDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                // Pass the User (ClaimsPrincipal) to the service method
                var updatedOrderDto = await _orderService.ConfirmOrderPreparationAsync(orderId, dto.TableNumber, User);

                if (updatedOrderDto == null)
                {
                    // Service returns null for various reasons: not found, access denied, invalid status, concurrency conflict
                    // Returning NotFound is generally safe, though BadRequest might be more specific for invalid status if the service threw an exception instead.
                    return NotFound($"Order with ID {orderId} not found, access denied, or cannot be confirmed in its current state.");
                }

                return Ok(updatedOrderDto);
            }
            catch (UnauthorizedAccessException ex) // Catch specific exception from service if thrown
            {
                 _logger.LogWarning(ex, "Unauthorized access attempt in ConfirmOrderPreparation for Order {OrderId}", orderId); // Use logger
                 return Forbid(); // User is authenticated but not authorized for this specific action/order
            }
            // Add catches for KeyNotFoundException or InvalidOperationException if the service is modified to throw them
            catch (Exception ex) // Catch unexpected errors
            {
                _logger.LogError(ex, "Unexpected error in ConfirmOrderPreparation for Order {OrderId}", orderId); // Use logger
                return StatusCode(500, "An unexpected error occurred while confirming the order.");
            }
        }

         // TODO: Add endpoints for updating order status if needed in Phase 1
         // e.g., PUT /api/Orders/{id}/status

        // --- KDS Endpoints ---

        // GET: api/orders/kds-station/{kdsStationId}?includeCompleted=true
        // Note: Route adjusted to fit within OrdersController. Consider moving to KdsStationsController if preferred.
        [HttpGet("kds-station/{kdsStationId}")]
        [Authorize(Roles = "Preparer,AreaAdmin,Admin,SuperAdmin")] // Define roles allowed to view KDS orders
        public async Task<ActionResult<IEnumerable<KdsOrderDto>>> GetActiveOrdersForKds(int kdsStationId, [FromQuery] bool includeCompleted = false) // Added includeCompleted query parameter
        {
            if (kdsStationId <= 0)
            {
                return BadRequest("Valid KDS Station ID is required.");
            }

            try
            {
                // Service handles authorization based on user context and station ownership
                // Pass the includeCompleted parameter to the service method
                var kdsOrders = await _orderService.GetActiveOrdersForKdsStationAsync(kdsStationId, User, includeCompleted);
                return Ok(kdsOrders);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized access attempt in GetActiveOrdersForKds for Station {KdsStationId}", kdsStationId); // Use logger
                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in GetActiveOrdersForKds for Station {KdsStationId}", kdsStationId); // Use logger
                return StatusCode(500, "An unexpected error occurred while retrieving KDS orders.");
            }
        } // Corrected: Added closing brace for GetActiveOrdersForKds method

        // PUT /api/orders/{orderId}/items/{orderItemId}/kds-status
        [HttpPut("{orderId}/items/{orderItemId}/kds-status")]
        [Authorize(Roles = "Waiter,Admin,SuperAdmin")] // Or specific KDS role if created
        public async Task<IActionResult> UpdateOrderItemKdsStatus(string orderId, int orderItemId, [FromBody] UpdateKdsItemStatusDto updateDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                // Corrected property name: KdsStatus instead of NewStatus
                var success = await _orderService.UpdateOrderItemKdsStatusAsync(orderId, orderItemId, updateDto.KdsStatus, User);
                if (success)
                {
                    return NoContent(); // Success, no content to return
                }
                else
                {
                    // Service layer handles logging, return generic not found/bad request?
                    // Returning NotFound might imply the item/order doesn't exist.
                    // Returning BadRequest might imply the status transition is invalid.
                    // Let's stick with NotFound for simplicity if the service returns false.
                    return NotFound($"Failed to update KDS status for item {orderItemId} in order {orderId}.");
                }
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message); // Use Forbid (403) for authorization issues
            }
            catch (Exception ex)
            {
                // Log the exception details server-side
                // Consider logging ex
                _logger.LogError(ex, "Unexpected error in UpdateOrderItemKdsStatus for Order {OrderId}, Item {OrderItemId}", orderId, orderItemId); // Use logger
                return StatusCode(500, "An unexpected error occurred while updating KDS status.");
            }
        }

        // PUT: api/Orders/{orderId}/confirm-payment
        [HttpPut("{orderId}/confirm-payment")]
        [Authorize(Roles = "Cashier, AreaAdmin, Admin, SuperAdmin")] // Define who can confirm pre-order payment
        public async Task<ActionResult<OrderDto>> ConfirmPreOrderPayment(string orderId, [FromBody] ConfirmPreOrderPaymentDto paymentDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var updatedOrderDto = await _orderService.ConfirmPreOrderPaymentAsync(orderId, paymentDto, User);

                // The service now throws exceptions for specific failure cases (NotFound, InvalidOperation, Unauthorized)
                // If it returns null unexpectedly, it's likely an unhandled case or DB issue not caught.
                if (updatedOrderDto == null)
                {
                     _logger.LogError("ConfirmPreOrderPaymentAsync returned null unexpectedly for Order {OrderId}.", orderId);
                     return StatusCode(500, "An unexpected error occurred while confirming the pre-order payment.");
                }

                return Ok(updatedOrderDto);
            }
            catch (KeyNotFoundException ex)
            {
                // Order or MenuItem not found
                return NotFound(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                // e.g., Order not in PreOrder status, missing required note, inconsistent data
                return BadRequest(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                // User doesn't have access to the order's organization
                 _logger.LogWarning(ex, "Unauthorized access attempt in ConfirmPreOrderPayment for Order {OrderId}", orderId);
                return Forbid(); // Return 403 Forbidden
            }
            catch (Exception ex) // Catch unexpected errors
            {
                _logger.LogError(ex, "Unexpected error in ConfirmPreOrderPayment for Order {OrderId}", orderId);
                return StatusCode(500, "An unexpected error occurred while confirming the pre-order payment.");
            }
        }

        // PUT /api/orders/{orderId}/kds-confirm-complete/{kdsStationId}
        [HttpPut("{orderId}/kds-confirm-complete/{kdsStationId}")]
        [Authorize(Roles = "Waiter,Admin,SuperAdmin")] // Roles allowed to confirm KDS completion for a station
        public async Task<IActionResult> ConfirmKdsOrderCompletion(string orderId, int kdsStationId)
        {
             if (kdsStationId <= 0)
             {
                 return BadRequest("Valid KDS Station ID is required.");
             }

            try
            {
                // Pass kdsStationId to the updated service method
                var success = await _orderService.ConfirmKdsOrderCompletionAsync(orderId, kdsStationId, User);
                if (success)
                {
                    // Success means the station confirmation was recorded.
                    // It does NOT necessarily mean the overall order status changed.
                    return NoContent(); // Success, station confirmed
                }
                else
                {
                    // Service returns false if the station confirmation itself failed (e.g., order not found, invalid status, DB error saving station status).
                    // It returns true even if the overall order status didn't change yet.
                    return BadRequest($"Could not record KDS station {kdsStationId} confirmation for order {orderId}. Ensure the order exists and is 'Preparing'.");
                }
            }
            catch (KeyNotFoundException ex) // e.g., KDS Station ID not found or invalid for the order
            {
                return NotFound(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                 _logger.LogWarning(ex, "Unauthorized access attempt in ConfirmKdsOrderCompletion for Order {OrderId}, Station {KdsStationId}", orderId, kdsStationId); // Use logger
                return Forbid(ex.Message);
            }
            catch (Exception ex)
            {
                // Log the exception details server-side
                _logger.LogError(ex, "Unexpected error in ConfirmKdsOrderCompletion for Order {OrderId}, Station {KdsStationId}", orderId, kdsStationId); // Use logger
                return StatusCode(500, "An unexpected error occurred while confirming KDS station completion.");
            }
        }

        // PUT /api/orders/{orderId}/confirm-pickup
        [HttpPut("{orderId}/confirm-pickup")]
        [Authorize(Roles = "Waiter, Cashier, AreaAdmin, Admin, SuperAdmin")] // Define roles allowed to confirm final pickup
        public async Task<ActionResult<OrderDto>> ConfirmOrderPickup(string orderId)
        {
            // No DTO needed as input, just the order ID
            try
            {
                var updatedOrderDto = await _orderService.ConfirmOrderPickupAsync(orderId, User);

                if (updatedOrderDto == null)
                {
                    // Handles not found, access denied, invalid state, concurrency issues from service
                    return NotFound($"Order with ID {orderId} not found, access denied, or cannot be confirmed for pickup in its current state.");
                }

                return Ok(updatedOrderDto);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized access attempt in ConfirmOrderPickup for Order {OrderId}", orderId);
                return Forbid();
            }
            catch (InvalidOperationException ex)
            {
                // Specific workflow state issue
                return BadRequest(ex.Message);
            }
            catch (Exception ex) // Catch unexpected errors
            {
                _logger.LogError(ex, "Unexpected error in ConfirmOrderPickup for Order {OrderId}", orderId);
                return StatusCode(500, "An unexpected error occurred while confirming order pickup.");
            }
        }

        // POST: api/orders/{orderId}/reprint
        [HttpPost("{orderId}/reprint")]
        [Authorize(Roles = "Cashier, AreaAdmin, Admin, SuperAdmin")]
        public async Task<IActionResult> ReprintOrder(string orderId, [FromBody] ReprintRequestDto reprintRequestDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (string.IsNullOrWhiteSpace(orderId))
            {
                return BadRequest("Order ID is required.");
            }

            try
            {
                var (success, error) = await _printerService.ReprintOrderDocumentsAsync(orderId, reprintRequestDto);
                if (success)
                {
                    _logger.LogInformation($"Successfully initiated reprint for Order ID: {orderId}, Type: {reprintRequestDto.ReprintJobType}");
                    return Ok(new { message = $"Reprint for order {orderId} initiated successfully." });
                }
                else
                {
                    _logger.LogWarning($"Reprint failed for Order ID: {orderId}, Type: {reprintRequestDto.ReprintJobType}. Error: {error}");
                    // Specific errors like "Order not found" or "No printer available" are handled by the service returning a message.
                    return BadRequest(new { message = error ?? "Failed to initiate reprint." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Unexpected error during reprint for Order ID: {orderId}");
                return StatusCode(500, "An unexpected error occurred during the reprint process.");
            }
        }
    } // Corrected: Closing brace for OrdersController class
} // Corrected: Closing brace for namespace
