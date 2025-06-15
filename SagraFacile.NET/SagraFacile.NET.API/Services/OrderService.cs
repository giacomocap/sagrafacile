using Microsoft.EntityFrameworkCore;
using SagraFacile.NET.API.Data;
using SagraFacile.NET.API.DTOs; // Added for DTOs
using SagraFacile.NET.API.Models;
using SagraFacile.NET.API.Services.Interfaces;
using System.Security.Claims;
using QRCoder; // Added for QR Code generation
using Microsoft.AspNetCore.SignalR; // Added for SignalR Hub Context
using SagraFacile.NET.API.Hubs; // Added for OrderHub
using SagraFacile.NET.API.Utils; // Added for OrderIdGenerator
using SagraFacile.NET.API.Models.Enums; // Added for PrintJobType

namespace SagraFacile.NET.API.Services
{
    // Inherit from BaseService
    public class OrderService : BaseService, IOrderService
    {
        private readonly ApplicationDbContext _context;
        private readonly IEmailService _emailService; // Added Email Service dependency
        private readonly ILogger<OrderService> _logger; // Added Logger dependency
        private readonly IHubContext<OrderHub> _hubContext; // Added SignalR Hub Context dependency
        private readonly IDayService _dayService; // Added Day Service dependency
        private readonly IPrinterService _printService; // Corrected to IPrinterService
        // IHttpContextAccessor is now inherited from BaseService

        public OrderService(
            ApplicationDbContext context,
            IHttpContextAccessor httpContextAccessor,
            IEmailService emailService, // Injected Email Service
            ILogger<OrderService> logger, // Injected Logger
            IHubContext<OrderHub> hubContext, // Injected SignalR Hub Context
            IDayService dayService, // Injected Day Service
            IPrinterService printService) // Corrected to IPrinterService
            : base(httpContextAccessor) // Call base constructor
        {
            _context = context;
            _emailService = emailService; // Assign injected service
            _logger = logger; // Assign injected logger
            _hubContext = hubContext; // Assign injected hub context
            _dayService = dayService; // Assign injected day service
            _printService = printService; // Assign injected print service
        }

        // GetUserContext and GetUserId helpers are now inherited from BaseService

