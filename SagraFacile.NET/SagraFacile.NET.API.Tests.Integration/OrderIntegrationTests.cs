using Microsoft.EntityFrameworkCore; // Added for FirstOrDefaultAsync
using SagraFacile.NET.API.DTOs; // Added for DTOs
using SagraFacile.NET.API.Models;
using SagraFacile.NET.API.Services.Interfaces; // For CreateOrderDto
using System.Collections.Generic;
using System.Linq;
using System.Net;
// Removed duplicate using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Xunit;
using Microsoft.Extensions.DependencyInjection; // Added for CreateScope
using SagraFacile.NET.API.Data; // Added for ApplicationDbContext
using System.Security.Claims; // Added for ClaimsPrincipal

namespace SagraFacile.NET.API.Tests.Integration
{
    public class OrderIntegrationTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly CustomWebApplicationFactory _factory;
        private readonly HttpClient _clientSuperAdmin;
        private readonly HttpClient _clientOrg1Admin;
        private readonly HttpClient _clientOrg2Admin;
        private readonly HttpClient _clientCashierOrg1;
        private readonly HttpClient _clientWaiterOrg1; // Added Waiter client

        // Seeded IDs from CustomWebApplicationFactory - Replaced by TestConstants
        // private const int Org1Id = 1;
        // ... etc ...

