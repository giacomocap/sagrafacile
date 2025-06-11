using SagraFacile.NET.API.Models;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Xunit;

namespace SagraFacile.NET.API.Tests.Integration
{
    public class MenuItemIntegrationTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly CustomWebApplicationFactory _factory;
        private readonly HttpClient _clientSuperAdmin;
        private readonly HttpClient _clientOrg1Admin;
        private readonly HttpClient _clientOrg2Admin;
        private readonly HttpClient _clientCashierOrg1;

        // Seeded IDs from CustomWebApplicationFactory - Replaced by TestConstants
        // private const int Org1Id = 1;
        // ... etc ...

        public MenuItemIntegrationTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
            // Use constants for client creation
            _clientSuperAdmin = _factory.CreateAuthenticatedClientAsync(TestConstants.SuperAdminEmail).Result;
            _clientOrg1Admin = _factory.CreateAuthenticatedClientAsync(TestConstants.Org1AdminEmail).Result;
            _clientOrg2Admin = _factory.CreateAuthenticatedClientAsync(TestConstants.Org2AdminEmail).Result;
            _clientCashierOrg1 = _factory.CreateAuthenticatedClientAsync(TestConstants.Org1CashierEmail).Result;
        }

        // =============================================
        // GET /api/menuitems?categoryId={categoryId} Tests
        // =============================================

        [Fact]
    public async Task GetItemsByCategory_WhenOrg1Admin_ForOwnCategory_ReturnsSuccessAndData()
    {
        // Act
        var response = await _clientOrg1Admin.GetAsync($"/api/menuitems?categoryId={TestConstants.Category1Area1Id}"); // Use constant

        // Assert
        response.EnsureSuccessStatusCode();
        var items = await response.Content.ReadFromJsonAsync<List<MenuItem>>();
        Assert.NotNull(items);
        Assert.Contains(items, i => i.Id == TestConstants.Item1Cat1Id && i.MenuCategoryId == TestConstants.Category1Area1Id); // Use constants
        Assert.Contains(items, i => i.Id == TestConstants.Item2Cat1Id && i.MenuCategoryId == TestConstants.Category1Area1Id); // Use constants
        Assert.DoesNotContain(items, i => i.Id == TestConstants.Item3Cat2Id); // Use constant
    }

    [Fact]
    public async Task GetItemsByCategory_WhenOrg1Admin_ForOtherOrgCategory_ReturnsForbidden()
    {
        // Act
        var response = await _clientOrg1Admin.GetAsync($"/api/menuitems?categoryId={TestConstants.Category2Area2Id}"); // Use constant (Corrected Category ID for Org2)

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
    public async Task GetItemsByCategory_WhenSuperAdmin_ForAnyCategory_ReturnsSuccessAndData()
    {
        // Act Org1 Category
        var responseOrg1 = await _clientSuperAdmin.GetAsync($"/api/menuitems?categoryId={TestConstants.Category1Area1Id}"); // Use constant
        // Act Org2 Category
        var responseOrg2 = await _clientSuperAdmin.GetAsync($"/api/menuitems?categoryId={TestConstants.Category2Area2Id}"); // Use constant (Corrected Category ID for Org2)

        // Assert Org1
        responseOrg1.EnsureSuccessStatusCode();
        var itemsOrg1 = await responseOrg1.Content.ReadFromJsonAsync<List<MenuItem>>();
        Assert.NotNull(itemsOrg1);
        Assert.Contains(itemsOrg1, i => i.Id == TestConstants.Item1Cat1Id); // Use constant

        // Assert Org2
        responseOrg2.EnsureSuccessStatusCode();
        var itemsOrg2 = await responseOrg2.Content.ReadFromJsonAsync<List<MenuItem>>();
        Assert.NotNull(itemsOrg2);
        Assert.Contains(itemsOrg2, i => i.Id == TestConstants.Item3Cat2Id); // Use constant
        }

        [Fact]
    public async Task GetItemsByCategory_WhenCashier_ReturnsOk() // Controller allows Cashier/Waiter
    {
        // Act
        var response = await _clientCashierOrg1.GetAsync($"/api/menuitems?categoryId={TestConstants.Category1Area1Id}"); // Use constant

        // Assert
        response.EnsureSuccessStatusCode(); // Should be OK
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = await response.Content.ReadFromJsonAsync<List<MenuItem>>(); // Check DTO if service returns it
        Assert.NotNull(items);
        // Optionally check if expected items are returned
        Assert.Contains(items, i => i.Id == TestConstants.Item1Cat1Id);
    }

        [Fact]
        public async Task GetItemsByCategory_ForNonExistentCategory_ReturnsNotFoundOrForbidden()
        {
            // Act (SuperAdmin)
            var responseSuper = await _clientSuperAdmin.GetAsync("/api/menuitems?categoryId=9999");
            // Act (Org1Admin)
            var responseOrg1 = await _clientOrg1Admin.GetAsync("/api/menuitems?categoryId=9999");

            // Assert
            // Service now throws KeyNotFoundException if category doesn't exist, controller returns NotFound.
            Assert.Equal(HttpStatusCode.NotFound, responseSuper.StatusCode);

            // Org1Admin trying to access non-existent category also results in NotFound.
            Assert.Equal(HttpStatusCode.NotFound, responseOrg1.StatusCode);
        }

        // =============================================
        // GET /api/menuitems/{id} Tests
        // =============================================

        [Fact]
    public async Task GetItemById_WhenOrg1Admin_ForOwnItem_ReturnsSuccessAndData()
    {
        // Act
        var response = await _clientOrg1Admin.GetAsync($"/api/menuitems/{TestConstants.Item1Cat1Id}"); // Use constant

        // Assert
        response.EnsureSuccessStatusCode();
        var item = await response.Content.ReadFromJsonAsync<MenuItem>();
        Assert.NotNull(item);
        Assert.Equal(TestConstants.Item1Cat1Id, item.Id); // Use constant
        Assert.Equal(TestConstants.Category1Area1Id, item.MenuCategoryId); // Use constant
    }

    [Fact]
    public async Task GetItemById_WhenOrg1Admin_ForOtherOrgItem_ReturnsNotFound() // Service returns null
    {
        // Act
        var response = await _clientOrg1Admin.GetAsync($"/api/menuitems/{TestConstants.Item3Cat2Id}"); // Use constant

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
    public async Task GetItemById_WhenSuperAdmin_ForAnyItem_ReturnsSuccessAndData()
    {
        // Act Org1 Item
        var responseOrg1 = await _clientSuperAdmin.GetAsync($"/api/menuitems/{TestConstants.Item1Cat1Id}"); // Use constant
        // Act Org2 Item
        var responseOrg2 = await _clientSuperAdmin.GetAsync($"/api/menuitems/{TestConstants.Item3Cat2Id}"); // Use constant

        // Assert Org1
        responseOrg1.EnsureSuccessStatusCode();
        var itemOrg1 = await responseOrg1.Content.ReadFromJsonAsync<MenuItem>();
        Assert.NotNull(itemOrg1);
        Assert.Equal(TestConstants.Item1Cat1Id, itemOrg1.Id); // Use constant

        // Assert Org2
        responseOrg2.EnsureSuccessStatusCode();
        var itemOrg2 = await responseOrg2.Content.ReadFromJsonAsync<MenuItem>();
        Assert.NotNull(itemOrg2);
        Assert.Equal(TestConstants.Item3Cat2Id, itemOrg2.Id); // Use constant
        }

        [Fact]
    public async Task GetItemById_WhenCashier_ReturnsOk() // Controller allows Cashier/Waiter
    {
        // Act
        var response = await _clientCashierOrg1.GetAsync($"/api/menuitems/{TestConstants.Item1Cat1Id}"); // Use constant

        // Assert
        response.EnsureSuccessStatusCode(); // Should be OK
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var item = await response.Content.ReadFromJsonAsync<MenuItem>(); // Check DTO if service returns it
        Assert.NotNull(item);
        Assert.Equal(TestConstants.Item1Cat1Id, item.Id);
    }

        [Fact]
        public async Task GetItemById_ForNonExistentItem_ReturnsNotFound()
        {
            // Act
            var response = await _clientSuperAdmin.GetAsync("/api/menuitems/9999");

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        // =============================================
        // POST /api/menuitems Tests
        // =============================================

        [Fact]
    public async Task PostItem_WhenOrg1Admin_ForOwnCategory_ReturnsCreated()
    {
        // Arrange
        var newItem = new MenuItem { Name = "Test Post Item Org1", Price = 5.50m, MenuCategoryId = TestConstants.Category1Area1Id }; // Use constant

        // Act
        var response = await _clientOrg1Admin.PostAsJsonAsync("/api/menuitems", newItem);

            // Assert
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var createdItem = await response.Content.ReadFromJsonAsync<MenuItem>();
            Assert.NotNull(createdItem);
            Assert.Equal(newItem.Name, createdItem.Name);
            Assert.Equal(newItem.Price, createdItem.Price);
            Assert.Equal(newItem.MenuCategoryId, createdItem.MenuCategoryId);
            Assert.True(createdItem.Id > 0);

            // Cleanup
            await _clientOrg1Admin.DeleteAsync($"/api/menuitems/{createdItem.Id}");
        }

        [Fact]
    public async Task PostItem_WhenSuperAdmin_ForAnyCategory_ReturnsCreated()
    {
        // Arrange
        var newItem = new MenuItem { Name = "Test Post Item SuperAdmin", Price = 12.00m, MenuCategoryId = TestConstants.Category2Area2Id }; // Use constant (Corrected Category ID for Org2)

        // Act
        var response = await _clientSuperAdmin.PostAsJsonAsync("/api/menuitems", newItem);

            // Assert
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var createdItem = await response.Content.ReadFromJsonAsync<MenuItem>();
            Assert.NotNull(createdItem);
            Assert.Equal(newItem.Name, createdItem.Name);
            Assert.Equal(newItem.MenuCategoryId, createdItem.MenuCategoryId);

            // Cleanup
            await _clientSuperAdmin.DeleteAsync($"/api/menuitems/{createdItem.Id}");
        }

        [Fact]
    public async Task PostItem_WhenOrg1Admin_ForOtherOrgCategory_ReturnsForbidden()
    {
        // Arrange
        var newItem = new MenuItem { Name = "Test Post Item Fail", Price = 1.00m, MenuCategoryId = TestConstants.Category2Area2Id }; // Use constant (Corrected Category ID for Org2)

        // Act
        var response = await _clientOrg1Admin.PostAsJsonAsync("/api/menuitems", newItem);

            // Assert
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task PostItem_WhenOrg1Admin_ForNonExistentCategory_ReturnsNotFound() // Service throws KeyNotFoundException
        {
            // Arrange
            var newItem = new MenuItem { Name = "Test Post Item Fail", Price = 1.00m, MenuCategoryId = 9999 };

            // Act
            var response = await _clientOrg1Admin.PostAsJsonAsync("/api/menuitems", newItem);

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode); // Controller should handle KeyNotFoundException
        }

        [Fact]
    public async Task PostItem_WhenCashier_ReturnsForbidden()
    {
        // Arrange
        var newItem = new MenuItem { Name = "Test Post Item Cashier", Price = 1.00m, MenuCategoryId = TestConstants.Category1Area1Id }; // Use constant

        // Act
        var response = await _clientCashierOrg1.PostAsJsonAsync("/api/menuitems", newItem);

            // Assert
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        // =============================================
        // PUT /api/menuitems/{id} Tests
        // =============================================

        [Fact]
    public async Task PutItem_WhenOrg1Admin_ForOwnItem_ReturnsNoContent()
    {
        // Arrange: Create an item to update
        var postItem = new MenuItem { Name = "Item to Update Org1", Price = 10.0m, MenuCategoryId = TestConstants.Category1Area1Id }; // Use constant
        var postResponse = await _clientOrg1Admin.PostAsJsonAsync("/api/menuitems", postItem);
        postResponse.EnsureSuccessStatusCode();
        var createdItem = await postResponse.Content.ReadFromJsonAsync<MenuItem>();
        Assert.NotNull(createdItem);

        var updatedItemData = new MenuItem { Id = createdItem.Id, Name = "Updated Item Org1", Price = 11.5m, MenuCategoryId = TestConstants.Category1Area1Id }; // Use constant

        // Act
        var putResponse = await _clientOrg1Admin.PutAsJsonAsync($"/api/menuitems/{createdItem.Id}", updatedItemData);

            // Assert
            Assert.Equal(HttpStatusCode.NoContent, putResponse.StatusCode);

            // Verify update
            var getResponse = await _clientOrg1Admin.GetAsync($"/api/menuitems/{createdItem.Id}");
            getResponse.EnsureSuccessStatusCode();
            var fetchedItem = await getResponse.Content.ReadFromJsonAsync<MenuItem>();
            Assert.NotNull(fetchedItem);
            Assert.Equal(updatedItemData.Name, fetchedItem.Name);
            Assert.Equal(updatedItemData.Price, fetchedItem.Price);

            // Cleanup
            await _clientOrg1Admin.DeleteAsync($"/api/menuitems/{createdItem.Id}");
        }

        [Fact]
    public async Task PutItem_WhenSuperAdmin_ForAnyItem_ReturnsNoContent()
    {
        // Arrange: Use seeded item from Org2
        var itemToUpdateId = TestConstants.Item3Cat2Id; // Use constant
        var updatedItemData = new MenuItem { Id = itemToUpdateId, Name = "Updated By SuperAdmin", Price = 6.0m, MenuCategoryId = TestConstants.Category2Area2Id }; // Use constant (Corrected Category ID for Org2)

        // Act
        var putResponse = await _clientSuperAdmin.PutAsJsonAsync($"/api/menuitems/{itemToUpdateId}", updatedItemData);

            // Assert
            Assert.Equal(HttpStatusCode.NoContent, putResponse.StatusCode);

            // Verify update
            var getResponse = await _clientSuperAdmin.GetAsync($"/api/menuitems/{itemToUpdateId}");
            getResponse.EnsureSuccessStatusCode();
            var fetchedItem = await getResponse.Content.ReadFromJsonAsync<MenuItem>();
            Assert.NotNull(fetchedItem);
            Assert.Equal(updatedItemData.Name, fetchedItem.Name);

            // Note: No cleanup needed as we modified seeded data. Consider resetting if needed.
        }

        [Fact]
    public async Task PutItem_WhenOrg1Admin_ForOtherOrgItem_ReturnsForbidden()
    {
        // Arrange
        var itemToUpdateId = TestConstants.Item3Cat2Id; // Use constant
        var updatedItemData = new MenuItem { Id = itemToUpdateId, Name = "Update Fail", Price = 1.0m, MenuCategoryId = TestConstants.Category2Area2Id }; // Use constant (Corrected Category ID for Org2)

        // Act
        var putResponse = await _clientOrg1Admin.PutAsJsonAsync($"/api/menuitems/{itemToUpdateId}", updatedItemData);

            // Assert
            Assert.Equal(HttpStatusCode.Forbidden, putResponse.StatusCode);
        }

        [Fact]
    public async Task PutItem_WhenOrg1Admin_MoveToOtherOrgCategory_ReturnsForbidden()
    {
        // Arrange: Use seeded item from Org1
        var itemToUpdateId = TestConstants.Item1Cat1Id; // Use constant
        var updatedItemData = new MenuItem { Id = itemToUpdateId, Name = "Move Fail", Price = 1.0m, MenuCategoryId = TestConstants.Category2Area2Id }; // Use constant (Corrected Category ID for Org2)

        // Act
        var putResponse = await _clientOrg1Admin.PutAsJsonAsync($"/api/menuitems/{itemToUpdateId}", updatedItemData);

            // Assert
            Assert.Equal(HttpStatusCode.Forbidden, putResponse.StatusCode);
        }

        [Fact]
    public async Task PutItem_ForNonExistentItem_ReturnsNotFound()
    {
        // Arrange
        var updatedItemData = new MenuItem { Id = 9999, Name = "Update NonExistent", Price = 1.0m, MenuCategoryId = TestConstants.Category1Area1Id }; // Use constant

        // Act
        var putResponse = await _clientSuperAdmin.PutAsJsonAsync("/api/menuitems/9999", updatedItemData);

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, putResponse.StatusCode);
        }

        [Fact]
    public async Task PutItem_WhenCashier_ReturnsForbidden()
    {
        // Arrange
        var itemToUpdateId = TestConstants.Item1Cat1Id; // Use constant
        var updatedItemData = new MenuItem { Id = itemToUpdateId, Name = "Update Fail Cashier", Price = 1.0m, MenuCategoryId = TestConstants.Category1Area1Id }; // Use constant

        // Act
        var putResponse = await _clientCashierOrg1.PutAsJsonAsync($"/api/menuitems/{itemToUpdateId}", updatedItemData);

            // Assert
            Assert.Equal(HttpStatusCode.Forbidden, putResponse.StatusCode);
        }

        // =============================================
        // DELETE /api/menuitems/{id} Tests
        // =============================================

        [Fact]
    public async Task DeleteItem_WhenOrg1Admin_ForOwnItem_ReturnsNoContent()
    {
        // Arrange: Create an item to delete
        var postItem = new MenuItem { Name = "Item to Delete Org1", Price = 1.0m, MenuCategoryId = TestConstants.Category1Area1Id }; // Use constant
        var postResponse = await _clientOrg1Admin.PostAsJsonAsync("/api/menuitems", postItem);
        postResponse.EnsureSuccessStatusCode();
        var createdItem = await postResponse.Content.ReadFromJsonAsync<MenuItem>();
            Assert.NotNull(createdItem);

            // Act
            var deleteResponse = await _clientOrg1Admin.DeleteAsync($"/api/menuitems/{createdItem.Id}");

            // Assert
            Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

            // Verify deletion
            var getResponse = await _clientOrg1Admin.GetAsync($"/api/menuitems/{createdItem.Id}");
            Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
        }

        [Fact]
    public async Task DeleteItem_WhenSuperAdmin_ForAnyItem_ReturnsNoContent()
    {
        // Arrange: Create an item in Org2 to delete
        var postItem = new MenuItem { Name = "Item to Delete SuperAdmin", Price = 1.0m, MenuCategoryId = TestConstants.Category2Area2Id }; // Use constant (Corrected Category ID for Org2)
        var postResponse = await _clientSuperAdmin.PostAsJsonAsync("/api/menuitems", postItem);
        postResponse.EnsureSuccessStatusCode();
        var createdItem = await postResponse.Content.ReadFromJsonAsync<MenuItem>();
            Assert.NotNull(createdItem);

            // Act
            var deleteResponse = await _clientSuperAdmin.DeleteAsync($"/api/menuitems/{createdItem.Id}");

            // Assert
            Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

            // Verify deletion
            var getResponse = await _clientSuperAdmin.GetAsync($"/api/menuitems/{createdItem.Id}");
            Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
        }

        [Fact]
    public async Task DeleteItem_WhenOrg1Admin_ForOtherOrgItem_ReturnsForbidden()
    {
        // Arrange: Use seeded item from Org2
        var itemToDeleteId = TestConstants.Item3Cat2Id; // Use constant

        // Act
        var deleteResponse = await _clientOrg1Admin.DeleteAsync($"/api/menuitems/{itemToDeleteId}");

            // Assert
            Assert.Equal(HttpStatusCode.Forbidden, deleteResponse.StatusCode);
        }

        [Fact]
        public async Task DeleteItem_ForNonExistentItem_ReturnsNotFound()
        {
            // Act
            var deleteResponse = await _clientSuperAdmin.DeleteAsync("/api/menuitems/9999");

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, deleteResponse.StatusCode);
        }

        [Fact]
    public async Task DeleteItem_WhenCashier_ReturnsForbidden()
    {
        // Arrange: Use seeded item from Org1
        var itemToDeleteId = TestConstants.Item1Cat1Id; // Use constant

        // Act
        var deleteResponse = await _clientCashierOrg1.DeleteAsync($"/api/menuitems/{itemToDeleteId}");

            // Assert
            Assert.Equal(HttpStatusCode.Forbidden, deleteResponse.StatusCode);
        }

        // TODO: Add test for deleting an item that is part of an Order (should fail if FK constraint exists)
    }
}