        // Suppress warning about potential trimming issues with Include/ThenInclude if necessary
        // [RequiresUnreferencedCode("Calls Microsoft.EntityFrameworkCore.RelationalQueryableExtensions.Include")]
        public async Task<OrderDto?> CreateOrderAsync(CreateOrderDto orderDto, string cashierId)
        {
            // --- Validation ---
            // 1. Validate Area FIRST
            var area = await _context.Areas
                                     .FirstOrDefaultAsync(a => a.Id == orderDto.AreaId);

            if (area == null)
            {
                // If area doesn't exist, throw KeyNotFoundException immediately.
                throw new KeyNotFoundException($"Area with ID {orderDto.AreaId} not found.");
            }

            // Area exists, now get user context and perform checks
            var (userOrganizationId, isSuperAdmin) = GetUserContext();
            var authenticatedUserId = GetUserId();

            // Verify the cashierId matches the authenticated user
            if (cashierId != authenticatedUserId)
            {
                // This check is important, but should happen after confirming the area exists
                // to avoid masking the KeyNotFoundException.
                throw new UnauthorizedAccessException("Cashier ID does not match authenticated user.");
            }

            // Check organization access for the existing area
            if (!isSuperAdmin && area.OrganizationId != userOrganizationId)
            {
                throw new UnauthorizedAccessException($"Access denied to create order in Area ID {orderDto.AreaId}.");
            }

            // 3. Validate Customer Name (Mandatory for Cashier Orders) - Moved before item validation
            if (string.IsNullOrWhiteSpace(orderDto.CustomerName))
            {
                throw new ArgumentException("Customer name is required for cashier orders.", nameof(orderDto.CustomerName));
            }

            // 4. Validate Items, Calculate Total, and check organization access via category/area - Moved before Day check
            decimal totalAmount = 0;
            var orderItems = new List<OrderItem>();

            // Preload relevant menu items and their categories/areas for efficiency and validation
            var menuItemIds = orderDto.Items.Select(i => i.MenuItemId).Distinct().ToList();
            var menuItems = await _context.MenuItems
                                          .Include(mi => mi.MenuCategory)
                                            .ThenInclude(mc => mc!.Area) // Need Area for org check (use ! to assure compiler it's not null after Include)
                                          .Where(mi => menuItemIds.Contains(mi.Id))
                                          .ToDictionaryAsync(mi => mi.Id);

            // Preload the cashier user to get their name for the DTO
            var cashier = await _context.Users.FindAsync(cashierId);
            if (cashier == null)
            {
                // This shouldn't happen if cashierId comes from authenticated user, but good practice to check
                throw new KeyNotFoundException($"Cashier with ID {cashierId} not found.");
            }


            foreach (var itemDto in orderDto.Items)
            {
                if (itemDto.Quantity <= 0) continue;

                if (!menuItems.TryGetValue(itemDto.MenuItemId, out var menuItem))
                {
                    // Item validation happens BEFORE day check now
                    throw new KeyNotFoundException($"MenuItem with ID {itemDto.MenuItemId} not found.");
                }

                // Check if item belongs to the correct area AND organization
                if (menuItem.MenuCategory == null || menuItem.MenuCategory.AreaId != orderDto.AreaId)
                {
                    throw new InvalidOperationException($"MenuItem ID {menuItem.Id} does not belong to the specified Area ID {orderDto.AreaId}.");
                }
                // Organization check is implicitly handled by the Area check above, but double-check for safety
                if (!isSuperAdmin && menuItem.MenuCategory.Area?.OrganizationId != userOrganizationId)
                {
                    throw new UnauthorizedAccessException($"Access denied to MenuItem ID {menuItem.Id} belonging to another organization.");
                }

                // Check if note is required but missing
                if (menuItem.IsNoteRequired && string.IsNullOrWhiteSpace(itemDto.Note))
                {
                    throw new InvalidOperationException($"Required note is missing for MenuItem ID {menuItem.Id}.");
                }

                // --- Stock Check (Scorta) ---
                if (menuItem.Scorta.HasValue)
                {
                    if (menuItem.Scorta.Value < itemDto.Quantity)
                    {
                        throw new InvalidOperationException($"Item '{menuItem.Name}' (ID: {menuItem.Id}) is out of stock or insufficient quantity available. Requested: {itemDto.Quantity}, Available: {menuItem.Scorta.Value}.");
                    }
                }
                // --- End Stock Check ---

                var orderItem = new OrderItem
                {
                    MenuItemId = menuItem.Id,
                    Quantity = itemDto.Quantity,
                    UnitPrice = menuItem.Price,
                    Note = itemDto.Note
                };
                orderItems.Add(orderItem);
                totalAmount += orderItem.Quantity * orderItem.UnitPrice;
            }

            if (!orderItems.Any())
            {
                throw new InvalidOperationException("Cannot create an order with no valid items.");
            }

            // Add guest and takeaway charges
            if (orderDto.IsTakeaway && area.TakeawayCharge > 0)
            {
                totalAmount += area.TakeawayCharge;
            }
            else if (!orderDto.IsTakeaway && area.GuestCharge > 0 && orderDto.NumberOfGuests > 0)
            {
                totalAmount += orderDto.NumberOfGuests * area.GuestCharge;
            }

            // 5. Check for Open Day - Moved AFTER item validation
            var currentOpenDay = await _dayService.GetCurrentOpenDayAsync(area.OrganizationId);
            if (currentOpenDay == null)
            {
                _logger.LogWarning("Attempted to create order in organization {OrganizationId} but no Day is open.", area.OrganizationId);
                throw new InvalidOperationException("Cannot create order: No operational day (Giornata) is currently open.");
            }

            // --- Determine Next Status based on Workflow Flags ---
            OrderStatus nextStatus;
            string? waiterId = null; // To store waiter ID if table order is confirmed

            // Check for table order with implicit waiter confirmation
            if (!string.IsNullOrWhiteSpace(orderDto.TableNumber) && area.EnableWaiterConfirmation)
            {
                // This is a table order, acting as an implicit waiter confirmation.
                // Skip 'Paid' status.
                waiterId = cashierId; // The user creating the order is the waiter

                if (area.EnableKds)
                {
                    nextStatus = OrderStatus.Preparing;
                }
                else if (area.EnableCompletionConfirmation)
                {
                    nextStatus = OrderStatus.ReadyForPickup;
                }
                else
                {
                    nextStatus = OrderStatus.Completed;
                }
            }
            else
            {
                // Standard order creation logic
                if (area.EnableWaiterConfirmation)
                {
                    nextStatus = OrderStatus.Paid;
                }
                else if (area.EnableKds)
                {
                    nextStatus = OrderStatus.Preparing;
                }
                else if (area.EnableCompletionConfirmation)
                {
                    nextStatus = OrderStatus.ReadyForPickup;
                }
                else
                {
                    nextStatus = OrderStatus.Completed;
                }
            }

            // --- Order Creation ---
            var order = new Order
            {
                OrganizationId = area.OrganizationId, // OrganizationId from the validated Area
                AreaId = area.Id,
                CashierId = cashierId, // Use the validated cashierId (which is the userId)
                WaiterId = waiterId, // Set if this is a table order with implicit confirmation
                Id = Guid.NewGuid().ToString(), // Use GUID for internal ID
                Status = nextStatus,
                OrderDateTime = DateTime.UtcNow,
                TotalAmount = totalAmount,
                PaymentMethod = orderDto.PaymentMethod,
                AmountPaid = orderDto.AmountPaid,
                CustomerName = orderDto.CustomerName.Trim(), // Add CustomerName from DTO
                DayId = currentOpenDay.Id, // Associate with the current open Day
                NumberOfGuests = orderDto.NumberOfGuests, // Added NumberOfGuests
                IsTakeaway = orderDto.IsTakeaway,       // Added IsTakeaway
                TableNumber = orderDto.TableNumber,     // Added TableNumber
                OrderItems = orderItems
            };

            // Transactions are not supported by the InMemory provider used in tests
            bool useTransaction = !_context.Database.ProviderName.Contains("InMemory");
            Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction = null;

            if (useTransaction)
            {
                transaction = await _context.Database.BeginTransactionAsync();
            }

            try // Corrected structure
            {
                // --- Generate DisplayOrderNumber ---
                string displayOrderNumberPrefix = area.Slug.ToUpperInvariant();
                displayOrderNumberPrefix = new string(displayOrderNumberPrefix.Where(char.IsLetterOrDigit).ToArray());
                if (displayOrderNumberPrefix.Length > 3)
                {
                    displayOrderNumberPrefix = displayOrderNumberPrefix.Substring(0, 3);
                }
                else if (string.IsNullOrEmpty(displayOrderNumberPrefix))
                {
                    displayOrderNumberPrefix = "ORD"; // Fallback prefix
                }

                var sequence = await _context.AreaDayOrderSequences
                    .FirstOrDefaultAsync(s => s.AreaId == area.Id && s.DayId == currentOpenDay.Id);

                if (sequence == null)
                {
                    sequence = new AreaDayOrderSequence
                    {
                        AreaId = area.Id,
                        DayId = currentOpenDay.Id,
                        LastSequenceNumber = 0
                    };
                    _context.AreaDayOrderSequences.Add(sequence);
                    // SaveChangesAsync will be called below, or EF will track this new entity
                }

                sequence.LastSequenceNumber++;
                order.DisplayOrderNumber = $"{displayOrderNumberPrefix}-{sequence.LastSequenceNumber:D3}";
                // --- End Generate DisplayOrderNumber ---

                // --- Stock Decrement (Scorta) ---
                foreach (var oi in order.OrderItems)
                {
                    if (menuItems.TryGetValue(oi.MenuItemId, out var mi) && mi.Scorta.HasValue)
                    {
                        mi.Scorta -= oi.Quantity;
                        // EF Core will track changes to mi if it's tracked from the menuItems dictionary load
                        _context.MenuItems.Update(mi); 
                    }
                }
                // --- End Stock Decrement ---

                _context.Orders.Add(order);
                await _context.SaveChangesAsync(); // This will save the order, sequence, and stock updates

                if (useTransaction && transaction != null)
                { 
                    await transaction.CommitAsync();
                } 

                // --- SignalR Broadcast for Stock Updates ---
                foreach (var oi in order.OrderItems)
                {
                    if (menuItems.TryGetValue(oi.MenuItemId, out var mi) && mi.Scorta.HasValue) // Check if Scorta was involved
                    {
                        var stockUpdateDto = new StockUpdateBroadcastDto
                        {
                            MenuItemId = mi.Id,
                            AreaId = area.Id, // area is already loaded
                            NewScorta = mi.Scorta, // The new, decremented value
                            Timestamp = DateTime.UtcNow
                        };
                        await _hubContext.Clients.Group($"Area-{area.Id}").SendAsync("ReceiveStockUpdate", stockUpdateDto);
                        _logger.LogInformation("Broadcasted stock update for MenuItem {MenuItemId} in Area {AreaId}, NewScorta: {NewScorta}", mi.Id, area.Id, mi.Scorta);
                    }
                }
                // --- End SignalR Broadcast ---

                // --- Printing ---
                try
                {
                    _logger.LogInformation("Attempting to print receipt for Order ID {OrderId} after creation.", order.Id);
                    await _printService.PrintOrderDocumentsAsync(order, PrintJobType.Receipt);

                    // Comanda Printing Logic for new Cashier Order:
                    // 'area' is already loaded. We might need to load CashierStation if order.CashierStationId is set.
                    bool comandaPrinted = false;
                    if (order.CashierStationId.HasValue)
                    {
                        var station = await _context.CashierStations.FindAsync(order.CashierStationId.Value);
                        if (station?.PrintComandasAtThisStation == true)
                        {
                            _logger.LogInformation("Printing comanda at station {StationId} for new Order ID {OrderId}.", station.Id, order.Id);
                            await _printService.PrintOrderDocumentsAsync(order, PrintJobType.Comanda);
                            comandaPrinted = true;
                        }
                    }

                    if (!comandaPrinted && area.PrintComandasAtCashier)
                    {
                        _logger.LogInformation("Printing comanda at area default for new Order ID {OrderId}.", order.Id);
                        await _printService.PrintOrderDocumentsAsync(order, PrintJobType.Comanda);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during printing for new Order ID {OrderId}. Order creation itself was successful. Printing failed.", order.Id);
                    // Do not rethrow, allow order creation to be considered successful.
                }
                // --- End Printing ---

                // --- Deferred Comanda Printing for Mobile/Table Orders ---
                // If the order was a mobile table order that skipped the 'Paid' status,
                // we need to trigger the deferred comanda printing logic now.
                if (!string.IsNullOrWhiteSpace(order.TableNumber) && order.WaiterId != null)
                {
                    try
                    {
                        _logger.LogInformation("Order {OrderId} is a mobile table order, triggering deferred comanda printing.", order.Id);
                        await TriggerDeferredComandaPrintingAsync(order);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error during deferred comanda printing for new mobile Order ID {OrderId}. Order creation itself was successful.", order.Id);
                        // Do not rethrow, as the primary operation (order creation) succeeded.
                    }
                }
                // --- End Deferred Comanda Printing ---

                // Map the created Order entity to OrderDto
                // Use null-coalescing for potentially null names
                string cashierFullName = $"{cashier?.FirstName ?? ""} {cashier?.LastName ?? ""}".Trim();
                // Encode only the Order ID for scanning by the Waiter app
                string qrCodeBase64 = GenerateQrCodeBase64(order.Id); // QR code should still use internal ID for lookups
                return MapOrderToDto(order, area.Name, cashierFullName, menuItems, qrCodeBase64); // Pass QR code
            }
            catch (Exception ex)
            {
                if (useTransaction && transaction != null)
                {
                    await transaction.RollbackAsync();
                }
                _logger.LogError(ex, "Exception during CreateOrderAsync for Area {AreaId}", orderDto.AreaId); // Use logger
                // Consider re-throwing or returning a specific error result instead of null
                return null;
            }
            finally // Ensure transaction is disposed even if commit/rollback fails
            {
                if (transaction != null)
                {
                    await transaction.DisposeAsync();
                }
            }
        }

        // New method for public pre-orders
        public async Task<OrderDto?> CreatePreOrderAsync(PreOrderDto preOrderDto)
        {
            // --- Validation ---
            // 1. Validate Organization and Area existence
            var area = await _context.Areas
                                     .Include(a => a.Organization) // Include Organization for validation
                                     .FirstOrDefaultAsync(a => a.Id == preOrderDto.AreaId);

            if (area == null)
            {
                throw new KeyNotFoundException($"Area with ID {preOrderDto.AreaId} not found.");
            }
            // Removed invalid OrganizationId check against PreOrderDto
            // if (area.OrganizationId != preOrderDto.OrganizationId || area.Organization == null)
            // {
            //     throw new InvalidOperationException($"Area ID {preOrderDto.AreaId} does not belong to Organization ID {preOrderDto.OrganizationId}.");
            // }

            // 2. Validate Items, Calculate Total
            decimal totalAmount = 0;
            var orderItems = new List<OrderItem>();

            // Preload relevant menu items and their categories for efficiency and validation
            var menuItemIds = preOrderDto.Items.Select(i => i.MenuItemId).Distinct().ToList();
            var menuItems = await _context.MenuItems
                                          .Include(mi => mi.MenuCategory) // Need MenuCategory for area check
                                          .Where(mi => menuItemIds.Contains(mi.Id))
                                          .ToDictionaryAsync(mi => mi.Id);

            foreach (var itemDto in preOrderDto.Items)
            {
                if (itemDto.Quantity <= 0) continue;

                if (!menuItems.TryGetValue(itemDto.MenuItemId, out var menuItem))
                {
                    throw new KeyNotFoundException($"MenuItem with ID {itemDto.MenuItemId} not found.");
                }

                // Check if item belongs to the correct area
                if (menuItem.MenuCategory == null || menuItem.MenuCategory.AreaId != preOrderDto.AreaId)
                {
                    throw new InvalidOperationException($"MenuItem ID {menuItem.Id} does not belong to the specified Area ID {preOrderDto.AreaId}.");
                }

                // Check if note is required but missing
                if (menuItem.IsNoteRequired && string.IsNullOrWhiteSpace(itemDto.Note))
                {
                    throw new InvalidOperationException($"Required note is missing for MenuItem ID {menuItem.Id}.");
                }

                var orderItem = new OrderItem
                {
                    MenuItemId = menuItem.Id,
                    Quantity = itemDto.Quantity,
                    UnitPrice = menuItem.Price, // Use current price
                    Note = itemDto.Note
                };
                orderItems.Add(orderItem);
                totalAmount += orderItem.Quantity * orderItem.UnitPrice;
            }

            if (!orderItems.Any())
            {
                throw new InvalidOperationException("Cannot create a pre-order with no valid items.");
            }

            // Add guest and takeaway charges
            if (preOrderDto.IsTakeaway && area.TakeawayCharge > 0)
            {
                totalAmount += area.TakeawayCharge;
            }
            else if (!preOrderDto.IsTakeaway && area.GuestCharge > 0 && preOrderDto.NumberOfGuests > 0)
            {
                totalAmount += preOrderDto.NumberOfGuests * area.GuestCharge;
            }

            // --- Pre-Order Creation ---
            var order = new Order
            {
                OrganizationId = area.OrganizationId,
                AreaId = area.Id,
                // CashierId is null for pre-orders initially
                Id = Guid.NewGuid().ToString(), // Use GUID for internal ID
                Status = OrderStatus.PreOrder, // Initial status for pre-orders
                OrderDateTime = DateTime.UtcNow,
                TotalAmount = totalAmount,
                CustomerName = preOrderDto.CustomerName.Trim(),
                CustomerEmail = preOrderDto.CustomerEmail?.Trim(),
                NumberOfGuests = preOrderDto.NumberOfGuests, // Added NumberOfGuests
                IsTakeaway = preOrderDto.IsTakeaway,       // Added IsTakeaway
                // DayId will be assigned upon confirmation/payment if a Day is open then
                OrderItems = orderItems
            };

            // Preload Area and Cashier (if any) for mapping to DTO
            // For PreOrder, CashierId is null, so CashierName will also be null.
            // The menu item names are already in the menuItems dictionary.
            // The area name is from the validated 'area' object.

            // For pre-orders, the QR code should contain information to allow easy lookup and confirmation
            // Using the Order ID for the QR code is consistent.
            string qrCodeForEmail = GenerateQrCodeBase64(order.Id); // QR code should still use internal ID

            // DisplayOrderNumber is NOT generated here for PreOrder.
            // It will be generated when the PreOrder is confirmed and paid,
            // associating it with an active Day.

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            // Send confirmation email
            try
            {
                //await _emailService.SendPreOrderConfirmationEmailAsync(order, area.Name, qrCodeForEmail);
            }
            catch (Exception ex)
            {
                // Log the email sending failure but don't let it fail the order creation
                _logger.LogError(ex, "Failed to send pre-order confirmation email for Order ID {OrderId}", order.Id);
            }

            // Map to OrderDto for the response
            // Pass the menuItems dictionary (which contains MenuItem Name and Price)
            return MapOrderToDto(order, area.Name, null, menuItems, qrCodeForEmail); // Pass null for cashierName, and QR code
        }


        // Suppress warning about potential trimming issues with Include/ThenInclude if necessary
        // [RequiresUnreferencedCode("Calls Microsoft.EntityFrameworkCore.RelationalQueryableExtensions.Include")]
        public async Task<OrderDto?> GetOrderByIdAsync(string id) // Kept original signature
        {
            // Authorization implemented inside
            var (userOrganizationId, isSuperAdmin) = GetUserContext();
            // We don't need the userId for this specific method

            // Include necessary data for DTO mapping
            var order = await _context.Orders
                                 .Include(o => o.Area) // Need Area for name and organization check
                                 .Include(o => o.Cashier) // Need Cashier for name
                                 .Include(o => o.Waiter) // Need Waiter for name
                                 .Include(o => o.OrderItems)
                                     .ThenInclude(oi => oi.MenuItem) // Need MenuItem for name
                                 .AsNoTracking() // Use AsNoTracking for read-only operations
                                 .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
            {
                order = await _context.Orders
             .Include(o => o.Area) // Need Area for name and organization check
             .Include(o => o.Cashier) // Need Cashier for name
             .Include(o => o.Waiter) // Need Waiter for name
             .Include(o => o.OrderItems)
                 .ThenInclude(oi => oi.MenuItem) // Need MenuItem for name
             .AsNoTracking() // Use AsNoTracking for read-only operations
             .FirstOrDefaultAsync(o => o.PreOrderPlatformId == id);
            }

            if (order == null)
            {
                return null; // Not found
            }

            // Check organization access via the included Area
            if (order.Area == null) // Should not happen if FK is enforced, but check defensively
            {
                _logger.LogWarning("Order ID {OrderId} has null Area.", id); // Use logger
                return null; // Treat as inaccessible or invalid data
            }
            if (!isSuperAdmin && order.Area.OrganizationId != userOrganizationId)
            {
                // Don't throw UnauthorizedAccessException here, just return null as if not found
                return null;
            }

            // --- Day Check for non-SuperAdmins ---
            // Allow fetching PreOrders even if no day is open or they don't match the current day.
            // The Day association happens during confirmation.
            if (!isSuperAdmin && order.Status != OrderStatus.PreOrder)
            {
                var currentOpenDay = await _dayService.GetCurrentOpenDayAsync(order.Area.OrganizationId);
                if (currentOpenDay == null || order.DayId != currentOpenDay.Id)
                {
                    // If no day is open, or the order doesn't belong to the current open day,
                    // non-SuperAdmins cannot access it via this method (unless it's a PreOrder).
                    _logger.LogWarning("Non-SuperAdmin user {UserId} denied access to Order {OrderId} (Status: {Status}) because it does not belong to the current open Day ({CurrentDayId}).", GetUserId(), id, order.Status, currentOpenDay?.Id);
                    return null;
                }
            }
            // --- End Day Check ---

            // Map Order to OrderDto
            // Need to fetch menu item names separately if not included above, or adjust includes
            // For simplicity, assuming OrderItems.MenuItem is included as above.
            var menuItemNames = order.OrderItems.ToDictionary(oi => oi.MenuItemId, oi => oi.MenuItem?.Name ?? "Unknown Item");
            string areaName = order.Area?.Name ?? "Unknown Area"; // Handle potentially null Area Name
            string cashierFullName = $"{order.Cashier?.FirstName ?? ""} {order.Cashier?.LastName ?? ""}".Trim(); // Handle potentially null Cashier names

            return MapOrderToDto(order, areaName, cashierFullName, menuItemNames);
        }

        // Suppress warning about potential trimming issues with Include/ThenInclude if necessary
        // [RequiresUnreferencedCode("Calls Microsoft.EntityFrameworkCore.RelationalQueryableExtensions.Include")]
        public async Task<IEnumerable<OrderDto>> GetOrdersByAreaAsync(int areaId) // Kept original signature
        {
            // Authorization implemented inside
            var (userOrganizationId, isSuperAdmin) = GetUserContext();
            // We don't need the userId for this specific method

            // Verify user has access to the requested area
            var area = await _context.Areas.FindAsync(areaId);
            if (area == null)
            {
                throw new KeyNotFoundException($"Area with ID {areaId} not found.");
            }
            if (!isSuperAdmin && area.OrganizationId != userOrganizationId)
            {
                throw new UnauthorizedAccessException($"Access denied to orders for Area ID {areaId}.");
            }

            // --- Day Filtering ---
            var currentOpenDay = await _dayService.GetCurrentOpenDayAsync(area.OrganizationId);
            if (currentOpenDay == null)
            {
                _logger.LogInformation("GetOrdersByAreaAsync: No open Day for Organization {OrganizationId}. Returning empty list.", area.OrganizationId);
                return Enumerable.Empty<OrderDto>(); // No open day, return empty list
            }
            // --- End Day Filtering ---

            // User has access, get the orders and project to DTOs
            var orders = await _context.Orders
                                 .Where(o => o.AreaId == areaId && o.DayId == currentOpenDay.Id) // Filter by Area AND current open Day
                                 .Include(o => o.Area) // Needed for AreaName
                                 .Include(o => o.Cashier) // Needed for CashierName
                                 .Include(o => o.Waiter) // Needed for WaiterName
                                 .Include(o => o.OrderItems) // Needed for Items list in DTO
                                    .ThenInclude(oi => oi.MenuItem) // Needed for MenuItemName in OrderItemDto
                                 .OrderByDescending(o => o.OrderDateTime)
                                 .AsNoTracking() // Use AsNoTracking for read-only list
                                 .ToListAsync();

            // Map List<Order> to List<OrderDto>
            return orders.Select(order =>
            {
                // Extract menu item names for this order's items
                var menuItemNames = order.OrderItems.ToDictionary(oi => oi.MenuItemId, oi => oi.MenuItem?.Name ?? "Unknown Item");
                return MapOrderToDto(order, order.Area?.Name ?? "Unknown Area", $"{order.Cashier?.FirstName} {order.Cashier?.LastName}", menuItemNames);
            }).ToList(); // Use ToList() to execute the projection
        }

        // New method for flexible order filtering - Updated signature with dayId
        public async Task<IEnumerable<OrderDto>> GetOrdersAsync(int? organizationId, int? areaId, List<OrderStatus>? statuses, int? dayId, ClaimsPrincipal user)
        {
            var (userOrganizationId, isSuperAdmin) = GetUserContext();
            int targetOrganizationId;

            // Determine the target organization ID
            if (isSuperAdmin)
            {
                if (!organizationId.HasValue)
                {
                    // SuperAdmin must provide an organizationId to filter by, otherwise it's ambiguous
                    // Alternatively, could return all orders across all orgs, but that seems risky/unintended.
                    // Let's require SuperAdmins to specify an org for this admin view.
                    throw new ArgumentException("SuperAdmin must specify an organizationId to view orders.", nameof(organizationId));
                }
                // SuperAdmin can view the specified organization (no access check needed here, assuming valid ID)
                targetOrganizationId = organizationId.Value;
            }
            else
            {
                // Non-SuperAdmin: Ignore provided organizationId, use their own context
                if (!userOrganizationId.HasValue)
                {
                    // Should not happen for Admin/AreaAdmin roles if claims are set correctly
                    throw new UnauthorizedAccessException("User organization context could not be determined.");
                }
                targetOrganizationId = userOrganizationId.Value;

                // If an organizationId was provided but doesn't match the user's context, deny access
                if (organizationId.HasValue && organizationId.Value != targetOrganizationId)
                {
                    throw new UnauthorizedAccessException($"Access denied to view orders for organization ID {organizationId.Value}.");
                }
            }

            // --- Area Filter Validation (Moved Before Day Filter) ---
            // If an areaId is provided, validate it belongs to the target organization *before* proceeding.
            if (areaId.HasValue)
            {
                var areaExistsInOrg = await _context.Areas
                                                    .AnyAsync(a => a.Id == areaId.Value && a.OrganizationId == targetOrganizationId);
                if (!areaExistsInOrg)
                {
                    // Throw KeyNotFound as the area doesn't exist *within the accessible org context*.
                    // This ensures tests checking for non-existent/inaccessible areas fail correctly.
                    throw new KeyNotFoundException($"Area with ID {areaId.Value} not found within organization ID {targetOrganizationId}.");
                }
                _logger.LogInformation("Validated Area {AreaId} exists within Organization {OrganizationId}.", areaId.Value, targetOrganizationId);
            }
            // --- End Area Filter Validation ---

            // --- Determine Day Filter (NEW LOGIC) ---
            int? filterDayId = null; // Variable to hold the Day ID to filter by

            if (dayId.HasValue)
            {
                // Specific day requested: Validate role and day existence
                bool isAdminOrSuperAdmin = user.IsInRole("Admin") || user.IsInRole("SuperAdmin");
                if (!isAdminOrSuperAdmin)
                {
                    _logger.LogWarning("User {UserId} without Admin/SuperAdmin role attempted to access historical orders for Day {DayId}.", GetUserId(), dayId.Value);
                    throw new UnauthorizedAccessException("Access denied to view historical orders. Admin or SuperAdmin role required.");
                }

                var dayExists = await _context.Days.AnyAsync(d => d.Id == dayId.Value && d.OrganizationId == targetOrganizationId);
                if (!dayExists)
                {
                    throw new KeyNotFoundException($"Day with ID {dayId.Value} not found within organization ID {targetOrganizationId}.");
                }
                filterDayId = dayId.Value;
                _logger.LogInformation("Filtering orders for Organization {OrganizationId} by specified Day {DayId} (Admin/SuperAdmin access).", targetOrganizationId, filterDayId);
            }
            else
            {
                // No specific day requested: Default to current open day for ALL roles
                var currentOpenDay = await _dayService.GetCurrentOpenDayAsync(targetOrganizationId);
                if (currentOpenDay != null)
                {
                    filterDayId = currentOpenDay.Id;
                    _logger.LogInformation("Filtering orders for Organization {OrganizationId} by current open Day {DayId} (default for all roles).", targetOrganizationId, filterDayId);
                }
                else
                {
                    // No day open, return empty list for everyone
                    _logger.LogInformation("No Day currently open for Organization {OrganizationId}. Returning empty list for GetOrdersAsync.", targetOrganizationId);
                    return Enumerable.Empty<OrderDto>();
                }
            }
            // --- End Determine Day Filter ---

            // Build the query
            var query = _context.Orders
                                .Where(o => o.OrganizationId == targetOrganizationId)
                                .Include(o => o.Area)       // Needed for AreaName
                                .Include(o => o.Cashier)    // Needed for CashierName
                                .Include(o => o.Waiter)     // Needed for WaiterName
                                .Include(o => o.OrderItems) // Needed for Items list in DTO
                                    .ThenInclude(oi => oi.MenuItem) // Needed for MenuItemName in OrderItemDto
                                .OrderByDescending(o => o.OrderDateTime)
                                .AsNoTracking();

            // Apply Day filter (filterDayId will always have a value here unless an empty list was returned above)
            if (filterDayId.HasValue) // Should always be true if we didn't return early
            {
                _logger.LogInformation("Applying Day filter: {DayId}", filterDayId.Value);
                query = query.Where(o => o.DayId == filterDayId.Value);
            }
            else
            {
                // This case should technically not be reachable due to the logic above,
                // but log a warning if it somehow is.
                _logger.LogWarning("GetOrdersAsync reached query execution without a filterDayId being set or returning early. This indicates a logic error.");
                // Depending on desired behavior, could return empty or proceed without day filter.
                // Returning empty is safer based on the requirements.
                return Enumerable.Empty<OrderDto>();
            }

            // Apply optional Statuses filter
            if (statuses != null && statuses.Any())
            {
                // Ensure the list contains valid enum values if necessary, though EF Core should handle it.
                query = query.Where(o => statuses.Contains(o.Status));
            }

            // Apply optional Area filter (Validation already done above)
            if (areaId.HasValue)
            {
                query = query.Where(o => o.AreaId == areaId.Value);
            }

            // Execute the query
            var orders = await query.ToListAsync();

            // Map List<Order> to List<OrderDto>
            return orders.Select(order =>
            {
                var menuItemNames = order.OrderItems.ToDictionary(oi => oi.MenuItemId, oi => oi.MenuItem?.Name ?? "Unknown Item");
                return MapOrderToDto(order, order.Area?.Name ?? "Unknown Area", $"{order.Cashier?.FirstName} {order.Cashier?.LastName}", menuItemNames);
            }).ToList();
        }


        // --- Helper Methods ---

        private async Task TriggerDeferredComandaPrintingAsync(Order order)
        {
            // This logic is for printing comandas that were deferred because the initial
            // order status skipped straight to 'Preparing' or beyond, bypassing the
            // typical cashier printing step. This is common for waiter confirmations
            // or mobile table orders.

            // We need the Area for workflow flags. The order passed in should have it included.
            var areaForWorkflow = order.Area ?? await _context.Areas.FindAsync(order.AreaId);
            if (areaForWorkflow == null)
            {
                _logger.LogWarning("TriggerDeferredComandaPrintingAsync: Area information for Order {OrderId} could not be loaded. Cannot determine comanda printing logic.", order.Id);
                return;
            }

            // First, determine if a comanda would have already been printed at the cashier station
            // when the order was initially created or paid for.
            bool comandaAlreadyPrinted = false;
            if (order.CashierStationId.HasValue)
            {
                var station = await _context.CashierStations.AsNoTracking().FirstOrDefaultAsync(cs => cs.Id == order.CashierStationId.Value);
                if (station?.PrintComandasAtThisStation == true)
                {
                    comandaAlreadyPrinted = true;
                    _logger.LogInformation("Deferred Comanda Check for Order {OrderId}: Comanda was likely already printed at CashierStation {StationId}.", order.Id, station.Id);
                }
            }
            else if (areaForWorkflow.PrintComandasAtCashier)
            {
                // This case applies if the order was NOT associated with a specific station,
                // but the area is configured to print all comandas at the cashier by default.
                comandaAlreadyPrinted = true;
                _logger.LogInformation("Deferred Comanda Check for Order {OrderId}: Comanda was likely already printed by default for Area {AreaId}.", order.Id, areaForWorkflow.Id);
            }

            // If the comanda was not already printed at the cashier, print it now to the category printers.
            if (!comandaAlreadyPrinted)
            {
                // This condition ensures we print only when it's appropriate.
                // e.g., when the order enters a state where items need to be prepared.
                bool shouldPrintComandaNow = order.Status == OrderStatus.Preparing ||
                                             (!areaForWorkflow.EnableKds && (order.Status == OrderStatus.ReadyForPickup || order.Status == OrderStatus.Completed));

                if (shouldPrintComandaNow)
                {
                    _logger.LogInformation("Attempting to print comanda via _printService for Order ID {OrderId} as part of deferred printing logic. Order Status: {OrderStatus}.", order.Id, order.Status);
                    await _printService.PrintOrderDocumentsAsync(order, PrintJobType.Comanda);
                    _logger.LogInformation("Call to _printService for deferred comanda printing completed for Order ID {OrderId}.", order.Id);
                }
                else
                {
                    _logger.LogInformation("Deferred comanda for Order {OrderId} will NOT be printed now as shouldPrintComandaNow is false. Order Status: {OrderStatus}, Area.EnableKds: {EnableKds}",
                                           order.Id, order.Status, areaForWorkflow.EnableKds);
                }
            }
            else
            {
                _logger.LogInformation("Skipping deferred comanda print for Order ID {OrderId} because it was likely already printed at the cashier.", order.Id);
            }
        }

        // Helper to map Order entity to OrderDto
        private OrderDto MapOrderToDto(Order order, string areaName, string? cashierName, IDictionary<int, string> menuItemNames, string? qrCodeBase64 = null) // Added qrCodeBase64 param, made cashierName nullable
        {
            return new OrderDto
            {
                Id = order.Id,
                DisplayOrderNumber = order.DisplayOrderNumber, // Added DisplayOrderNumber
                DayId = order.DayId,
                AreaId = order.AreaId,
                AreaName = areaName,
                CashierId = order.CashierId,
                CashierName = cashierName,
                WaiterId = order.WaiterId,
                OrderDateTime = order.OrderDateTime,
                Status = order.Status,
                TotalAmount = order.TotalAmount,
                PaymentMethod = order.PaymentMethod,
                AmountPaid = order.AmountPaid,
                CustomerName = order.CustomerName,
                CustomerEmail = order.CustomerEmail,
                TableNumber = order.TableNumber,
                QrCodeBase64 = qrCodeBase64,
                NumberOfGuests = order.NumberOfGuests,
                IsTakeaway = order.IsTakeaway,
                Items = order.OrderItems.Select(oi => new OrderItemDto
                {
                    MenuItemId = oi.MenuItemId,
                    MenuItemName = menuItemNames.TryGetValue(oi.MenuItemId, out var name) ? name : "Unknown Item",
                    Quantity = oi.Quantity,
                    UnitPrice = oi.UnitPrice,
                    Note = oi.Note
                }).ToList()
            };
        }

        // Overload for mapping when full MenuItem objects are available (e.g., when creating an order)
        private OrderDto MapOrderToDto(Order order, string areaName, string? cashierName, IDictionary<int, MenuItem> menuItems, string? qrCodeBase64 = null) // Added qrCodeBase64 param, made cashierName nullable
        {
            return new OrderDto
            {
                Id = order.Id,
                DisplayOrderNumber = order.DisplayOrderNumber, // Added DisplayOrderNumber
                DayId = order.DayId,
                AreaId = order.AreaId,
                AreaName = areaName,
                CashierId = order.CashierId,
                CashierName = cashierName,
                WaiterId = order.WaiterId,
                OrderDateTime = order.OrderDateTime,
                Status = order.Status,
                TotalAmount = order.TotalAmount,
                PaymentMethod = order.PaymentMethod,
                AmountPaid = order.AmountPaid,
                CustomerName = order.CustomerName,
                CustomerEmail = order.CustomerEmail,
                TableNumber = order.TableNumber,
                QrCodeBase64 = qrCodeBase64,
                NumberOfGuests = order.NumberOfGuests,
                IsTakeaway = order.IsTakeaway,
                Items = order.OrderItems.Select(oi => new OrderItemDto
                {
                    MenuItemId = oi.MenuItemId,
                    MenuItemName = menuItems.TryGetValue(oi.MenuItemId, out var item) ? item.Name : "Unknown Item",
                    Quantity = oi.Quantity,
                    UnitPrice = oi.UnitPrice,
                    Note = oi.Note
                }).ToList()
            };
        }

        // Helper to generate QR Code as Base64 string
        private string GenerateQrCodeBase64(string text)
        {
            using var qrGenerator = new QRCodeGenerator();
            using var qrCodeData = qrGenerator.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q); // Quality level Q
            using var qrCode = new PngByteQRCode(qrCodeData); // Use PngByteQRCode for byte array output
            byte[] qrCodeImageBytes = qrCode.GetGraphic(20); // Pixels per module

            return Convert.ToBase64String(qrCodeImageBytes);
        }

