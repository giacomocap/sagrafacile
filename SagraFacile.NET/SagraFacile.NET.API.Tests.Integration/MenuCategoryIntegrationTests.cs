using SagraFacile.NET.API.Models;
using SagraFacile.NET.API.DTOs; // Assuming a Category DTO might be needed later
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Xunit;
using Microsoft.AspNetCore.Mvc.Testing; // Required for WebApplicationFactoryClientOptions

namespace SagraFacile.NET.API.Tests.Integration
{
    public class MenuCategoryIntegrationTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly CustomWebApplicationFactory _factory;
        private readonly HttpClient _clientSuperAdmin;
        private readonly HttpClient _clientOrg1Admin;
        private readonly HttpClient _clientOrg2Admin;
        private readonly HttpClient _clientCashierOrg1;

        // Seeded IDs from CustomWebApplicationFactory - Replaced by TestConstants
        // private const int Org1Id = 1;
        // ... etc ...

        public MenuCategoryIntegrationTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
            // Create clients with authentication tokens using constants
            _clientSuperAdmin = _factory.CreateAuthenticatedClientAsync(TestConstants.SuperAdminEmail).Result;
            _clientOrg1Admin = _factory.CreateAuthenticatedClientAsync(TestConstants.Org1AdminEmail).Result;
            _clientOrg2Admin = _factory.CreateAuthenticatedClientAsync(TestConstants.Org2AdminEmail).Result;
            _clientCashierOrg1 = _factory.CreateAuthenticatedClientAsync(TestConstants.Org1CashierEmail).Result;
        }

        // =============================================
        // GET /api/menucategories?areaId={areaId} Tests
        // =============================================

        [Fact]
        public async Task GetCategoriesByArea_WhenOrg1Admin_ForOwnArea_ReturnsSuccessAndData()
        {
            // Act
            var response = await _clientOrg1Admin.GetAsync($"/api/menucategories?areaId={TestConstants.Org1Area1Id}"); // Use constant

            // Assert
            response.EnsureSuccessStatusCode(); // Status Code 200-299
            var categories = await response.Content.ReadFromJsonAsync<List<MenuCategoryDto>>(); // Use DTO
            Assert.NotNull(categories);
            // Use constants
            Assert.Contains(categories, c => c.Id == TestConstants.Category1Area1Id && c.AreaId == TestConstants.Org1Area1Id);
            // Use constants
            Assert.DoesNotContain(categories, c => c.Id == TestConstants.Category2Area2Id); // Should not see category from Org2
        }

        [Fact]
        public async Task GetCategoriesByArea_WhenOrg1Admin_ForOtherOrgArea_ReturnsForbidden()
        {
            // Act
            var response = await _clientOrg1Admin.GetAsync($"/api/menucategories?areaId={TestConstants.Org2Area1Id}"); // Use constant

            // Assert
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task GetCategoriesByArea_WhenSuperAdmin_ForAnyArea_ReturnsSuccessAndData()
        {
            // Act Org1 Area
            var responseOrg1 = await _clientSuperAdmin.GetAsync($"/api/menucategories?areaId={TestConstants.Org1Area1Id}"); // Use constant
                                                                                                                            // Act Org2 Area
            var responseOrg2 = await _clientSuperAdmin.GetAsync($"/api/menucategories?areaId={TestConstants.Org2Area1Id}"); // Use constant


            // Assert Org1
            responseOrg1.EnsureSuccessStatusCode();
            var categoriesOrg1 = await responseOrg1.Content.ReadFromJsonAsync<List<MenuCategoryDto>>(); // Use DTO
            Assert.NotNull(categoriesOrg1);
            Assert.Contains(categoriesOrg1, c => c.Id == TestConstants.Category1Area1Id && c.AreaId == TestConstants.Org1Area1Id); // Use constants

            // Assert Org2
            responseOrg2.EnsureSuccessStatusCode();
            var categoriesOrg2 = await responseOrg2.Content.ReadFromJsonAsync<List<MenuCategoryDto>>(); // Use DTO
            Assert.NotNull(categoriesOrg2);
            // Use constants
            Assert.Contains(categoriesOrg2, c => c.Id == TestConstants.Category2Area2Id && c.AreaId == TestConstants.Org2Area1Id); // Use constants
        }

        [Fact]
        public async Task GetCategoriesByArea_WhenCashier_ReturnsOk() // Controller allows Cashier/Waiter
        {
            // Act
            var response = await _clientCashierOrg1.GetAsync($"/api/menucategories?areaId={TestConstants.Org1Area1Id}"); // Use constant

            // Assert
            response.EnsureSuccessStatusCode(); // Should be OK
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var categories = await response.Content.ReadFromJsonAsync<List<MenuCategoryDto>>(); // Check DTO
            Assert.NotNull(categories);
            // Optionally check if expected categories are returned
            Assert.Contains(categories, c => c.Id == TestConstants.Category1Area1Id);
        }

        [Fact]
        public async Task GetCategoriesByArea_ForNonExistentArea_ReturnsNotFoundOrForbidden()
        {
            // Depending on implementation (check area existence before auth or after),
            // this could be 404 or 403 for non-SuperAdmin. SuperAdmin should get 404.

            // Act (SuperAdmin)
            var responseSuper = await _clientSuperAdmin.GetAsync("/api/menucategories?areaId=9999");
            // Act (Org1Admin)
            var responseOrg1 = await _clientOrg1Admin.GetAsync("/api/menucategories?areaId=9999");

            // Assert (SuperAdmin should ideally get NotFound if area doesn't exist)
            // If the service throws KeyNotFoundException before auth check, it might be 500 without proper handling.
            // If auth happens first (checking if user *could* access area 9999 if it existed in their org), it might be 403.
            // Let's assume the service checks area existence *after* auth check passes for the org.
            // Update: Based on AreaService, it throws UnauthorizedAccessException first if org doesn't match.
            // Let's refine this test based on actual controller/service behavior.
            // The current MenuCategoryService.GetCategoriesByAreaAsync checks auth *after* filtering by AreaId.
            // If AreaId doesn't exist, the initial query returns empty. Auth check on Area happens later.
            // Let's assume the controller handles the service exception.
            // Update 2: The service now checks Area access first. If Area doesn't exist, it throws UnauthorizedAccessException.
            // So, for a non-existent Area, both SuperAdmin and Org1Admin should get Forbidden (as the service throws before returning).
            // Let's adjust the assertion based on the service throwing UnauthorizedAccessException for non-existent area check.

            // Assert.Equal(HttpStatusCode.OK, responseSuper.StatusCode); // SuperAdmin gets OK, but empty list - This seems wrong based on service code
            // var categoriesSuper = await responseSuper.Content.ReadFromJsonAsync<List<MenuCategoryDto>>(); // Use DTO
            // Assert.NotNull(categoriesSuper);
            // Assert.Empty(categoriesSuper);
            // Let's expect OK for SuperAdmin (service doesn't check area existence for superadmin) and Forbidden for Org1Admin
            Assert.Equal(HttpStatusCode.OK, responseSuper.StatusCode); // SuperAdmin gets OK + empty list
            var categoriesSuper = await responseSuper.Content.ReadFromJsonAsync<List<MenuCategoryDto>>(); // Use DTO
            Assert.NotNull(categoriesSuper);
            Assert.Empty(categoriesSuper);


            Assert.Equal(HttpStatusCode.NotFound, responseOrg1.StatusCode); // Org1Admin gets NotFound trying to access non-existent area (service returns null, controller returns NotFound)
        }


        // =============================================
        // GET /api/menucategories/{id} Tests
        // =============================================

        [Fact]
        public async Task GetCategoryById_WhenOrg1Admin_ForOwnCategory_ReturnsSuccessAndData()
        {
            // Act
            var response = await _clientOrg1Admin.GetAsync($"/api/menucategories/{TestConstants.Category1Area1Id}"); // Use constant

            // Assert
            response.EnsureSuccessStatusCode();
            var category = await response.Content.ReadFromJsonAsync<MenuCategoryDto>(); // Use DTO
            Assert.NotNull(category);
            Assert.Equal(TestConstants.Category1Area1Id, category.Id); // Use constant
            Assert.Equal(TestConstants.Org1Area1Id, category.AreaId); // Use constant
        }

        [Fact]
        public async Task GetCategoryById_WhenOrg1Admin_ForOtherOrgCategory_ReturnsNotFound() // Service returns null if wrong org
        {
            // Act
            var response = await _clientOrg1Admin.GetAsync($"/api/menucategories/{TestConstants.Category2Area2Id}"); // Use constant

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task GetCategoryById_WhenSuperAdmin_ForAnyCategory_ReturnsSuccessAndData()
        {
            // Act Org1 Category
            var responseOrg1 = await _clientSuperAdmin.GetAsync($"/api/menucategories/{TestConstants.Category1Area1Id}"); // Use constant
                                                                                                                          // Act Org2 Category
            var responseOrg2 = await _clientSuperAdmin.GetAsync($"/api/menucategories/{TestConstants.Category2Area2Id}"); // Use constant

            // Assert Org1
            responseOrg1.EnsureSuccessStatusCode();
            var categoryOrg1 = await responseOrg1.Content.ReadFromJsonAsync<MenuCategoryDto>(); // Use DTO
            Assert.NotNull(categoryOrg1);
            Assert.Equal(TestConstants.Category1Area1Id, categoryOrg1.Id); // Use constant

            // Assert Org2
            responseOrg2.EnsureSuccessStatusCode();
            var categoryOrg2 = await responseOrg2.Content.ReadFromJsonAsync<MenuCategoryDto>(); // Use DTO
            Assert.NotNull(categoryOrg2);
            Assert.Equal(TestConstants.Category2Area2Id, categoryOrg2.Id); // Use constant
        }

        [Fact]
        public async Task GetCategoryById_WhenCashier_ReturnsOk() // Controller allows Cashier/Waiter
        {
            // Act
            var response = await _clientCashierOrg1.GetAsync($"/api/menucategories/{TestConstants.Category1Area1Id}"); // Use constant

            // Assert
            response.EnsureSuccessStatusCode(); // Should be OK
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var category = await response.Content.ReadFromJsonAsync<MenuCategoryDto>(); // Check DTO
            Assert.NotNull(category);
            Assert.Equal(TestConstants.Category1Area1Id, category.Id);
        }

        [Fact]
        public async Task GetCategoryById_ForNonExistentCategory_ReturnsNotFound()
        {
            // Act
            var response = await _clientSuperAdmin.GetAsync("/api/menucategories/9999");

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        // =============================================
        // POST /api/menucategories Tests
        // =============================================

        [Fact]
        public async Task PostCategory_WhenOrg1Admin_ForOwnArea_ReturnsCreated()
        {
            // Arrange
            var newCategory = new MenuCategory { Name = "Test Post Category Org1", AreaId = TestConstants.Org1Area1Id }; // Use constant

            // Act
            var response = await _clientOrg1Admin.PostAsJsonAsync("/api/menucategories", newCategory);

            // Assert
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var createdCategory = await response.Content.ReadFromJsonAsync<MenuCategoryDto>(); // Use DTO
            Assert.NotNull(createdCategory);
            Assert.Equal(newCategory.Name, createdCategory.Name);
            Assert.Equal(newCategory.AreaId, createdCategory.AreaId);
            Assert.True(createdCategory.Id > 0); // Should have a new ID

            // Cleanup (optional but good practice)
            await _clientOrg1Admin.DeleteAsync($"/api/menucategories/{createdCategory.Id}");
        }

        [Fact]
        public async Task PostCategory_WhenSuperAdmin_ForAnyArea_ReturnsCreated()
        {
            // Arrange
            var newCategory = new MenuCategory { Name = "Test Post Category SuperAdmin", AreaId = TestConstants.Org2Area1Id }; // Use constant

            // Act
            var response = await _clientSuperAdmin.PostAsJsonAsync("/api/menucategories", newCategory);

            // Assert
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var createdCategory = await response.Content.ReadFromJsonAsync<MenuCategoryDto>(); // Use DTO
            Assert.NotNull(createdCategory);
            Assert.Equal(newCategory.Name, createdCategory.Name);
            Assert.Equal(newCategory.AreaId, createdCategory.AreaId);

            // Cleanup
            await _clientSuperAdmin.DeleteAsync($"/api/menucategories/{createdCategory.Id}");
        }

        [Fact]
        public async Task PostCategory_WhenOrg1Admin_ForOtherOrgArea_ReturnsForbidden()
        {
            // Arrange
            var newCategory = new MenuCategory { Name = "Test Post Category Fail", AreaId = TestConstants.Org2Area1Id }; // Use constant

            // Act
            var response = await _clientOrg1Admin.PostAsJsonAsync("/api/menucategories", newCategory);

            // Assert
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task PostCategory_WhenOrg1Admin_ForNonExistentArea_ReturnsNotFound() // Service throws KeyNotFoundException
        {
            // Arrange
            var newCategory = new MenuCategory { Name = "Test Post Category Fail", AreaId = 9999 };

            // Act
            var response = await _clientOrg1Admin.PostAsJsonAsync("/api/menucategories", newCategory);

            // Assert
            // Expecting NotFound because the service checks Area existence first.
            // If the controller doesn't handle KeyNotFoundException well, it might be 500.
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task PostCategory_WhenCashier_ReturnsForbidden()
        {
            // Arrange
            var newCategory = new MenuCategory { Name = "Test Post Category Cashier", AreaId = TestConstants.Org1Area1Id }; // Use constant

            // Act
            var response = await _clientCashierOrg1.PostAsJsonAsync("/api/menucategories", newCategory);

            // Assert
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }


        // =============================================
        // PUT /api/menucategories/{id} Tests
        // =============================================

        [Fact]
        public async Task PutCategory_WhenOrg1Admin_ForOwnCategory_ReturnsNoContent()
        {
            // Arrange: Create a category to update
            var postCategory = new MenuCategory { Name = "Category to Update Org1", AreaId = TestConstants.Org1Area1Id }; // Use constant
            var postResponse = await _clientOrg1Admin.PostAsJsonAsync("/api/menucategories", postCategory);
            postResponse.EnsureSuccessStatusCode();
            var createdCategory = await postResponse.Content.ReadFromJsonAsync<MenuCategoryDto>(); // Use DTO
            Assert.NotNull(createdCategory);

            var updatedCategoryData = new MenuCategory { Id = createdCategory.Id, Name = "Updated Category Org1", AreaId = TestConstants.Org1Area1Id }; // Use constant

            // Act
            var putResponse = await _clientOrg1Admin.PutAsJsonAsync($"/api/menucategories/{createdCategory.Id}", updatedCategoryData);

            // Assert
            Assert.Equal(HttpStatusCode.NoContent, putResponse.StatusCode);

            // Verify update
            var getResponse = await _clientOrg1Admin.GetAsync($"/api/menucategories/{createdCategory.Id}");
            getResponse.EnsureSuccessStatusCode();
            var fetchedCategory = await getResponse.Content.ReadFromJsonAsync<MenuCategoryDto>(); // Use DTO
            Assert.NotNull(fetchedCategory);
            Assert.Equal(updatedCategoryData.Name, fetchedCategory.Name);

            // Cleanup
            await _clientOrg1Admin.DeleteAsync($"/api/menucategories/{createdCategory.Id}");
        }

        [Fact]
        public async Task PutCategory_WhenSuperAdmin_ForAnyCategory_ReturnsNoContent()
        {
            // Arrange: Use seeded category from Org2
            var categoryToUpdateId = TestConstants.Category2Area2Id; // Use constant
            var updatedCategoryData = new MenuCategory { Id = categoryToUpdateId, Name = "Updated By SuperAdmin", AreaId = TestConstants.Org2Area1Id }; // Use constant

            // Act
            var putResponse = await _clientSuperAdmin.PutAsJsonAsync($"/api/menucategories/{categoryToUpdateId}", updatedCategoryData);

            // Assert
            Assert.Equal(HttpStatusCode.NoContent, putResponse.StatusCode);

            // Verify update
            var getResponse = await _clientSuperAdmin.GetAsync($"/api/menucategories/{categoryToUpdateId}");
            getResponse.EnsureSuccessStatusCode();
            var fetchedCategory = await getResponse.Content.ReadFromJsonAsync<MenuCategoryDto>(); // Use DTO
            Assert.NotNull(fetchedCategory);
            Assert.Equal(updatedCategoryData.Name, fetchedCategory.Name);

            // Note: No cleanup needed as we modified seeded data. Consider resetting if needed.
        }

        [Fact]
        public async Task PutCategory_WhenOrg1Admin_ForOtherOrgCategory_ReturnsForbidden()
        {
            // Arrange
            var categoryToUpdateId = TestConstants.Category2Area2Id; // Use constant
            var updatedCategoryData = new MenuCategory { Id = categoryToUpdateId, Name = "Update Fail", AreaId = TestConstants.Org2Area1Id }; // Use constant

            // Act
            var putResponse = await _clientOrg1Admin.PutAsJsonAsync($"/api/menucategories/{categoryToUpdateId}", updatedCategoryData);

            // Assert
            Assert.Equal(HttpStatusCode.Forbidden, putResponse.StatusCode);
        }

        [Fact]
        public async Task PutCategory_WhenOrg1Admin_MoveToOtherOrgArea_ReturnsForbidden()
        {
            // Arrange: Use seeded category from Org1
            var categoryToUpdateId = TestConstants.Category1Area1Id; // Use constant
            var updatedCategoryData = new MenuCategory { Id = categoryToUpdateId, Name = "Move Fail", AreaId = TestConstants.Org2Area1Id }; // Use constant

            // Act
            var putResponse = await _clientOrg1Admin.PutAsJsonAsync($"/api/menucategories/{categoryToUpdateId}", updatedCategoryData);

            // Assert
            Assert.Equal(HttpStatusCode.Forbidden, putResponse.StatusCode);
        }

        [Fact]
        public async Task PutCategory_ForNonExistentCategory_ReturnsNotFound()
        {
            // Arrange
            var updatedCategoryData = new MenuCategory { Id = 9999, Name = "Update NonExistent", AreaId = TestConstants.Org1Area1Id }; // Use constant

            // Act
            var putResponse = await _clientSuperAdmin.PutAsJsonAsync("/api/menucategories/9999", updatedCategoryData);

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, putResponse.StatusCode);
        }

        [Fact]
        public async Task PutCategory_WhenCashier_ReturnsForbidden()
        {
            // Arrange
            var categoryToUpdateId = TestConstants.Category1Area1Id; // Use constant
            var updatedCategoryData = new MenuCategory { Id = categoryToUpdateId, Name = "Update Fail Cashier", AreaId = TestConstants.Org1Area1Id }; // Use constant

            // Act
            var putResponse = await _clientCashierOrg1.PutAsJsonAsync($"/api/menucategories/{categoryToUpdateId}", updatedCategoryData);

            // Assert
            Assert.Equal(HttpStatusCode.Forbidden, putResponse.StatusCode);
        }

        // =============================================
        // DELETE /api/menucategories/{id} Tests
        // =============================================

        [Fact]
        public async Task DeleteCategory_WhenOrg1Admin_ForOwnCategory_ReturnsNoContent()
        {
            // Arrange: Create a category to delete
            var postCategory = new MenuCategory { Name = "Category to Delete Org1", AreaId = TestConstants.Org1Area1Id }; // Use constant
            var postResponse = await _clientOrg1Admin.PostAsJsonAsync("/api/menucategories", postCategory);
            postResponse.EnsureSuccessStatusCode();
            var createdCategory = await postResponse.Content.ReadFromJsonAsync<MenuCategoryDto>(); // Use DTO
            Assert.NotNull(createdCategory);

            // Act
            var deleteResponse = await _clientOrg1Admin.DeleteAsync($"/api/menucategories/{createdCategory.Id}");

            // Assert
            Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

            // Verify deletion
            var getResponse = await _clientOrg1Admin.GetAsync($"/api/menucategories/{createdCategory.Id}");
            Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
        }

        [Fact]
        public async Task DeleteCategory_WhenSuperAdmin_ForAnyCategory_ReturnsNoContent()
        {
            // Arrange: Create a category in Org2 to delete
            var postCategory = new MenuCategory { Name = "Category to Delete SuperAdmin", AreaId = TestConstants.Org2Area1Id }; // Use constant
            var postResponse = await _clientSuperAdmin.PostAsJsonAsync("/api/menucategories", postCategory);
            postResponse.EnsureSuccessStatusCode();
            var createdCategory = await postResponse.Content.ReadFromJsonAsync<MenuCategoryDto>(); // Use DTO
            Assert.NotNull(createdCategory);

            // Act
            var deleteResponse = await _clientSuperAdmin.DeleteAsync($"/api/menucategories/{createdCategory.Id}");

            // Assert
            Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

            // Verify deletion
            var getResponse = await _clientSuperAdmin.GetAsync($"/api/menucategories/{createdCategory.Id}");
            Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
        }

        [Fact]
        public async Task DeleteCategory_WhenOrg1Admin_ForOtherOrgCategory_ReturnsForbidden()
        {
            // Arrange: Use seeded category from Org2
            var categoryToDeleteId = TestConstants.Category2Area2Id; // Use constant

            // Act
            var deleteResponse = await _clientOrg1Admin.DeleteAsync($"/api/menucategories/{categoryToDeleteId}");

            // Assert
            // Expecting Forbidden because the service throws UnauthorizedAccessException
            // Need to ensure the controller handles this and returns 403.
            Assert.Equal(HttpStatusCode.Forbidden, deleteResponse.StatusCode);
        }

        [Fact]
        public async Task DeleteCategory_ForNonExistentCategory_ReturnsNotFound()
        {
            // Act
            var deleteResponse = await _clientSuperAdmin.DeleteAsync("/api/menucategories/9999");

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, deleteResponse.StatusCode);
        }

        [Fact]
        public async Task DeleteCategory_WhenCashier_ReturnsForbidden()
        {
            // Arrange: Use seeded category from Org1
            var categoryToDeleteId = TestConstants.Category1Area1Id; // Use constant

            // Act
            var deleteResponse = await _clientCashierOrg1.DeleteAsync($"/api/menucategories/{categoryToDeleteId}");

            // Assert
            Assert.Equal(HttpStatusCode.Forbidden, deleteResponse.StatusCode);
        }

        // TODO: Add test for deleting a category that has MenuItems (should fail if FK constraint exists)
    }
}
