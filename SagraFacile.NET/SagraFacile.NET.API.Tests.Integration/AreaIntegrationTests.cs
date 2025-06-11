using SagraFacile.NET.API.Models;
using System; // For UnauthorizedAccessException
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Xunit;

namespace SagraFacile.NET.API.Tests.Integration
{
    // Use the same factory fixture as BasicIntegrationTests to share the seeded in-memory DB
    [Collection("Sequential")] // Ensure tests using the shared factory run sequentially
    public class AreaIntegrationTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly CustomWebApplicationFactory _factory;

        public AreaIntegrationTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        // --- GET /api/areas (List) Tests ---

        [Fact]
    public async Task GetAreas_WhenOrg1Admin_ReturnsOnlyOrg1Areas()
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync(TestConstants.Org1AdminEmail); // Use constant

        // Act
        var response = await client.GetAsync("/api/areas");

            // Assert
            response.EnsureSuccessStatusCode();
            var areas = await response.Content.ReadFromJsonAsync<List<Area>>();
        Assert.NotNull(areas);
        // Check presence of known Org1 items and absence of Org2 items, avoid exact count
        Assert.Contains(areas, a => a.Id == TestConstants.Org1Area1Id && a.OrganizationId == TestConstants.Org1Id); // Use constants
        Assert.Contains(areas, a => a.Id == TestConstants.Org1Area2Id && a.OrganizationId == TestConstants.Org1Id); // Use constants
        Assert.DoesNotContain(areas, a => a.OrganizationId == TestConstants.Org2Id); // Use constant
        Assert.All(areas, a => Assert.Equal(TestConstants.Org1Id, a.OrganizationId)); // Use constant
    }

    [Fact]
    public async Task GetAreas_WhenOrg2Admin_ReturnsOnlyOrg2Areas()
    {
         // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync(TestConstants.Org2AdminEmail); // Use constant

        // Act
        var response = await client.GetAsync("/api/areas");

            // Assert
            response.EnsureSuccessStatusCode();
            var areas = await response.Content.ReadFromJsonAsync<List<Area>>();
        Assert.NotNull(areas);
        // Check presence of known Org2 items and absence of Org1 items, avoid exact count
        Assert.Contains(areas, a => a.Id == TestConstants.Org2Area1Id && a.OrganizationId == TestConstants.Org2Id); // Use constants
        Assert.DoesNotContain(areas, a => a.OrganizationId == TestConstants.Org1Id); // Use constant
        Assert.All(areas, a => Assert.Equal(TestConstants.Org2Id, a.OrganizationId)); // Use constant
    }

    [Fact]
    public async Task GetAreas_WhenSuperAdmin_ReturnsAllAreas()
    {
         // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync(TestConstants.SuperAdminEmail); // Use constant

        // Act
        var response = await client.GetAsync("/api/areas");

            // Assert
            response.EnsureSuccessStatusCode();
            var areas = await response.Content.ReadFromJsonAsync<List<Area>>();
        Assert.NotNull(areas);
        // Check presence of known items from both orgs, avoid exact count
        Assert.Contains(areas, a => a.Id == TestConstants.Org1Area1Id && a.OrganizationId == TestConstants.Org1Id); // Use constants
        Assert.Contains(areas, a => a.Id == TestConstants.Org2Area1Id && a.OrganizationId == TestConstants.Org2Id); // Use constants
        Assert.Contains(areas, a => a.Id == TestConstants.Org1Area2Id && a.OrganizationId == TestConstants.Org1Id); // Use constants
    }

     [Fact]
    public async Task GetAreas_WhenCashier_ReturnsForbidden() // Cashier role not allowed by controller
    {
         // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync(TestConstants.Org1CashierEmail); // Use constant

        // Act
        var response = await client.GetAsync("/api/areas");

            // Assert
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }


        // --- GET /api/areas/{id} Tests ---

        [Fact]
    public async Task GetAreaById_WhenOrg1Admin_CanGetOwnArea()
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync(TestConstants.Org1AdminEmail); // Use constant
        const int areaId = TestConstants.Org1Area1Id; // Use constant

        // Act
        var response = await client.GetAsync($"/api/areas/{areaId}");

            // Assert
            response.EnsureSuccessStatusCode();
            var area = await response.Content.ReadFromJsonAsync<Area>();
        Assert.NotNull(area);
        Assert.Equal(areaId, area.Id);
        Assert.Equal(TestConstants.Org1Id, area.OrganizationId); // Use constant
    }

    [Fact]
    public async Task GetAreaById_WhenOrg1Admin_CannotGetOtherOrgArea()
    {
         // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync(TestConstants.Org1AdminEmail); // Use constant
        const int areaId = TestConstants.Org2Area1Id; // Use constant

        // Act
        var response = await client.GetAsync($"/api/areas/{areaId}");

            // Assert
            // Service returns null for unauthorized access, controller translates to NotFound
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

     [Fact]
    public async Task GetAreaById_WhenSuperAdmin_CanGetAnyArea()
    {
         // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync(TestConstants.SuperAdminEmail); // Use constant
        const int areaIdOrg1 = TestConstants.Org1Area1Id; // Use constant
        const int areaIdOrg2 = TestConstants.Org2Area1Id; // Use constant

        // Act
        var responseOrg1 = await client.GetAsync($"/api/areas/{areaIdOrg1}");
        var responseOrg2 = await client.GetAsync($"/api/areas/{areaIdOrg2}");

            // Assert
            responseOrg1.EnsureSuccessStatusCode();
            var areaOrg1 = await responseOrg1.Content.ReadFromJsonAsync<Area>();
            Assert.NotNull(areaOrg1);
            Assert.Equal(areaIdOrg1, areaOrg1.Id);

            responseOrg2.EnsureSuccessStatusCode();
            var areaOrg2 = await responseOrg2.Content.ReadFromJsonAsync<Area>();
            Assert.NotNull(areaOrg2);
            Assert.Equal(areaIdOrg2, areaOrg2.Id);
        }

        // --- POST /api/areas Tests ---

        [Fact]
    public async Task PostArea_WhenOrg1Admin_CreatesAreaForOrg1()
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync(TestConstants.Org1AdminEmail); // Use constant
        var newArea = new Area { Name = "New Org1 Area via POST" }; // OrgId should be set by service

        // Act
        var response = await client.PostAsJsonAsync("/api/areas", newArea);

            // Assert
            response.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var createdArea = await response.Content.ReadFromJsonAsync<Area>();
        Assert.NotNull(createdArea);
        Assert.Equal(newArea.Name, createdArea.Name);
        Assert.Equal(TestConstants.Org1Id, createdArea.OrganizationId); // Use constant
        Assert.True(createdArea.Id > TestConstants.Org1Area2Id); // Should be higher than last seeded ID
    }

     [Fact]
    public async Task PostArea_WhenOrg1Admin_CannotCreateAreaForOrg2()
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync(TestConstants.Org1AdminEmail); // Use constant
        // Attempt to explicitly set OrgId to Org 2
        var newArea = new Area { Name = "Attempt Create for Org2", OrganizationId = TestConstants.Org2Id }; // Use constant

        // Act
        var response = await client.PostAsJsonAsync("/api/areas", newArea);

            // Assert
            // Service throws UnauthorizedAccessException, which typically results in 500 Internal Server Error
            // unless specific exception handling maps it to 403 Forbidden or 400 Bad Request in the controller.
            // Let's assume default behavior leads to 500 for now, or check controller's catch blocks.
            // Based on current controller, it might fall into the general catch -> 500
            // Or if the service throws KeyNotFoundException first (if Org 2 doesn't exist, which it does), it's BadRequest.
            // If UnauthorizedAccessException is thrown, it's likely 500. Let's test for non-success.
             Assert.False(response.IsSuccessStatusCode);
             // We could refine this to check for 403 or 500 if we add specific handling
             // For now, checking it's not successful is a basic check.
             // A more specific check might be Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
             // Or Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode); if handled.
        }

        [Fact]
    public async Task PostArea_WhenSuperAdmin_CanCreateAreaForAnyOrg()
    {
         // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync(TestConstants.SuperAdminEmail); // Use constant
        // SuperAdmin explicitly sets the OrgId
        var newArea = new Area { Name = "New Org2 Area via SuperAdmin", OrganizationId = TestConstants.Org2Id }; // Use constant

        // Act
        var response = await client.PostAsJsonAsync("/api/areas", newArea);

            // Assert
            response.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var createdArea = await response.Content.ReadFromJsonAsync<Area>();
        Assert.NotNull(createdArea);
        Assert.Equal(newArea.Name, createdArea.Name);
        Assert.Equal(TestConstants.Org2Id, createdArea.OrganizationId); // Use constant
    }

    // --- PUT /api/areas/{id} Tests ---

    [Fact]
    public async Task PutArea_WhenOrg1Admin_CanUpdateOwnArea()
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync(TestConstants.Org1AdminEmail); // Use constant
        // Create a unique area for this test to update
        var areaToCreate = new Area { Name = $"Test Update Area {Guid.NewGuid()}" };
        var createResponse = await client.PostAsJsonAsync("/api/areas", areaToCreate);
            createResponse.EnsureSuccessStatusCode();
            var createdArea = await createResponse.Content.ReadFromJsonAsync<Area>();
            Assert.NotNull(createdArea);

            var updatedAreaData = new Area { Id = createdArea.Id, Name = "Updated Test Area Name", OrganizationId = createdArea.OrganizationId }; // Use created area's ID and OrgId

            // Act
            var response = await client.PutAsJsonAsync($"/api/areas/{createdArea.Id}", updatedAreaData);

            // Assert
            response.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        [Fact]
    public async Task PutArea_WhenOrg1Admin_CannotUpdateOtherOrgArea()
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync(TestConstants.Org1AdminEmail); // Use constant
        const int areaId = TestConstants.Org2Area1Id; // Use constant
        var updatedAreaData = new Area { Id = areaId, Name = "Attempt Update Org2 Area", OrganizationId = TestConstants.Org2Id }; // Use constant

        // Act
        var response = await client.PutAsJsonAsync($"/api/areas/{areaId}", updatedAreaData);

            // Assert
            // Service throws UnauthorizedAccessException -> likely 500 or potentially 403/404 if handled
             Assert.False(response.IsSuccessStatusCode);
             Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode); // Service throws UnauthorizedAccessException -> Controller catch -> 500
        }

     [Fact]
    public async Task PutArea_WhenOrg1Admin_CannotChangeOrgId()
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync(TestConstants.Org1AdminEmail); // Use constant
        const int areaId = TestConstants.Org1Area1Id; // Use constant
        // Attempt to change OrganizationId to 2
        var updatedAreaData = new Area { Id = areaId, Name = "Attempt Change OrgId", OrganizationId = TestConstants.Org2Id }; // Use constant

        // Act
        var response = await client.PutAsJsonAsync($"/api/areas/{areaId}", updatedAreaData);

            // Assert
            // Service throws UnauthorizedAccessException -> Controller catch -> 500
             Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Fact]
    public async Task PutArea_WhenSuperAdmin_CanUpdateAnyArea()
    {
         // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync(TestConstants.SuperAdminEmail); // Use constant
        // Create a unique area (e.g., for Org 2) for this test to update
        var areaToCreate = new Area { Name = $"Test Update Area SA {Guid.NewGuid()}", OrganizationId = TestConstants.Org2Id }; // Use constant
        var createResponse = await client.PostAsJsonAsync("/api/areas", areaToCreate);
        createResponse.EnsureSuccessStatusCode();
        var createdArea = await createResponse.Content.ReadFromJsonAsync<Area>();
            Assert.NotNull(createdArea);

            var updatedAreaData = new Area { Id = createdArea.Id, Name = "Updated Test Area Name SA", OrganizationId = createdArea.OrganizationId }; // Use created area's ID and OrgId

            // Act
            var response = await client.PutAsJsonAsync($"/api/areas/{createdArea.Id}", updatedAreaData);

            // Assert
            response.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        // --- DELETE /api/areas/{id} Tests ---

        [Fact]
    public async Task DeleteArea_WhenOrg1Admin_CanDeleteOwnArea()
    {
         // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync(TestConstants.Org1AdminEmail); // Use constant
        // Create a unique area for this test to delete
        var areaToCreate = new Area { Name = $"Test Delete Area {Guid.NewGuid()}" };
        var createResponse = await client.PostAsJsonAsync("/api/areas", areaToCreate);
            createResponse.EnsureSuccessStatusCode();
            var createdArea = await createResponse.Content.ReadFromJsonAsync<Area>();
            Assert.NotNull(createdArea);

            // Act
            var response = await client.DeleteAsync($"/api/areas/{createdArea.Id}");

            // Assert
            response.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

         [Fact]
        public async Task DeleteArea_WhenOrg1Admin_CannotDeleteOtherOrgArea()
        {
             // Arrange
            var client = await _factory.CreateAuthenticatedClientAsync(TestConstants.Org1AdminEmail); // Use constant
            const int areaIdToDelete = TestConstants.Org2Area1Id; // Use constant

            // Act
            var response = await client.DeleteAsync($"/api/areas/{areaIdToDelete}");

             // Assert
             // Controller now catches UnauthorizedAccessException and returns 403 Forbidden
             Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
    public async Task DeleteArea_WhenSuperAdmin_CanDeleteAnyArea()
    {
         // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync(TestConstants.SuperAdminEmail); // Use constant
        // Create a unique area (e.g., for Org 2) for this test to delete
        var areaToCreate = new Area { Name = $"Test Delete Area SA {Guid.NewGuid()}", OrganizationId = TestConstants.Org2Id }; // Use constant
        var createResponse = await client.PostAsJsonAsync("/api/areas", areaToCreate);
        createResponse.EnsureSuccessStatusCode();
        var createdArea = await createResponse.Content.ReadFromJsonAsync<Area>();
            Assert.NotNull(createdArea);

            // Act
            var response = await client.DeleteAsync($"/api/areas/{createdArea.Id}");

            // Assert
            response.EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }
    }
}