        // Updated method for waiter confirmation - Signature changed in Interface
        public async Task<OrderDto?> ConfirmOrderPreparationAsync(string orderId, string tableNumber, ClaimsPrincipal user)
        {
            var (userOrganizationId, isSuperAdmin) = GetUserContext();
            var waiterId = GetUserId(); // Get the waiter's user ID

            if (string.IsNullOrWhiteSpace(waiterId))
            {
                // This should not happen if the endpoint is properly authorized
                throw new UnauthorizedAccessException("User ID could not be determined.");
            }


            // Include Area to check workflow flags
            var order = await _context.Orders
                                    .Include(o => o.Area) // Include Area
                                    .Include(o => o.OrderItems)
                                        .ThenInclude(oi => oi.MenuItem)
                                    .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null) throw new KeyNotFoundException($"Order with ID {orderId} not found.");

            // Authorization check: Ensure the order belongs to the user's organization OR user is SuperAdmin
            if (!isSuperAdmin && order.OrganizationId != userOrganizationId)
            {
                throw new UnauthorizedAccessException("Access denied to confirm preparation for this order.");
            }

            // Authorization check: Ensure the Area allows waiter confirmation
            if (order.Area == null)
            {
                // Should not happen due to FK constraint, but good practice
                throw new InvalidOperationException($"Area information is missing for order {orderId}.");
            }