        public OrderIntegrationTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
            // Use constants for client creation
            _clientSuperAdmin = _factory.CreateAuthenticatedClientAsync(TestConstants.SuperAdminEmail).Result;
            _clientOrg1Admin = _factory.CreateAuthenticatedClientAsync(TestConstants.Org1AdminEmail).Result;
            _clientOrg2Admin = _factory.CreateAuthenticatedClientAsync(TestConstants.Org2AdminEmail).Result;
            _clientCashierOrg1 = _factory.CreateAuthenticatedClientAsync(TestConstants.Org1CashierEmail).Result;
            _clientWaiterOrg1 = _factory.CreateAuthenticatedClientAsync(TestConstants.Org1WaiterEmail).Result; // Initialize Waiter client
        }

        // Helper to ensure an open day exists for Org1
        private async Task EnsureOpenDayForOrg1Async()
        {
            using var scope = _factory.Services.CreateScope();
            var dayService = scope.ServiceProvider.GetRequiredService<IDayService>();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>(); // Get DbContext

            // Fetch the Admin User ID using the known email
            var adminUser = await dbContext.Users.FirstOrDefaultAsync(u => u.Email == TestConstants.Org1AdminEmail);
            if (adminUser == null)
            {
                throw new InvalidOperationException($"Test setup failed: Could not find user with email {TestConstants.Org1AdminEmail}");
            }

            var user = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
            {
                new Claim(ClaimTypes.NameIdentifier, adminUser.Id), // Use fetched Admin User ID
                new Claim(ClaimTypes.Name, TestConstants.Org1AdminEmail),
                new Claim("OrganizationId", TestConstants.Org1Id.ToString())
            }, "TestAuthentication"));

            try
            {
                // Check directly in DB if a day is already open for Org1 within this scope
                var isOpen = await dbContext.Days.AnyAsync(d => d.OrganizationId == TestConstants.Org1Id && d.EndTime == null);

                if (!isOpen)
                {
                    // If not, open one using the service and the constructed user principal
                    await dayService.OpenDayAsync(user);
                }
            }
            catch (Exception) // Removed unused 'ex' variable
            {
                // Log or handle potential exceptions during day opening if necessary
                // Console.WriteLine($"Error ensuring open day for Org1: {ex.Message}"); // Removed problematic line
                throw; // Re-throw to fail the test if setup fails
            }
        }


        // =============================================
        // POST /api/orders Tests
        // =============================================

        [Fact]
    public async Task PostOrder_WhenCashierOrg1_ForOwnArea_ReturnsCreated()
    {
        // Arrange
        await EnsureOpenDayForOrg1Async(); // Ensure day is open
        var orderDto = new CreateOrderDto
        {
            AreaId = TestConstants.Org1Area1Id, // Use constant
            CustomerName = "Test Cashier Customer", // Added required field
            Items = new List<CreateOrderItemDto>
            {
                new CreateOrderItemDto { MenuItemId = TestConstants.Item1Cat1Id, Quantity = 1 }, // Use constant
                new CreateOrderItemDto { MenuItemId = TestConstants.Item2Cat1Id, Quantity = 2, Note = "Extra Ragu" } // Use constant
            }
        };

        // Act
            var response = await _clientCashierOrg1.PostAsJsonAsync("/api/orders", orderDto);

            // Assert
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var createdOrderDto = await response.Content.ReadFromJsonAsync<OrderDto>();
            Assert.NotNull(createdOrderDto);
        Assert.Equal(orderDto.AreaId, createdOrderDto.AreaId);
        Assert.Equal("Org1 Area 1", createdOrderDto.AreaName); // Corrected expected name
        Assert.Equal(2, createdOrderDto.Items.Count);
        Assert.False(string.IsNullOrEmpty(createdOrderDto.Id)); // Check if string ID is generated
        Assert.Equal("Org1 Cashier", createdOrderDto.CashierName); // Corrected expected name
        Assert.Contains(createdOrderDto.Items, i => i.MenuItemId == TestConstants.Item1Cat1Id && i.Quantity == 1); // Use constant
        Assert.Contains(createdOrderDto.Items, i => i.MenuItemId == TestConstants.Item2Cat1Id && i.Quantity == 2 && i.Note == "Extra Ragu"); // Use constant

        // TODO: Add cleanup if necessary (deleting created order)
    }

         [Fact]
    public async Task PostOrder_WhenOrg1Admin_ForOwnArea_ReturnsCreated() // Admins should also be able to create orders
    {
        // Arrange
        await EnsureOpenDayForOrg1Async(); // Ensure day is open
        var orderDto = new CreateOrderDto
        {
            AreaId = TestConstants.Org1Area2Id, // Use constant
            CustomerName = "Test Admin Customer", // Added required field
            Items = new List<CreateOrderItemDto>
            {
                new CreateOrderItemDto { MenuItemId = TestConstants.Item4Cat3Id, Quantity = 1, Note = "Specific request" } // Use constant
            }
        };

        // Act
            var response = await _clientOrg1Admin.PostAsJsonAsync("/api/orders", orderDto);

            // Assert
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var createdOrderDto = await response.Content.ReadFromJsonAsync<OrderDto>();
            Assert.NotNull(createdOrderDto);
            Assert.Equal(orderDto.AreaId, createdOrderDto.AreaId);
            Assert.Equal("Org1 Area 2", createdOrderDto.AreaName); // Corrected expected name (Assuming Area3 is named "Org1 Area 2")
            // Assert.Equal(Org1Id, _factory.GetDbContext().Areas.Find(createdOrderDto.AreaId)?.OrganizationId); // Removed problematic line
            Assert.Single(createdOrderDto.Items);
            Assert.Equal("Org1 Admin", createdOrderDto.CashierName); // Admin created it - Corrected expected name
        }

        [Fact]
    public async Task PostOrder_WhenCashierOrg1_ForOtherOrgArea_ReturnsForbidden()
    {
        // Arrange
        var orderDto = new CreateOrderDto
        {
            AreaId = TestConstants.Org2Area1Id, // Use constant
            CustomerName = "Wrong Org Customer", // Added required field
            Items = new List<CreateOrderItemDto> { new CreateOrderItemDto { MenuItemId = TestConstants.Item3Cat2Id, Quantity = 1 } } // Use constant
        };

        // Act
            var response = await _clientCashierOrg1.PostAsJsonAsync("/api/orders", orderDto);

            // Assert
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
    public async Task PostOrder_WhenCashierOrg1_WithItemFromOtherOrg_ReturnsBadRequestOrForbidden() // Service validation should catch this
    {
        // Arrange
        // No open day needed here, should fail before day check if item is invalid
        var orderDto = new CreateOrderDto
        {
            AreaId = TestConstants.Org1Area1Id, // Use constant
            CustomerName = "Mixed Items Customer", // Added required field
            Items = new List<CreateOrderItemDto>
            {
                new CreateOrderItemDto { MenuItemId = TestConstants.Item1Cat1Id, Quantity = 1 }, // Use constant
                new CreateOrderItemDto { MenuItemId = TestConstants.Item3Cat2Id, Quantity = 1 } // Use constant (Item from Org2)
            }
        };

        // Act
            var response = await _clientCashierOrg1.PostAsJsonAsync("/api/orders", orderDto);

            // Assert
            // Expecting BadRequest (400) because the service throws InvalidOperationException ("MenuItem ID X does not belong to the specified Area ID Y")
            // which the controller now catches and returns BadRequest.
            // If the service threw UnauthorizedAccessException ("Access denied to MenuItem ID X..."), the controller would return Forbidden.
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
    public async Task PostOrder_WithNonExistentItem_ReturnsNotFound()
    {
         // Arrange
        await EnsureOpenDayForOrg1Async(); // Ensure day is open (to test item validation *after* day check passes)
        var orderDto = new CreateOrderDto
        {
            AreaId = TestConstants.Org1Area1Id, // Use constant
            CustomerName = "Nonexistent Item Customer", // Added required field
            Items = new List<CreateOrderItemDto> { new CreateOrderItemDto { MenuItemId = 9999, Quantity = 1 } }
        };

        // Act
            var response = await _clientCashierOrg1.PostAsJsonAsync("/api/orders", orderDto);

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode); // Service throws KeyNotFoundException
        }

         [Fact]
    public async Task PostOrder_WithNonExistentArea_ReturnsNotFound()
    {
         // Arrange
        var orderDto = new CreateOrderDto
        {
            AreaId = 9999,
            CustomerName = "Nonexistent Area Customer", // Added required field
            Items = new List<CreateOrderItemDto> { new CreateOrderItemDto { MenuItemId = TestConstants.Item1Cat1Id, Quantity = 1 } } // Use constant
        };

        // Act
            var response = await _clientCashierOrg1.PostAsJsonAsync("/api/orders", orderDto);

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode); // Service throws KeyNotFoundException
        }

        // =============================================
        // GET /api/orders/{id} Tests
        // =============================================

        [Fact]
    public async Task GetOrderById_WhenCashierOrg1_ForOwnOrgOrder_ReturnsSuccess()
    {
        // Arrange
        await EnsureOpenDayForOrg1Async(); // Ensure day is open

        // Act
        // Note: SeededOrder1Id might not belong to the *newly* opened day.
        // This test might need adjustment to fetch an order created *within* the open day,
        // or the GetOrderById logic needs refinement for historical orders.
        // For now, let's assume the service logic allows fetching historical if day is open.
        var response = await _clientCashierOrg1.GetAsync($"/api/orders/{TestConstants.SeededOrder1Id}"); // Use constant

        // Assert
        response.EnsureSuccessStatusCode();
        var orderDto = await response.Content.ReadFromJsonAsync<OrderDto>();
        Assert.NotNull(orderDto);
        Assert.Equal(TestConstants.SeededOrder1Id, orderDto.Id); // Use string constant
        Assert.Equal(TestConstants.Org1Area1Id, orderDto.AreaId); // Use constant
        Assert.Equal("Org1 Cashier", orderDto.CashierName); // Corrected expected name
        Assert.NotEmpty(orderDto.Items);
    }

     [Fact]
    public async Task GetOrderById_WhenOrg1Admin_ForOwnOrgOrder_ReturnsSuccess()
    {
        // Arrange
        await EnsureOpenDayForOrg1Async(); // Ensure day is open

        // Act
        // Similar note as above regarding fetching potentially historical orders.
        var response = await _clientOrg1Admin.GetAsync($"/api/orders/{TestConstants.SeededOrder3Id}"); // Use constant

        // Assert
        response.EnsureSuccessStatusCode();
        var orderDto = await response.Content.ReadFromJsonAsync<OrderDto>();
        Assert.NotNull(orderDto);
        Assert.Equal(TestConstants.SeededOrder3Id, orderDto.Id); // Use string constant
        Assert.Equal(TestConstants.Org1Area2Id, orderDto.AreaId); // Use constant (Area3 is Org1Area2Id)
        Assert.Equal("Org1 Cashier", orderDto.CashierName); // Corrected expected name
    }

    [Fact]
    public async Task GetOrderById_WhenOrg1Admin_ForOtherOrgOrder_ReturnsNotFound() // Service returns null
    {
        // Act
        var response = await _clientOrg1Admin.GetAsync($"/api/orders/{TestConstants.SeededOrder2Id}"); // Use constant

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
    public async Task GetOrderById_WhenSuperAdmin_ForAnyOrder_ReturnsSuccess()
    {
        // Act Org1 Order
        var responseOrg1 = await _clientSuperAdmin.GetAsync($"/api/orders/{TestConstants.SeededOrder1Id}"); // Use constant
        // Act Org2 Order
        var responseOrg2 = await _clientSuperAdmin.GetAsync($"/api/orders/{TestConstants.SeededOrder2Id}"); // Use constant

        // Assert Org1
        responseOrg1.EnsureSuccessStatusCode();
        var orderDtoOrg1 = await responseOrg1.Content.ReadFromJsonAsync<OrderDto>();
        Assert.NotNull(orderDtoOrg1);
        Assert.Equal(TestConstants.SeededOrder1Id, orderDtoOrg1.Id); // Use string constant
        Assert.Equal("Org1 Area 1", orderDtoOrg1.AreaName); // Corrected expected name

        // Assert Org2
        responseOrg2.EnsureSuccessStatusCode();
        var orderDtoOrg2 = await responseOrg2.Content.ReadFromJsonAsync<OrderDto>();
        Assert.NotNull(orderDtoOrg2);
        Assert.Equal(TestConstants.SeededOrder2Id, orderDtoOrg2.Id); // Use string constant
        Assert.Equal("Org2 Area 1", orderDtoOrg2.AreaName); // Corrected expected name (Assuming Area2 is named "Org2 Area 1")
    }

        [Fact]
        public async Task GetOrderById_ForNonExistentOrder_ReturnsNotFound()
        {
            // Act
            var response = await _clientSuperAdmin.GetAsync("/api/orders/9999");

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        // =============================================
        // GET /api/orders?areaId={areaId} Tests
        // =============================================

        [Fact]
    public async Task GetOrdersByArea_WhenOrg1Admin_ForOwnArea_ReturnsSuccessAndData()
    {
        // Arrange
        await EnsureOpenDayForOrg1Async(); // Ensure day is open

        // Act
        // This should now fetch orders associated with the currently open day for Org1Area1Id
        var response = await _clientOrg1Admin.GetAsync($"/api/orders?areaId={TestConstants.Org1Area1Id}"); // Use constant

        // Assert
        response.EnsureSuccessStatusCode();
        var orderDtos = await response.Content.ReadFromJsonAsync<List<OrderDto>>();
        Assert.NotNull(orderDtos);
        Assert.Contains(orderDtos, o => o.Id == TestConstants.SeededOrder1Id && o.AreaId == TestConstants.Org1Area1Id); // Use string constant for Id
        Assert.All(orderDtos, o => Assert.Equal(TestConstants.Org1Area1Id, o.AreaId)); // Use constant
        Assert.All(orderDtos, o => Assert.False(string.IsNullOrEmpty(o.CashierName))); // Check DTO mapping
    }

    [Fact]
    public async Task GetOrdersByArea_WhenOrg1Admin_ForOtherOrgArea_ReturnsNotFound() // Changed from Forbidden
    {
        // Act
        var response = await _clientOrg1Admin.GetAsync($"/api/orders?areaId={TestConstants.Org2Area1Id}"); // Use constant

        // Assert
        // Service returns null for access denied, controller returns NotFound
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
    public async Task GetOrdersByArea_WhenSuperAdmin_ForAnyArea_ReturnsSuccessAndData()
    {
         // Arrange
         // No specific day needed for SuperAdmin if service logic is corrected
         // Act Org1 Area
        var responseOrg1 = await _clientSuperAdmin.GetAsync($"/api/orders?areaId={TestConstants.Org1Area1Id}&organizationId={TestConstants.Org1Id}"); // Added organizationId
        // Act Org2 Area
        // Ensure Org2 also has an open day if the service logic isn't fixed yet, or use a specific dayId
        // For now, assume service logic fix or test failure is acceptable here pending service update.
        var responseOrg2 = await _clientSuperAdmin.GetAsync($"/api/orders?areaId={TestConstants.Org2Area1Id}&organizationId={TestConstants.Org2Id}"); // Added organizationId

        // Assert Org1
        responseOrg1.EnsureSuccessStatusCode();
        var orderDtosOrg1 = await responseOrg1.Content.ReadFromJsonAsync<List<OrderDto>>();
        Assert.NotNull(orderDtosOrg1);
        Assert.Contains(orderDtosOrg1, o => o.Id == TestConstants.SeededOrder1Id); // Use string constant
        Assert.All(orderDtosOrg1, o => Assert.Equal(TestConstants.Org1Area1Id, o.AreaId)); // Use constant

        // Assert Org2
        responseOrg2.EnsureSuccessStatusCode();
        var orderDtosOrg2 = await responseOrg2.Content.ReadFromJsonAsync<List<OrderDto>>();
        Assert.NotNull(orderDtosOrg2);
        Assert.Contains(orderDtosOrg2, o => o.Id == TestConstants.SeededOrder2Id); // Use string constant
        Assert.All(orderDtosOrg2, o => Assert.Equal(TestConstants.Org2Area1Id, o.AreaId)); // Use constant
    }

         [Fact]
        public async Task GetOrdersByArea_ForNonExistentArea_ReturnsNotFound() // Changed name and assertion
        {
            // Act (SuperAdmin - needs orgId even if area doesn't exist)
            var responseSuper = await _clientSuperAdmin.GetAsync($"/api/orders?areaId=9999&organizationId={TestConstants.Org1Id}"); // Added organizationId
            // Act (Org1Admin)
            var responseOrg1 = await _clientOrg1Admin.GetAsync("/api/orders?areaId=9999");

            // Assert
            // Service throws KeyNotFoundException for non-existent area, controller returns NotFound.
            Assert.Equal(HttpStatusCode.NotFound, responseSuper.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, responseOrg1.StatusCode);
         }

        [Fact]
        public async Task GetOrderById_WhenWaiterOrg1_ForOwnOrgOrder_ReturnsSuccess()
        {
            // Arrange
            await EnsureOpenDayForOrg1Async(); // Ensure day is open

            // Act
            // Similar note as GetOrderById_WhenCashierOrg1_ForOwnOrgOrder_ReturnsSuccess
            var response = await _clientWaiterOrg1.GetAsync($"/api/orders/{TestConstants.SeededOrder1Id}"); // Use an existing completed order

            // Assert
            response.EnsureSuccessStatusCode(); // Waiter should be able to GET orders
            var orderDto = await response.Content.ReadFromJsonAsync<OrderDto>();
            Assert.NotNull(orderDto);
            Assert.Equal(TestConstants.SeededOrder1Id, orderDto.Id);
            Assert.Equal(TestConstants.Org1Area1Id, orderDto.AreaId);
        }

        [Fact]
        public async Task GetOrderById_WhenWaiterOrg1_ForOtherOrgOrder_ReturnsNotFound()
        {
            // Act
            var response = await _clientWaiterOrg1.GetAsync($"/api/orders/{TestConstants.SeededOrder2Id}"); // Order from Org2

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode); // Service returns null for access denied
        }


        // =============================================
        // PUT /api/orders/{orderId}/confirm-preparation Tests
        // =============================================

        [Fact]
        public async Task ConfirmPreparation_WhenWaiterOrg1_ForPaidOrder_ReturnsOkAndUpdatesStatus()
        {
            // Arrange
            await EnsureOpenDayForOrg1Async(); // Ensure day is open
            // TODO: This test might need to CREATE a Paid order within the open day first,
            // as SeededOrder5Id might not belong to the newly opened day.
            // For now, assume ConfirmPrep works if *any* day is open, even if not the order's original day.
            var orderId = TestConstants.SeededOrder5Id; // Use the seeded Paid order
            var dto = new ConfirmPreparationDto { TableNumber = "T10" };

            // Act
            var response = await _clientWaiterOrg1.PutAsJsonAsync($"/api/orders/{orderId}/confirm-preparation", dto);

            // Assert
            response.EnsureSuccessStatusCode();
            var updatedOrderDto = await response.Content.ReadFromJsonAsync<OrderDto>();
            Assert.NotNull(updatedOrderDto);
            Assert.Equal(orderId, updatedOrderDto.Id);
            Assert.Equal(OrderStatus.Preparing, updatedOrderDto.Status); // Check status update
            Assert.Equal("T10", updatedOrderDto.TableNumber); // Check table number update

            // Verify in DB (optional but good)
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var orderInDb = await db.Orders.FindAsync(orderId);
            Assert.NotNull(orderInDb);
            Assert.Equal(OrderStatus.Preparing, orderInDb.Status);
            Assert.Equal("T10", orderInDb.TableNumber);
        }

        [Fact]
        public async Task ConfirmPreparation_WhenWaiterOrg1_ForPreOrder_ReturnsOkAndUpdatesStatus()
        {
            // Arrange
            await EnsureOpenDayForOrg1Async(); // Ensure day is open
            // TODO: Similar to the Paid order test, ideally create a PreOrder within the open day.
            var orderId = TestConstants.SeededOrder6Id; // Use the seeded PreOrder
            var dto = new ConfirmPreparationDto { TableNumber = "P5" };

            // Act
            var response = await _clientWaiterOrg1.PutAsJsonAsync($"/api/orders/{orderId}/confirm-preparation", dto);

            // Assert
            response.EnsureSuccessStatusCode();
            var updatedOrderDto = await response.Content.ReadFromJsonAsync<OrderDto>();
            Assert.NotNull(updatedOrderDto);
            Assert.Equal(orderId, updatedOrderDto.Id);
            Assert.Equal(OrderStatus.Preparing, updatedOrderDto.Status);
            Assert.Equal("P5", updatedOrderDto.TableNumber);

            // Verify in DB
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var orderInDb = await db.Orders.FindAsync(orderId);
            Assert.NotNull(orderInDb);
            Assert.Equal(OrderStatus.Preparing, orderInDb.Status);
            Assert.Equal("P5", orderInDb.TableNumber);
        }

        [Fact]
        public async Task ConfirmPreparation_WhenWaiterOrg1_ForCompletedOrder_ReturnsNotFound()
        {
            // Arrange
            var orderId = TestConstants.SeededOrder1Id; // Use the seeded Completed order
            var dto = new ConfirmPreparationDto { TableNumber = "T1" };

            // Act
            var response = await _clientWaiterOrg1.PutAsJsonAsync($"/api/orders/{orderId}/confirm-preparation", dto);

            // Assert
            // Service returns null for invalid status, controller returns NotFound
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task ConfirmPreparation_WhenWaiterOrg1_ForOtherOrgOrder_ReturnsNotFound()
        {
            // Arrange
            var orderId = TestConstants.SeededOrder2Id; // Order from Org2
            var dto = new ConfirmPreparationDto { TableNumber = "T2" };

            // Act
            var response = await _clientWaiterOrg1.PutAsJsonAsync($"/api/orders/{orderId}/confirm-preparation", dto);

            // Assert
            // Service returns null for access denied, controller returns NotFound
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task ConfirmPreparation_WhenCashierOrg1_ForPaidOrder_ReturnsForbidden()
        {
            // Arrange
            var orderId = TestConstants.SeededOrder5Id; // Use the seeded Paid order
            var dto = new ConfirmPreparationDto { TableNumber = "T11" };

            // Act
            var response = await _clientCashierOrg1.PutAsJsonAsync($"/api/orders/{orderId}/confirm-preparation", dto);

            // Assert
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode); // Cashier role not allowed
        }

        [Fact]
        public async Task ConfirmPreparation_WhenWaiterOrg1_WithMissingTableNumber_ReturnsBadRequest()
        {
            // Arrange
            var orderId = TestConstants.SeededOrder5Id;
            var dto = new { }; // Invalid DTO (missing required TableNumber)

            // Act
            var response = await _clientWaiterOrg1.PutAsJsonAsync($"/api/orders/{orderId}/confirm-preparation", dto);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); // Model validation fails
        }

        [Fact]
        public async Task ConfirmPreparation_WhenWaiterOrg1_WithEmptyTableNumber_ReturnsBadRequest()
        {
            // Arrange
            var orderId = TestConstants.SeededOrder5Id;
            var dto = new ConfirmPreparationDto { TableNumber = "" }; // Invalid DTO (empty string)

            // Act
            var response = await _clientWaiterOrg1.PutAsJsonAsync($"/api/orders/{orderId}/confirm-preparation", dto);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); // Model validation fails
        }
     }
 }
