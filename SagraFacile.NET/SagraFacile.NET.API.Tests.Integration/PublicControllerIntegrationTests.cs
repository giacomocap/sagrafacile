using SagraFacile.NET.API.DTOs;
using SagraFacile.NET.API.Models; // Added for OrderStatus
using System.Collections.Generic; // Added for List
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Xunit;
using Moq; // Added for mocking verification
using SagraFacile.NET.API.Services.Interfaces; // Added for IEmailService

namespace SagraFacile.NET.API.Tests.Integration
{
    public class PublicControllerIntegrationTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly CustomWebApplicationFactory _factory;
        private readonly HttpClient _client;

        public PublicControllerIntegrationTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
            _client = _factory.CreateClient(); // Public endpoints don't need authentication
        }

        // =============================================
        // GET /api/public/organizations/{orgSlug} Tests
        // =============================================

        [Fact]
        public async Task GetOrganizationBySlug_WithValidSlug_ReturnsOkAndData()
        {
            // Arrange
            var orgSlug = TestConstants.Org1Slug;

            // Act
            var response = await _client.GetAsync($"/api/public/organizations/{orgSlug}");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var orgDto = await response.Content.ReadFromJsonAsync<OrganizationDto>();
            Assert.NotNull(orgDto);
            Assert.Equal(TestConstants.Org1Id, orgDto.Id);
            Assert.Equal(TestConstants.Org1Name, orgDto.Name);
            Assert.Equal(orgSlug, orgDto.Slug);
        }

        [Fact]
        public async Task GetOrganizationBySlug_WithInvalidSlug_ReturnsNotFound()
        {
            // Arrange
            var invalidSlug = "invalid-slug";

            // Act
            var response = await _client.GetAsync($"/api/public/organizations/{invalidSlug}");

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        // ============================================================
        // GET /api/public/organizations/{orgSlug}/areas/{areaSlug} Tests
        // ============================================================

        [Fact]
        public async Task GetAreaBySlugs_WithValidSlugs_ReturnsOkAndData()
        {
            // Arrange
            var orgSlug = TestConstants.Org1Slug;
            var areaSlug = TestConstants.Org1Area1Slug;

            // Act
            var response = await _client.GetAsync($"/api/public/organizations/{orgSlug}/areas/{areaSlug}");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var areaDto = await response.Content.ReadFromJsonAsync<AreaDto>();
            Assert.NotNull(areaDto);
            Assert.Equal(TestConstants.Org1Area1Id, areaDto.Id);
            Assert.Equal(TestConstants.Org1Area1Name, areaDto.Name);
            Assert.Equal(areaSlug, areaDto.Slug);
            Assert.Equal(TestConstants.Org1Id, areaDto.OrganizationId);
        }

        [Fact]
        public async Task GetAreaBySlugs_WithInvalidAreaSlug_ReturnsNotFound()
        {
            // Arrange
            var orgSlug = TestConstants.Org1Slug;
            var invalidAreaSlug = "invalid-area-slug";

            // Act
            var response = await _client.GetAsync($"/api/public/organizations/{orgSlug}/areas/{invalidAreaSlug}");

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task GetAreaBySlugs_WithInvalidOrgSlug_ReturnsNotFound()
        {
            // Arrange
            var invalidOrgSlug = "invalid-org-slug";
            var areaSlug = TestConstants.Org1Area1Slug;

            // Act
            var response = await _client.GetAsync($"/api/public/organizations/{invalidOrgSlug}/areas/{areaSlug}");

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode); // Org not found first
        }

        [Fact]
        public async Task GetAreaBySlugs_WithMismatchedSlugs_ReturnsNotFound()
        {
            // Arrange
            var orgSlug = TestConstants.Org1Slug; // Valid Org 1
            var areaSlug = TestConstants.Org2Area1Slug; // Valid Area from Org 2

            // Act
            var response = await _client.GetAsync($"/api/public/organizations/{orgSlug}/areas/{areaSlug}");

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode); // Area not found within the specified Org
        }

        // ============================================================
        // GET /api/public/areas/{areaId}/menucategories Tests
        // ============================================================

        [Fact]
        public async Task GetMenuCategoriesByArea_WithValidAreaId_ReturnsOkAndData()
        {
            // Arrange
            var areaId = TestConstants.Org1Area1Id;

            // Act
            var response = await _client.GetAsync($"/api/public/areas/{areaId}/menucategories");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var categories = await response.Content.ReadFromJsonAsync<List<MenuCategoryDto>>();
            Assert.NotNull(categories);
            Assert.NotEmpty(categories);
            Assert.Contains(categories, c => c.Id == TestConstants.Category1Area1Id); // Check if seeded category is present
            Assert.All(categories, c => Assert.Equal(areaId, c.AreaId)); // Ensure all returned categories belong to the requested area
        }

        [Fact]
        public async Task GetMenuCategoriesByArea_WithInvalidAreaId_ReturnsNotFound()
        {
            // Arrange
            var invalidAreaId = 9999;

            // Act
            var response = await _client.GetAsync($"/api/public/areas/{invalidAreaId}/menucategories");

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode); // Service should return null/empty, controller returns NotFound if area doesn't exist
        }

        [Fact]
        // Renamed: Test case changed to check for non-existent ID
        public async Task GetMenuCategoriesByArea_WithNonExistentAreaId_ReturnsNotFound()
        {
            // Arrange
            var nonExistentAreaId = 9999;

            // Act
            var response = await _client.GetAsync($"/api/public/areas/{nonExistentAreaId}/menucategories");

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode); // Expect NotFound for invalid ID
        }

        // ============================================================
        // GET /api/public/menucategories/{categoryId}/menuitems Tests
        // ============================================================

        [Fact]
        public async Task GetMenuItemsByCategory_WithValidCategoryId_ReturnsOkAndData()
        {
            // Arrange
            var categoryId = TestConstants.Category1Area1Id; // Category with seeded items

            // Act
            var response = await _client.GetAsync($"/api/public/menucategories/{categoryId}/menuitems");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var items = await response.Content.ReadFromJsonAsync<List<MenuItemDto>>();
            Assert.NotNull(items);
            Assert.NotEmpty(items);
            Assert.Contains(items, i => i.Id == TestConstants.Item1Cat1Id); // Check for seeded item 1
            Assert.Contains(items, i => i.Id == TestConstants.Item2Cat1Id); // Check for seeded item 2
            Assert.All(items, i => Assert.Equal(categoryId, i.MenuCategoryId)); // Ensure all items belong to the category
        }

        [Fact]
        public async Task GetMenuItemsByCategory_WithInvalidCategoryId_ReturnsNotFound()
        {
            // Arrange
            var invalidCategoryId = 9999;

            // Act
            var response = await _client.GetAsync($"/api/public/menucategories/{invalidCategoryId}/menuitems");

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode); // Service should return null/empty, controller returns NotFound if category doesn't exist
        }

        [Fact]
        // Renamed: Test case changed to check for non-existent ID
        public async Task GetMenuItemsByCategory_WithNonExistentCategoryId_ReturnsNotFound()
        {
            // Arrange
            var nonExistentCategoryId = 9999;

            // Act
            var response = await _client.GetAsync($"/api/public/menucategories/{nonExistentCategoryId}/menuitems");

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode); // Expect NotFound for invalid ID
        }

        // =============================================
        // POST /api/public/preorders Tests
        // =============================================

        [Fact]
        public async Task PostPreOrder_WithValidData_ReturnsCreated_And_SendsEmail() // Updated test name
        {
            // Arrange
            // Reset the mock before the test
            _factory.MockEmailService.Reset();

            var preOrderDto = new PreOrderDto
            {
                AreaId = TestConstants.Org1Area1Id,
                CustomerName = "Test Customer",
                CustomerEmail = "test.customer@example.com",
                Items = new List<PreOrderItemDto>
                {
                    new PreOrderItemDto { MenuItemId = TestConstants.Item1Cat1Id, Quantity = 2 },
                    new PreOrderItemDto { MenuItemId = TestConstants.Item2Cat1Id, Quantity = 1, Note = "Test Note" }
                }
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/public/preorders", preOrderDto);

            // Assert
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var createdOrder = await response.Content.ReadFromJsonAsync<OrderDto>(); // Assuming it returns the created OrderDto
            Assert.NotNull(createdOrder);
            Assert.False(string.IsNullOrEmpty(createdOrder.Id)); // Check if string ID is generated
            Assert.Equal(preOrderDto.AreaId, createdOrder.AreaId);
            Assert.Equal(preOrderDto.CustomerName, createdOrder.CustomerName);
            Assert.Equal(preOrderDto.CustomerEmail, createdOrder.CustomerEmail);
            Assert.Equal(OrderStatus.PreOrder, createdOrder.Status); // Check status
            Assert.Null(createdOrder.CashierId); // Cashier should be null for pre-orders
            Assert.Null(createdOrder.CashierName);
            Assert.Equal(2, createdOrder.Items.Count);
            Assert.Contains(createdOrder.Items, i => i.MenuItemId == TestConstants.Item1Cat1Id && i.Quantity == 2);
            Assert.Contains(createdOrder.Items, i => i.MenuItemId == TestConstants.Item2Cat1Id && i.Quantity == 1 && i.Note == "Test Note");

            // Assert: Verify email was sent via the mock
            _factory.MockEmailService.Verify(
                x => x.SendEmailAsync(
                    It.Is<string>(email => email == preOrderDto.CustomerEmail), // Check recipient
                    It.Is<string>(subject => subject.Contains("Conferma Pre-Ordine") && subject.Contains(createdOrder.Id)), // Check subject contains key info (using Id now)
                    It.Is<string>(body => body.Contains(preOrderDto.CustomerName) && body.Contains("data:image/png;base64,")) // Check body contains name and QR image tag
                ),
                Times.Once // Ensure it was called exactly once
            );

            // TODO: Add cleanup if necessary (deleting created pre-order)
            // Consider adding a helper to delete the order by ID using the DbContext from the factory's scope if needed.
        }

        [Fact]
        public async Task PostPreOrder_WithInvalidAreaId_ReturnsNotFound()
        {
            // Arrange
            var preOrderDto = new PreOrderDto
            {
                AreaId = 9999, // Invalid Area ID
                CustomerName = "Test Customer",
                CustomerEmail = "test.customer@example.com",
                Items = new List<PreOrderItemDto> { new PreOrderItemDto { MenuItemId = TestConstants.Item1Cat1Id, Quantity = 1 } }
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/public/preorders", preOrderDto);

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode); // Service should throw KeyNotFoundException for Area
        }

        [Fact]
        public async Task PostPreOrder_WithInvalidMenuItemId_ReturnsNotFound()
        {
            // Arrange
            var preOrderDto = new PreOrderDto
            {
                AreaId = TestConstants.Org1Area1Id,
                CustomerName = "Test Customer",
                CustomerEmail = "test.customer@example.com",
                Items = new List<PreOrderItemDto> { new PreOrderItemDto { MenuItemId = 9999, Quantity = 1 } } // Invalid Item ID
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/public/preorders", preOrderDto);

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode); // Service should throw KeyNotFoundException for MenuItem
        }

        [Fact]
        public async Task PostPreOrder_WithMenuItemFromDifferentArea_ReturnsBadRequest()
        {
            // Arrange
            var preOrderDto = new PreOrderDto
            {
                AreaId = TestConstants.Org1Area1Id, // Area from Org 1
                CustomerName = "Test Customer",
                CustomerEmail = "test.customer@example.com",
                Items = new List<PreOrderItemDto> { new PreOrderItemDto { MenuItemId = TestConstants.Item3Cat2Id, Quantity = 1 } } // Item from Org 2
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/public/preorders", preOrderDto);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); // Service should throw InvalidOperationException ("MenuItem ID X does not belong...")
        }

        [Fact]
        public async Task PostPreOrder_WithMissingCustomerName_ReturnsBadRequest()
        {
            // Arrange
            var preOrderDto = new PreOrderDto
            {
                AreaId = TestConstants.Org1Area1Id,
                CustomerName = "", // Missing Name
                CustomerEmail = "test.customer@example.com",
                Items = new List<PreOrderItemDto> { new PreOrderItemDto { MenuItemId = TestConstants.Item1Cat1Id, Quantity = 1 } }
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/public/preorders", preOrderDto);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); // Model validation failure
        }

        [Fact]
        public async Task PostPreOrder_WithMissingCustomerEmail_ReturnsBadRequest()
        {
            // Arrange
            var preOrderDto = new PreOrderDto
            {
                AreaId = TestConstants.Org1Area1Id,
                CustomerName = "Test Customer",
                CustomerEmail = "", // Missing Email
                Items = new List<PreOrderItemDto> { new PreOrderItemDto { MenuItemId = TestConstants.Item1Cat1Id, Quantity = 1 } }
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/public/preorders", preOrderDto);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); // Model validation failure
        }

         [Fact]
        public async Task PostPreOrder_WithInvalidCustomerEmail_ReturnsBadRequest()
        {
            // Arrange
            var preOrderDto = new PreOrderDto
            {
                AreaId = TestConstants.Org1Area1Id,
                CustomerName = "Test Customer",
                CustomerEmail = "invalid-email", // Invalid Email Format
                Items = new List<PreOrderItemDto> { new PreOrderItemDto { MenuItemId = TestConstants.Item1Cat1Id, Quantity = 1 } }
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/public/preorders", preOrderDto);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); // Model validation failure
        }

        [Fact]
        public async Task PostPreOrder_WithEmptyItemsList_ReturnsBadRequest()
        {
            // Arrange
            var preOrderDto = new PreOrderDto
            {
                AreaId = TestConstants.Org1Area1Id,
                CustomerName = "Test Customer",
                CustomerEmail = "test.customer@example.com",
                Items = new List<PreOrderItemDto>() // Empty list
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/public/preorders", preOrderDto);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); // Model validation failure (or potentially service validation)
        }

        // Remove the TODO as all endpoints are now covered
    }
}