            // Workflow Check: This action is only valid if waiter confirmation is enabled AND the order is in the Paid state.
            if (!order.Area.EnableWaiterConfirmation || order.Status != OrderStatus.Paid)
            {
                _logger.LogWarning("Attempted to confirm preparation for order {OrderId} with status {OrderStatus} in area {AreaId} where EnableWaiterConfirmation={EnableWaiterConfirmation}",
                    orderId, order.Status, order.AreaId, order.Area.EnableWaiterConfirmation);
                throw new InvalidOperationException($"Cannot confirm preparation for order {orderId}. Current status: {order.Status}. Waiter confirmation enabled: {order.Area.EnableWaiterConfirmation}.");
            }

            // Check for Open Day before proceeding (especially important if confirming a PreOrder)
            var currentOpenDayForPrep = await _dayService.GetCurrentOpenDayAsync(order.Area.OrganizationId);
            if (currentOpenDayForPrep == null)
            {
                _logger.LogWarning("Attempted to confirm order {OrderId} preparation in organization {OrganizationId} but no Day is open.", orderId, order.Area.OrganizationId);
                throw new InvalidOperationException("Cannot confirm order preparation: No operational day (Giornata) is currently open.");
            }

            // --- Determine Next Status based on Workflow Flags ---
            OrderStatus nextStatus;
            if (order.Area.EnableKds)
            {
                nextStatus = OrderStatus.Preparing; // Next step is KDS
            }
            else if (order.Area.EnableCompletionConfirmation)
            {
                nextStatus = OrderStatus.ReadyForPickup; // Skip KDS, go to Ready for final confirmation
            }
            else
            {
                nextStatus = OrderStatus.Completed; // Skip KDS and final confirmation
            }

            order.TableNumber = tableNumber.Trim();
            order.Status = nextStatus;
            order.DayId = currentOpenDayForPrep.Id; // Associate with the current open Day
            order.WaiterId = waiterId; // Record the waiter who confirmed

            try
            {
                await _context.SaveChangesAsync();
                await SendOrderStatusUpdateAsync(order.Id, order.Status, order.OrganizationId, order.AreaId); // Send SignalR update

                // --- Trigger Deferred Comanda Printing ---
                try
                {
                    _logger.LogInformation("Order {OrderId} confirmed by waiter, triggering deferred comanda printing.", order.Id);
                    await TriggerDeferredComandaPrintingAsync(order);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during deferred comanda printing for waiter-confirmed Order ID {OrderId}. Main confirmation was successful.", order.Id);
                    // Do not rethrow.
                }
                // --- End Printing ---


                // Fetch related data again for DTO mapping (or pass necessary info)
                var areaName = order.Area.Name; // We already have the Area
                var waiter = await _context.Users.FindAsync(waiterId);
                var waiterName = $"{waiter?.FirstName ?? ""} {waiter?.LastName ?? ""}".Trim();
                var menuItemIds = order.OrderItems.Select(oi => oi.MenuItemId).Distinct().ToList();
                var menuItems = await _context.MenuItems
                                              .Where(mi => menuItemIds.Contains(mi.Id))
                                              .ToDictionaryAsync(mi => mi.Id);
                // Get cashier name if available
                string? cashierName = null;
                if (!string.IsNullOrEmpty(order.CashierId))
                {
                    var cashier = await _context.Users.FindAsync(order.CashierId);
                    cashierName = $"{cashier?.FirstName ?? ""} {cashier?.LastName ?? ""}".Trim();
                }


                // Map to DTO
                return MapOrderToDto(order, areaName, cashierName, menuItems); // QR Code not needed here
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogError(ex, "Concurrency error confirming preparation for order {OrderId}", orderId);
                throw; // Re-throw for controller to handle
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error confirming preparation for order {OrderId}", orderId);
                return null; // Or re-throw specific exception
            }
        }

        // --- KDS Methods Implementation ---

        // Updated signature to include includeCompleted
        public async Task<IEnumerable<KdsOrderDto>> GetActiveOrdersForKdsStationAsync(int kdsStationId, ClaimsPrincipal user, bool includeCompleted = false)
        {
            var (userOrganizationId, isSuperAdmin) = GetUserContext();

            // 1. Validate KDS Station and User Access
            var station = await _context.KdsStations
                                        .AsNoTracking()
                                        .FirstOrDefaultAsync(ks => ks.Id == kdsStationId);

            if (station == null)
            {
                throw new KeyNotFoundException($"KDS Station with ID {kdsStationId} not found.");
            }

            if (!isSuperAdmin && station.OrganizationId != userOrganizationId)
            {
                throw new UnauthorizedAccessException($"Access denied to KDS Station {kdsStationId}.");
            }

            // 2. Get Assigned Category IDs for this station
            var assignedCategoryIds = await _context.KdsCategoryAssignments
                                                    .Where(kca => kca.KdsStationId == kdsStationId)
                                                    .Select(kca => kca.MenuCategoryId)
                                                    .ToListAsync();

            if (!assignedCategoryIds.Any())
            {
                _logger.LogInformation("KDS Station {KdsStationId} has no assigned categories.", kdsStationId);
                return Enumerable.Empty<KdsOrderDto>(); // No categories means no orders to display
            }

            // --- Day Filtering ---
            var currentOpenDay = await _dayService.GetCurrentOpenDayAsync(station.OrganizationId);
            if (currentOpenDay == null && !includeCompleted) // Only return empty if fetching active orders and no day is open
            {
                _logger.LogInformation("GetActiveOrdersForKdsStationAsync (Active): No open Day for Organization {OrganizationId}. Returning empty list.", station.OrganizationId);
                return Enumerable.Empty<KdsOrderDto>(); // No open day, no active orders to display
            }
            // If includeCompleted is true, we might still show historical orders even if no day is currently open.
            // The filtering below will handle the DayId check.
            // --- End Day Filtering ---


            // 3. Fetch relevant orders and items based on includeCompleted flag
            List<Order> relevantOrders;

            if (includeCompleted)
            {
                // Fetch orders that HAVE been confirmed by THIS KDS station, regardless of current Order.Status
                _logger.LogInformation("Fetching completed orders for KDS Station {KdsStationId}", kdsStationId);
                relevantOrders = await _context.Orders
                    .Where(o => o.OrganizationId == station.OrganizationId) // Filter by Org
                                                                            // If a day is open, only include completed orders from that day? Or all completed? Let's filter by day if open.
                    .Where(o => currentOpenDay == null || o.DayId == currentOpenDay.Id) // Filter by current day if open
                    .Include(o => o.OrderItems)
                        .ThenInclude(oi => oi.MenuItem)
                            .ThenInclude(mi => mi!.MenuCategory)
                    // Include only orders where this specific KDS station HAS confirmed its part
                    .Where(o => _context.OrderKdsStationStatuses.Any(okss => okss.OrderId == o.Id &&
                                                                             okss.KdsStationId == kdsStationId &&
                                                                             okss.IsConfirmed))
                    .OrderByDescending(o => o.OrderDateTime) // Newest completed first? Or keep OrderDateTime ascending? Let's try descending for completed.
                    .AsNoTracking()
                    .ToListAsync();
            }
            else
            {
                // Fetch orders in 'Preparing' status for the CURRENT OPEN DAY that have NOT already been confirmed by THIS KDS station
                _logger.LogInformation("Fetching active orders for KDS Station {KdsStationId} for Day {DayId}", kdsStationId, currentOpenDay?.Id);
                relevantOrders = await _context.Orders
                   .Where(o => o.OrganizationId == station.OrganizationId &&
                               o.Status == OrderStatus.Preparing &&
                               o.DayId == currentOpenDay!.Id) // Filter by Org, Status, AND current open Day
                   .Include(o => o.OrderItems)
                       .ThenInclude(oi => oi.MenuItem)
                           .ThenInclude(mi => mi!.MenuCategory) // Need category to filter items later
                                                                // Exclude orders where this specific KDS station has already confirmed its part
                   .Where(o => !_context.OrderKdsStationStatuses.Any(okss => okss.OrderId == o.Id &&
                                                                             okss.KdsStationId == kdsStationId &&
                                                                             okss.IsConfirmed))
                   .OrderBy(o => o.OrderDateTime) // Oldest active orders first
                   .AsNoTracking()
                   .ToListAsync();
            }

            // 4. Project to KdsOrderDto, filtering items within each order
            var kdsOrderDtos = relevantOrders.Select(order => new KdsOrderDto
            {
                OrderId = order.Id,
                DayId = order.DayId,
                OrderDateTime = order.OrderDateTime,
                TableNumber = order.TableNumber,
                CustomerName = order.CustomerName,
                NumberOfGuests = order.NumberOfGuests,
                IsTakeaway = order.IsTakeaway,
                Items = order.OrderItems
                    .Where(oi => assignedCategoryIds.Contains(oi.MenuItem.MenuCategoryId)) // Filter items for *this* station
                    .Select(oi => new KdsOrderItemDto
                    {
                        OrderItemId = oi.Id,
                        MenuItemName = oi.MenuItem?.Name ?? "Unknown Item",
                        Quantity = oi.Quantity,
                        Note = oi.Note,
                        KdsStatus = oi.KdsStatus // Include current KDS status
                    }).ToList()
            })
            // Ensure we only return orders that still have items relevant to this station after filtering
            // (Handles cases where an order might have items for multiple stations)
            .Where(dto => dto.Items.Any())
            .ToList();

            return kdsOrderDtos;
        }


        public async Task<bool> UpdateOrderItemKdsStatusAsync(string orderId, int orderItemId, KdsStatus newStatus, ClaimsPrincipal user)
        {
            var (userOrganizationId, isSuperAdmin) = GetUserContext();

            // 1. Fetch the specific OrderItem and related Order/Area for validation
            var orderItem = await _context.OrderItems
                                        .Include(oi => oi.Order)
                                            .ThenInclude(o => o!.Area) // Need Order and Area for org check
                                        .FirstOrDefaultAsync(oi => oi.Id == orderItemId && oi.OrderId == orderId);

            if (orderItem == null || orderItem.Order == null || orderItem.Order.Area == null)
            {
                _logger.LogWarning("OrderItem {OrderItemId} for Order {OrderId} not found or missing related data.", orderItemId, orderId);
                return false; // Not found or invalid state
            }

            // 2. Authorize: Check if user belongs to the order's organization
            if (!isSuperAdmin && orderItem.Order.Area.OrganizationId != userOrganizationId)
            {
                _logger.LogWarning("User denied access to update KDS status for OrderItem {OrderItemId} in Order {OrderId}.", orderItemId, orderId);
                throw new UnauthorizedAccessException("Access denied to update KDS status for this order item.");
            }

            // 3. Update the KdsStatus
            if (orderItem.KdsStatus == newStatus)
            {
                _logger.LogInformation("KDS Status for OrderItem {OrderItemId} is already {Status}. No change made.", orderItemId, newStatus);
                return true; // Indicate success as the state is already correct
            }

            orderItem.KdsStatus = newStatus;
            _context.OrderItems.Update(orderItem);
            int changes = await _context.SaveChangesAsync();

            if (changes > 0)
            {
                _logger.LogInformation("Updated KDS Status for OrderItem {OrderItemId} in Order {OrderId} to {Status}.", orderItemId, orderId, newStatus);

                // This method NO LONGER triggers the overall order status check.
                // TODO: Consider if a SignalR event for individual item status is still needed.
                // Example: await _hubContext.Clients.Group($"KDS_{orderItem.Order.Area.OrganizationId}").SendAsync("KdsItemStatusUpdate", orderId, orderItemId, newStatus);

                return true;
            }
            else
            {
                _logger.LogWarning("Failed to save KDS Status update for OrderItem {OrderItemId} in Order {OrderId}.", orderItemId, orderId);
                return false;
            }
        }

        public async Task<bool> ConfirmKdsOrderCompletionAsync(string orderId, int kdsStationId, ClaimsPrincipal user)
        {
            var (userOrganizationId, isSuperAdmin) = GetUserContext();
            var userId = GetUserId();

            // Fetch the order, including items, and the Area for workflow flags
            var order = await _context.Orders
                .Include(o => o.Area) // Include Area for workflow flags
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.MenuItem) // Need MenuItem for CategoryId
                        .ThenInclude(mi => mi!.MenuCategory) // Need Category for Name/Grouping (Optional but good)
                                                             // Removed include for OrderKdsStationStatuses - will query separately
                                                             // .Include(o => o.OrderKdsStationStatuses.Where(oks => oks.KdsStationId == kdsStationId))
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null) throw new KeyNotFoundException($"Order with ID {orderId} not found.");

            // Authorization check (Org level)
            if (!isSuperAdmin && order.OrganizationId != userOrganizationId)
            {
                throw new UnauthorizedAccessException("Access denied to confirm KDS completion for this order.");
            }

            // Fetch the specific KDS Station to verify association with the Area and user's Org
            var kdsStation = await _context.KdsStations
                .FirstOrDefaultAsync(ks => ks.Id == kdsStationId);

            if (kdsStation == null)
            {
                throw new KeyNotFoundException($"KDS Station with ID {kdsStationId} not found.");
            }
            if (kdsStation.AreaId != order.AreaId)
            {
                // This check prevents confirming using a station from another area by mistake
                throw new InvalidOperationException($"KDS Station {kdsStationId} does not belong to the order's Area {order.AreaId}.");
            }
            // Org check on KDS station (redundant if Area check passes, but safe)
            if (!isSuperAdmin && kdsStation.OrganizationId != userOrganizationId)
            {
                throw new UnauthorizedAccessException($"Access denied to KDS Station {kdsStationId}.");
            }


            // Workflow check: KDS must be enabled for the area, and order must be 'Preparing'
            if (order.Area == null || !order.Area.EnableKds || order.Status != OrderStatus.Preparing)
            {
                _logger.LogWarning("Attempted KDS completion for order {OrderId} (Status: {OrderStatus}) in Area {AreaId} (KDS Enabled: {EnableKds})",
                    orderId, order.Status, order.AreaId, order.Area?.EnableKds);
                throw new InvalidOperationException($"Cannot confirm KDS completion. Order Status: {order.Status}, KDS Enabled: {order.Area?.EnableKds}.");
            }

            // Find or create the status tracker for this order and KDS station
            // Query OrderKdsStationStatuses directly from context
            var stationStatus = await _context.OrderKdsStationStatuses
                                        .FirstOrDefaultAsync(oks => oks.OrderId == orderId && oks.KdsStationId == kdsStationId);

            if (stationStatus == null)
            {
                stationStatus = new OrderKdsStationStatus
                {
                    OrderId = orderId,
                    KdsStationId = kdsStationId,
                    IsConfirmed = false // Use IsConfirmed, default is false
                };
                _context.OrderKdsStationStatuses.Add(stationStatus);
                // Need to save here so subsequent checks work correctly if it's a new status record
                await _context.SaveChangesAsync();
            }

            // Check if already completed (confirmed) for this station
            if (stationStatus.IsConfirmed) // Use IsConfirmed
            {
                _logger.LogInformation("KDS Station {KdsStationId} completion for order {OrderId} already recorded.", kdsStationId, orderId);
                return true;
            }


            // Verify all items assigned to *this* KDS station are marked as 'Confirmed'
            // Get category IDs for items in the order
            var orderCategoryIds = order.OrderItems
                .Where(oi => oi.MenuItem?.MenuCategoryId != null)
                .Select(oi => oi.MenuItem!.MenuCategoryId)
                .Distinct()
                .ToList();

            // Get KDS assignments for those categories for *this* station
            var assignmentsForStation = await _context.KdsCategoryAssignments
                .Where(kca => kca.KdsStationId == kdsStationId && orderCategoryIds.Contains(kca.MenuCategoryId))
                .Select(kca => kca.MenuCategoryId)
                .ToListAsync();

            // Filter order items belonging to categories assigned to this station
            var itemsForThisStation = order.OrderItems
                .Where(oi => oi.MenuItem?.MenuCategoryId != null && assignmentsForStation.Contains(oi.MenuItem.MenuCategoryId))
                .ToList();


            if (!itemsForThisStation.Any())
            {
                _logger.LogWarning("No items found assigned to KDS Station {KdsStationId} for order {OrderId}. Marking station as vacuously confirmed.", kdsStationId, orderId);
                // Treat as completed/confirmed for this station even if no items were assigned to it.
                stationStatus.IsConfirmed = true; // Use IsConfirmed
                                                  // Add completion time/user if those fields existed in OrderKdsStationStatus
                                                  // stationStatus.CompletionTime = DateTime.UtcNow;
                                                  // stationStatus.CompletedByUserId = userId;

                // Fall through to check overall order completion
            }
            else
            {
                bool allItemsDoneForStation = itemsForThisStation.All(oi => oi.KdsStatus == KdsStatus.Confirmed); // Use Confirmed

                if (!allItemsDoneForStation)
                {
                    _logger.LogWarning("Attempted to confirm KDS completion for station {KdsStationId}, order {OrderId}, but not all assigned items have KdsStatus 'Confirmed'.", kdsStationId, orderId);
                    throw new InvalidOperationException("Not all items assigned to this KDS station are marked as confirmed.");
                }

                // Mark this station as completed (confirmed) for this order
                stationStatus.IsConfirmed = true; // Use IsConfirmed
                // Add completion time/user if those fields existed
                // stationStatus.CompletionTime = DateTime.UtcNow;
                // stationStatus.CompletedByUserId = userId; // Record who confirmed
            }


            // --- Check if ALL required KDS stations have completed the order ---
            // Get all KDS stations associated with the items in this order
            var allRelevantCategoryIds = order.OrderItems
               .Where(oi => oi.MenuItem?.MenuCategoryId != null)
               .Select(oi => oi.MenuItem!.MenuCategoryId)
               .Distinct()
               .ToList();

            var allRelevantStationIds = await _context.KdsCategoryAssignments
                .Where(kca => allRelevantCategoryIds.Contains(kca.MenuCategoryId))
                .Select(kca => kca.KdsStationId)
                .Distinct()
                .ToListAsync();


            if (!allRelevantStationIds.Any())
            {
                // If no items are assigned to ANY KDS station
                _logger.LogInformation("Order {OrderId} has KDS enabled but no items assigned to any KDS station. Marking as KDS-complete.", orderId);
            }
            else
            {
                // Fetch all completion statuses for this order to check if all relevant stations are done
                var allCompletionStatuses = await _context.OrderKdsStationStatuses
                    .Where(oks => oks.OrderId == orderId && allRelevantStationIds.Contains(oks.KdsStationId))
                    .ToListAsync();

                bool allStationsDone = allRelevantStationIds.All(stationId =>
                   allCompletionStatuses.Any(status => status.KdsStationId == stationId && status.IsConfirmed)); // Use IsConfirmed

                if (!allStationsDone)
                {
                    // Not all stations required for this order have confirmed completion yet.
                    // Just save the current station's completion status and return.
                    _logger.LogInformation("KDS Station {KdsStationId} confirmed for order {OrderId}. Waiting for other stations.", kdsStationId, orderId);
                    await _context.SaveChangesAsync(); // Save the updated stationStatus
                    return true; // Indicate success for *this* station's confirmation
                }
                // Fixed logger call:
                _logger.LogInformation("All relevant KDS stations ({StationCount}) have confirmed order {OrderId}.", allRelevantStationIds.Count, orderId);
            }


            // --- All necessary KDS stations are complete. Determine next Order Status ---
            OrderStatus nextStatus;
            if (order.Area.EnableCompletionConfirmation)
            {
                nextStatus = OrderStatus.ReadyForPickup; // Next step is final pickup confirmation
            }
            else
            {
                nextStatus = OrderStatus.Completed; // Skip final confirmation
            }

            order.Status = nextStatus;

            try
            {
                // Save the updated stationStatus (if changed) AND the Order status change
                await _context.SaveChangesAsync();
                await SendOrderStatusUpdateAsync(order.Id, order.Status, order.OrganizationId, order.AreaId); // SignalR update for overall status

                // --- Printing ---
                // try
                // {
                //     _logger.LogInformation("Attempting to print receipt for Order ID {OrderId} after pre-order payment confirmation.", order.Id);
                //     await _printService.PrintOrderDocumentsAsync(order, PrintJobType.Receipt);

                //     // Comanda Printing Logic for Confirmed Pre-Order (similar to new Cashier Order):
                //     // 'order.Area' is loaded. We might need to load CashierStation if order.CashierStationId is set (though usually null for pre-orders before this point).
                //     bool comandaPrinted = false;
                //     if (order.CashierStationId.HasValue) // CashierStationId might be set if a specific station confirms the pre-order
                //     {
                //         var station = await _context.CashierStations.FindAsync(order.CashierStationId.Value);
                //         if (station?.PrintComandasAtThisStation == true)
                //         {
                //             _logger.LogInformation("Printing comanda at station {StationId} for confirmed Pre-Order ID {OrderId}.", station.Id, order.Id);
                //             await _printService.PrintOrderDocumentsAsync(order, PrintJobType.Comanda);
                //             comandaPrinted = true;
                //         }
                //     }

                //     if (!comandaPrinted && order.Area?.PrintComandasAtCashier == true) // order.Area should be loaded
                //     {
                //         _logger.LogInformation("Printing comanda at area default for confirmed Pre-Order ID {OrderId}.", order.Id);
                //         await _printService.PrintOrderDocumentsAsync(order, PrintJobType.Comanda);
                //     }
                // }
                // catch (Exception ex)
                // {
                //     _logger.LogError(ex, "Error during printing for Order ID {OrderId} after pre-order payment confirmation. Operation continued.", order.Id);
                //     // Do not rethrow.
                // }
                // --- End Printing ---

                return true;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogError(ex, "Concurrency error during KDS completion confirmation for order {OrderId}, station {KdsStationId}", orderId, kdsStationId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error confirming KDS completion for order {OrderId}, station {KdsStationId}", orderId, kdsStationId);
                return false;
            }
        }

        // --- Pre-Order Confirmation Implementation ---
        public async Task<OrderDto?> ConfirmPreOrderPaymentAsync(string orderId, ConfirmPreOrderPaymentDto paymentDto, ClaimsPrincipal user)
        {
            var (userOrganizationId, isSuperAdmin) = GetUserContext();
            var authenticatedUserId = GetUserId();

            if (string.IsNullOrEmpty(authenticatedUserId))
            {
                // Should not happen if endpoint is authorized, but check defensively
                throw new UnauthorizedAccessException("User ID could not be determined from the token.");
            }

            // Transactions are recommended for multi-step updates
            bool useTransaction = !_context.Database.ProviderName.Contains("InMemory");
            Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction = null;

            if (useTransaction)
            {
                transaction = await _context.Database.BeginTransactionAsync();
            }

            try
            {
                // 1. Fetch the order, including necessary related data
                var order = await _context.Orders
                                     .Include(o => o.Area) // For org check and DTO mapping
                                     .Include(o => o.OrderItems) // To compare and update items
                                     .FirstOrDefaultAsync(o => o.Id == orderId);

                if (order == null)
                {
                    _logger.LogWarning("Pre-order {OrderId} not found for payment confirmation.", orderId);
                    throw new KeyNotFoundException($"Order with ID {orderId} not found.");
                }

                // 2. Validation
                if (order.Status != OrderStatus.PreOrder)
                {
                    _logger.LogWarning("Attempted to confirm payment for order {OrderId} which is not a PreOrder (Status: {Status}).", orderId, order.Status);
                    throw new InvalidOperationException($"Order {orderId} is not a PreOrder and cannot be confirmed this way.");
                }

                if (order.Area == null)
                {
                    _logger.LogError("Order {OrderId} has a null Area during payment confirmation. Data integrity issue.", orderId);
                    throw new InvalidOperationException("Order data is inconsistent (missing Area).");
                }

                if (!isSuperAdmin && order.Area.OrganizationId != userOrganizationId)
                {
                    _logger.LogWarning("User {UserId} denied access to confirm payment for order {OrderId} in organization {OrganizationId}.", authenticatedUserId, orderId, order.Area.OrganizationId);
                    throw new UnauthorizedAccessException("Access denied to confirm payment for this order.");
                }

                // Validate required fields in DTO (though model binding should handle most)
                if (string.IsNullOrWhiteSpace(paymentDto.CustomerName))
                {
                    throw new ArgumentException("Customer name cannot be empty.", nameof(paymentDto.CustomerName));
                }
                if (string.IsNullOrWhiteSpace(paymentDto.PaymentMethod))
                {
                    throw new ArgumentException("Payment method is required.", nameof(paymentDto.PaymentMethod));
                }

                // Check for Open Day before proceeding
                var currentOpenDay = await _dayService.GetCurrentOpenDayAsync(order.Area.OrganizationId);
                if (currentOpenDay == null)
                {
                    _logger.LogWarning("Attempted to confirm pre-order {OrderId} payment in organization {OrganizationId} but no Day is open.", orderId, order.Area.OrganizationId);
                    throw new InvalidOperationException("Cannot confirm pre-order payment: No operational day (Giornata) is currently open.");
                }

                // 3. Update Order Properties
                // --- Determine Next Status based on Workflow Flags ---
                OrderStatus nextStatus;
                if (order.Area.EnableWaiterConfirmation)
                {
                    nextStatus = OrderStatus.Paid; // Needs waiter confirmation
                }
                else if (order.Area.EnableKds)
                {
                    nextStatus = OrderStatus.Preparing; // Skip Paid, go directly to Preparing for KDS
                }
                else if (order.Area.EnableCompletionConfirmation)
                {
                    nextStatus = OrderStatus.ReadyForPickup; // Skip Paid & Preparing, go to Ready for final confirmation
                }
                else
                {
                    nextStatus = OrderStatus.Completed; // Skip all intermediate steps
                }
                order.Status = nextStatus;
                order.CashierId = authenticatedUserId;
                order.DayId = currentOpenDay.Id; // Associate with the current open Day
                order.PaymentMethod = paymentDto.PaymentMethod;
                order.AmountPaid = paymentDto.AmountPaid;
                order.CustomerName = paymentDto.CustomerName.Trim(); // Update customer name
                order.IsTakeaway = paymentDto.IsTakeaway;
                order.NumberOfGuests = paymentDto.NumberOfGuests;

                if (paymentDto.CashierStationId.HasValue)
                {
                    var cashierStation = await _context.CashierStations
                        .FirstOrDefaultAsync(cs => cs.Id == paymentDto.CashierStationId.Value && cs.OrganizationId == order.OrganizationId && cs.AreaId == order.AreaId && cs.IsEnabled);
                    if (cashierStation == null)
                    {
                        throw new KeyNotFoundException($"Cashier Station with ID {paymentDto.CashierStationId.Value} not found, not enabled, or does not belong to the order's area/organization.");
                    }
                    order.CashierStationId = paymentDto.CashierStationId.Value; // Assign to the order
                }
                else
                {
                    // If no CashierStationId is provided from frontend, we might clear it if it was set previously for some reason,
                    // or enforce that it MUST be provided by the cashier interface. For now, let's assume it's optional from DTO
                    // but if provided, it must be valid. If not provided, the order.CashierStationId might remain null
                    // or be determined by other logic if applicable.
                    // For now, if it's not in DTO, we don't change it on the order unless it needs to be explicitly nulled.
                    order.CashierStationId = null; // Explicitly nullify if not provided, assuming new context.
                    _logger.LogInformation("No CashierStationId provided for PreOrder payment confirmation for Order {OrderId}. Order will not be linked to a specific station.", orderId);
                }
                // Keep original OrderDateTime for pre-orders? Or update to payment time? Let's keep original.
                // order.OrderDateTime = DateTime.UtcNow;

                // 4. Handle Item Changes
                decimal newTotalAmount = 0;
                var updatedOrderItems = new List<OrderItem>();
                var incomingMenuItemIds = paymentDto.Items.Select(i => i.MenuItemId).Distinct().ToList();

                // Preload menu items for validation and price lookup
                var menuItems = await _context.MenuItems
                                              .Include(mi => mi.MenuCategory) // For area check
                                              .Where(mi => incomingMenuItemIds.Contains(mi.Id) && mi.MenuCategory != null && mi.MenuCategory.AreaId == order.AreaId)
                                              .ToDictionaryAsync(mi => mi.Id);

                foreach (var itemDto in paymentDto.Items)
                {
                    if (itemDto.Quantity <= 0) continue; // Ignore items with zero or negative quantity

                    if (!menuItems.TryGetValue(itemDto.MenuItemId, out var menuItem))
                    {
                        // Item either doesn't exist or doesn't belong to the order's area
                        throw new KeyNotFoundException($"MenuItem with ID {itemDto.MenuItemId} not found in Area ID {order.AreaId}.");
                    }

                    // Check if note is required but missing
                    if (menuItem.IsNoteRequired && string.IsNullOrWhiteSpace(itemDto.Note))
                    {
                        throw new InvalidOperationException($"Required note is missing for MenuItem ID {menuItem.Id}.");
                    }

                    // --- Stock Check (Scorta) for PreOrder Confirmation ---
                    if (menuItem.Scorta.HasValue)
                    {
                        if (menuItem.Scorta.Value < itemDto.Quantity)
                        {
                            // Specific error message for pre-order confirmation context
                            throw new InvalidOperationException($"Item '{menuItem.Name}' (ID: {menuItem.Id}) is now out of stock or has insufficient quantity for pre-order confirmation. Requested: {itemDto.Quantity}, Available: {menuItem.Scorta.Value}. Please modify the order.");
                        }
                    }
                    // --- End Stock Check ---

                    var existingItem = order.OrderItems.FirstOrDefault(oi => oi.MenuItemId == itemDto.MenuItemId);

                    if (existingItem != null)
                    {
                        // Update existing item
                        existingItem.Quantity = itemDto.Quantity;
                        existingItem.Note = itemDto.Note;
                        existingItem.UnitPrice = menuItem.Price; // Ensure price is current
                        updatedOrderItems.Add(existingItem);
                        newTotalAmount += existingItem.Quantity * existingItem.UnitPrice;
                    }
                    else
                    {
                        // Add new item
                        var newOrderItem = new OrderItem
                        {
                            OrderId = order.Id, // Associate with the order
                            MenuItemId = menuItem.Id,
                            Quantity = itemDto.Quantity,
                            UnitPrice = menuItem.Price,
                            Note = itemDto.Note,
                            KdsStatus = KdsStatus.Pending // New items start as pending for KDS
                        };
                        updatedOrderItems.Add(newOrderItem);
                        newTotalAmount += newOrderItem.Quantity * newOrderItem.UnitPrice;
                    }
                }

                // Remove items that were in the original order but not in the DTO
                var itemsToRemove = order.OrderItems.Where(oi => !updatedOrderItems.Any(uoi => uoi.Id == oi.Id || uoi.MenuItemId == oi.MenuItemId)).ToList();
                if (itemsToRemove.Any())
                {
                    _context.OrderItems.RemoveRange(itemsToRemove);
                    _logger.LogInformation("Removing {Count} items from Order {OrderId} during pre-order confirmation.", itemsToRemove.Count, orderId);
                }

                // Update the order's collection (EF Core tracks changes)
                order.OrderItems = updatedOrderItems;

                // 5. Update Total Amount
                order.TotalAmount = newTotalAmount;

                // Add guest and takeaway charges
                if (paymentDto.IsTakeaway && order.Area.TakeawayCharge > 0)
                {
                    order.TotalAmount += order.Area.TakeawayCharge;
                }
                else if (!paymentDto.IsTakeaway && order.Area.GuestCharge > 0 && paymentDto.NumberOfGuests > 0)
                {
                    order.TotalAmount += paymentDto.NumberOfGuests * order.Area.GuestCharge;
                }

                // --- Generate DisplayOrderNumber for Confirmed PreOrder ---
                string displayOrderNumberPrefixPreOrder = order.Area.Slug.ToUpperInvariant();
                displayOrderNumberPrefixPreOrder = new string(displayOrderNumberPrefixPreOrder.Where(char.IsLetterOrDigit).ToArray());
                if (displayOrderNumberPrefixPreOrder.Length > 3)
                {
                    displayOrderNumberPrefixPreOrder = displayOrderNumberPrefixPreOrder.Substring(0, 3);
                }
                else if (string.IsNullOrEmpty(displayOrderNumberPrefixPreOrder))
                {
                    displayOrderNumberPrefixPreOrder = "ORD"; // Fallback prefix
                }

                var sequencePreOrder = await _context.AreaDayOrderSequences
                    .FirstOrDefaultAsync(s => s.AreaId == order.AreaId && s.DayId == currentOpenDay.Id);

                if (sequencePreOrder == null)
                {
                    sequencePreOrder = new AreaDayOrderSequence
                    {
                        AreaId = order.AreaId,
                        DayId = currentOpenDay.Id,
                        LastSequenceNumber = 0
                    };
                    _context.AreaDayOrderSequences.Add(sequencePreOrder);
                }

                sequencePreOrder.LastSequenceNumber++;
                order.DisplayOrderNumber = $"{displayOrderNumberPrefixPreOrder}-{sequencePreOrder.LastSequenceNumber:D3}";
                // --- End Generate DisplayOrderNumber ---

                // --- Stock Decrement (Scorta) for Confirmed PreOrder ---
                foreach (var oi in order.OrderItems) // Iterate over the final list of items in the order
                {
                    // menuItems dictionary contains the items being confirmed, with their current stock levels
                    if (menuItems.TryGetValue(oi.MenuItemId, out var mi) && mi.Scorta.HasValue)
                    {
                        mi.Scorta -= oi.Quantity;
                        _context.MenuItems.Update(mi); // Ensure EF Core tracks this change
                    }
                }
                // --- End Stock Decrement ---

                // 6. Save Changes
                _context.Orders.Update(order); // Mark order as modified
                await _context.SaveChangesAsync(); // Saves Order, OrderItems, AreaDayOrderSequence, and Stock Updates

                // 7. Commit Transaction
                if (useTransaction && transaction != null)
                {
                    await transaction.CommitAsync();
                }

                _logger.LogInformation("Pre-order {OrderId} (Display: {DisplayOrderNumber}) successfully confirmed and paid by Cashier {CashierId}.", order.Id, order.DisplayOrderNumber, authenticatedUserId);

                // 8. SignalR for Order Status and Stock Updates
                await SendOrderStatusUpdateAsync(order.Id, order.Status, order.OrganizationId, order.AreaId);

                // --- SignalR Broadcast for Stock Updates (Confirmed PreOrder) ---
                foreach (var oi in order.OrderItems)
                {
                    if (menuItems.TryGetValue(oi.MenuItemId, out var mi) && mi.Scorta.HasValue) // Check if Scorta was involved
                    {
                        var stockUpdateDto = new StockUpdateBroadcastDto
                        {
                            MenuItemId = mi.Id,
                            AreaId = order.AreaId, // order.Area is loaded
                            NewScorta = mi.Scorta, // The new, decremented value
                            Timestamp = DateTime.UtcNow
                        };
                        await _hubContext.Clients.Group($"Area-{order.AreaId}").SendAsync("ReceiveStockUpdate", stockUpdateDto);
                        _logger.LogInformation("Broadcasted stock update for MenuItem {MenuItemId} in Area {AreaId} (PreOrder Confirmed), NewScorta: {NewScorta}", mi.Id, order.AreaId, mi.Scorta);
                    }
                }
                // --- End SignalR Broadcast ---

                // 9. Map to DTO and Return
                // Fetch cashier details for the DTO
                var cashier = await _context.Users.FindAsync(authenticatedUserId);
                string cashierFullName = $"{cashier?.FirstName ?? ""} {cashier?.LastName ?? ""}".Trim();
                string areaName = order.Area?.Name ?? "Unknown Area";

                // Re-fetch menu item names for the final item set for accurate DTO mapping
                var finalMenuItemIds = order.OrderItems.Select(oi => oi.MenuItemId).ToList();
                var finalMenuItemNames = await _context.MenuItems
                                                      .Where(mi => finalMenuItemIds.Contains(mi.Id))
                                                      .ToDictionaryAsync(mi => mi.Id, mi => mi.Name);

                // Generate a new QR code? Or is the old one still valid? Let's assume the ID is the key, so old QR is fine.
                // If we needed to encode more data, we'd regenerate here.
                // string qrCodeBase64 = GenerateQrCodeBase64(order.Id);

                return MapOrderToDto(order, areaName, cashierFullName, finalMenuItemNames); // Pass null for QR code for now
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception during ConfirmPreOrderPaymentAsync for Order {OrderId}", orderId);
                if (useTransaction && transaction != null)
                {
                    await transaction.RollbackAsync();
                }
                // Re-throw the exception to be handled by the controller/middleware
                // Or return null / specific error DTO
                throw; // Rethrowing preserves stack trace and allows centralized error handling
            }
            finally
            {
                if (transaction != null)
                {
                    await transaction.DisposeAsync();
                }
            }
        }

        // --- NEW METHOD for Final Order Completion/Pickup ---
        public async Task<OrderDto?> ConfirmOrderPickupAsync(string orderId, ClaimsPrincipal user)
        {
            var (userOrganizationId, isSuperAdmin) = GetUserContext();
            var userId = GetUserId(); // ID of the user confirming pickup

            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new UnauthorizedAccessException("User ID could not be determined.");
            }

            // Fetch order including Area for workflow check
            var order = await _context.Orders
                                    .Include(o => o.Area)
                                    .Include(o => o.OrderItems)
                                        .ThenInclude(oi => oi.MenuItem) // For mapping
                                    .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null) throw new KeyNotFoundException($"Order with ID {orderId} not found.");

            // Authorization Check
            if (!isSuperAdmin && order.OrganizationId != userOrganizationId)
            {
                throw new UnauthorizedAccessException("Access denied to confirm pickup for this order.");
            }

            if (order.Area == null)
            {
                throw new InvalidOperationException($"Area information is missing for order {orderId}.");
            }

            // Workflow Check: This action is only valid if completion confirmation is enabled AND the order is ReadyForPickup
            if (!order.Area.EnableCompletionConfirmation || order.Status != OrderStatus.ReadyForPickup)
            {
                _logger.LogWarning("Attempted to confirm pickup for order {OrderId} with status {OrderStatus} in area {AreaId} where EnableCompletionConfirmation={EnableCompletionConfirmation}",
                    orderId, order.Status, order.AreaId, order.Area.EnableCompletionConfirmation);
                throw new InvalidOperationException($"Cannot confirm pickup for order {orderId}. Current status: {order.Status}. Completion confirmation enabled: {order.Area.EnableCompletionConfirmation}.");
            }

            // Transition to Completed status
            order.Status = OrderStatus.Completed;
            // Optionally record who completed it, if needed (e.g., add a 'CompletedByUserId' field to Order model)
            // order.CompletedByUserId = userId;

            try
            {
                await _context.SaveChangesAsync();
                await SendOrderStatusUpdateAsync(order.Id, order.Status, order.OrganizationId, order.AreaId); // SignalR update

                // TODO: Trigger Print Action? (Unlikely needed at this stage)

                // Fetch related data for DTO mapping
                var areaName = order.Area.Name;
                string? cashierName = null;
                if (!string.IsNullOrEmpty(order.CashierId))
                {
                    var cashier = await _context.Users.FindAsync(order.CashierId);
                    cashierName = $"{cashier?.FirstName ?? ""} {cashier?.LastName ?? ""}".Trim();
                }
                string? waiterName = null;
                if (!string.IsNullOrEmpty(order.WaiterId))
                {
                    var waiter = await _context.Users.FindAsync(order.WaiterId);
                    waiterName = $"{waiter?.FirstName ?? ""} {waiter?.LastName ?? ""}".Trim();
                }

                var menuItemIds = order.OrderItems.Select(oi => oi.MenuItemId).Distinct().ToList();
                var menuItems = await _context.MenuItems
                                              .Where(mi => menuItemIds.Contains(mi.Id))
                                              .ToDictionaryAsync(mi => mi.Id);

                // Map to DTO - Consider adding WaiterName to OrderDto if useful
                return MapOrderToDto(order, areaName, cashierName, menuItems);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogError(ex, "Concurrency error confirming pickup for order {OrderId}", orderId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error confirming pickup for order {OrderId}", orderId);
                return null;
            }
        }

        public async Task<IEnumerable<OrderDto>> GetOrdersByStatusAsync(int areaId, OrderStatus status)
        {
            // No explicit user context needed here as it's for a public display,
            // but we still need to validate the area and ensure it's for an open day.

            var area = await _context.Areas.FindAsync(areaId);
            if (area == null)
            {
                _logger.LogWarning("GetOrdersByStatusAsync: Area with ID {AreaId} not found.", areaId);
                throw new KeyNotFoundException($"Area with ID {areaId} not found.");
            }

            // Ensure there's an open day for the area's organization
            var currentOpenDay = await _dayService.GetCurrentOpenDayAsync(area.OrganizationId);
            if (currentOpenDay == null)
            {
                _logger.LogInformation("GetOrdersByStatusAsync for Area {AreaId}, Status {Status}: No open Day for Organization {OrganizationId}. Returning empty list.", areaId, status, area.OrganizationId);
                return Enumerable.Empty<OrderDto>();
            }
            _logger.LogInformation("GetOrdersByStatusAsync for Area {AreaId}, Status {Status}: Current open Day ID is {DayId}.", areaId, status, currentOpenDay.Id);

            // Log orders before DayId filter
            var ordersPreFilter = await _context.Orders
                .Where(o => o.AreaId == areaId && o.Status == status)
                .Select(o => new { o.Id, o.DayId, o.Status, o.OrderDateTime, o.CustomerName }) // Select a few fields for logging
                .ToListAsync();
            _logger.LogInformation("GetOrdersByStatusAsync for Area {AreaId}, Status {Status}: Found {Count} orders BEFORE DayId filter. Orders: {OrdersJson}",
                areaId, status, ordersPreFilter.Count, System.Text.Json.JsonSerializer.Serialize(ordersPreFilter));

            var orders = await _context.Orders
                .Where(o => o.AreaId == areaId && o.Status == status && o.DayId == currentOpenDay.Id)
                .Include(o => o.Area)       // For AreaName
                .Include(o => o.Cashier)    // For CashierName
                        .Include(o => o.Waiter)     // For WaiterName (though likely not relevant for ReadyForPickup)
                        .Include(o => o.OrderItems)
                            .ThenInclude(oi => oi.MenuItem) // For MenuItemName
                .OrderBy(o => o.OrderDateTime) // Oldest ready orders first
                .AsNoTracking()
                .ToListAsync();

            _logger.LogInformation("GetOrdersByStatusAsync for Area {AreaId}, Status {Status}, Day {DayId}: Found {Count} orders AFTER DayId filter.",
                areaId, status, currentOpenDay.Id, orders.Count);

            return orders.Select(order =>
            {
                var menuItemNames = order.OrderItems.ToDictionary(oi => oi.MenuItemId, oi => oi.MenuItem?.Name ?? "Unknown Item");
                // Cashier name might be null if it was a pre-order confirmed by system or other non-cashier flow
                string? cashierName = (order.Cashier != null) ? $"{order.Cashier.FirstName} {order.Cashier.LastName}".Trim() : null;
                return MapOrderToDto(order, area.Name, cashierName, menuItemNames);
            }).ToList();
        }

        public async Task<IEnumerable<OrderDto>> GetPublicOrdersByStatusAsync(int areaId, OrderStatus status)
        {
            var area = await _context.Areas.FindAsync(areaId);
            if (area == null)
            {
                _logger.LogWarning("GetPublicOrdersByStatusAsync: Area with ID {AreaId} not found.", areaId);
                throw new KeyNotFoundException($"Area with ID {areaId} not found.");
            }

            // Use a public method to get the current open day without user context
            var currentOpenDay = await _dayService.GetPublicCurrentOpenDayAsync(area.OrganizationId);
            if (currentOpenDay == null)
            {
                _logger.LogInformation("GetPublicOrdersByStatusAsync for Area {AreaId}, Status {Status}: No open Day for Organization {OrganizationId}. Returning empty list.", areaId, status, area.OrganizationId);
                return Enumerable.Empty<OrderDto>();
            }

            var orders = await _context.Orders
                .Where(o => o.AreaId == areaId && o.Status == status && o.DayId == currentOpenDay.Id)
                .Include(o => o.Area)
                .Include(o => o.Cashier)
                .Include(o => o.Waiter)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.MenuItem)
                .OrderBy(o => o.OrderDateTime)
                .AsNoTracking()
                .ToListAsync();

            return orders.Select(order =>
            {
                var menuItemNames = order.OrderItems.ToDictionary(oi => oi.MenuItemId, oi => oi.MenuItem?.Name ?? "Unknown Item");
                string? cashierName = (order.Cashier != null) ? $"{order.Cashier.FirstName} {order.Cashier.LastName}".Trim() : null;
                return MapOrderToDto(order, area.Name, cashierName, menuItemNames);
            }).ToList();
        }

        // Helper to send SignalR updates consistently
        private async Task SendOrderStatusUpdateAsync(string orderId, OrderStatus newStatus, int organizationId, int areaId)
        {
            _logger.LogInformation("Preparing to send order status update for Order {OrderId}, NewStatus: {NewStatus}, Org: {OrganizationId}, Area: {AreaId}", orderId, newStatus, organizationId, areaId);

            // Fetch additional order details for the broadcast DTO
            var orderDetails = await _context.Orders
                                             .Where(o => o.Id == orderId)
                                             .Select(o => new { o.CustomerName, o.TableNumber, o.DisplayOrderNumber }) // Added DisplayOrderNumber
                                             .FirstOrDefaultAsync();

            if (orderDetails == null)
            {
                _logger.LogWarning("Order {OrderId} not found when trying to send status update. Aborting broadcast.", orderId);
                return;
            }

            var broadcastDto = new OrderStatusBroadcastDto
            {
                OrderId = orderId,
                DisplayOrderNumber = orderDetails.DisplayOrderNumber, // Use orderDetails.DisplayOrderNumber
                NewStatus = newStatus,
                OrganizationId = organizationId,
                AreaId = areaId,
                CustomerName = orderDetails.CustomerName,
                TableNumber = orderDetails.TableNumber,
                StatusChangeTime = DateTime.UtcNow
            };

            string groupName = $"Area-{areaId}";
            try
            {
                await _hubContext.Clients.Group(groupName).SendAsync("ReceiveOrderStatusUpdate", broadcastDto);
                _logger.LogInformation("Broadcasted 'ReceiveOrderStatusUpdate' via SignalR for Order {OrderId} to group {GroupName}. NewStatus: {NewStatus}", orderId, groupName, newStatus);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting 'ReceiveOrderStatusUpdate' for Order {OrderId} to group {GroupName}", orderId, groupName);
            }
        }
    }
}
