using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions; // For RemoveAll
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using SagraFacile.NET.API.Data;
using SagraFacile.NET.API.DTOs; // Add DTO namespace
using SagraFacile.NET.API.Models; // Needed for Organization model if used in seeding/assertions
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers; // For AuthenticationHeaderValue
using System.Net.Http.Json; // For ReadFromJsonAsync
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace SagraFacile.NET.API.Tests.Integration;

// NOTE: The CustomWebApplicationFactory class has been moved to CustomWebApplicationFactory.cs

// Test class now uses the CustomWebApplicationFactory from the separate file
public class BasicIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    // Remove the single shared client, create authenticated ones as needed per test
    // private readonly HttpClient _client;

    public BasicIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        // _client = _factory.CreateClient(); // Don't create the shared unauthenticated client here anymore
    }

    [Fact]
    public async Task Get_OpenApiSpec_ReturnsSuccessStatusCode()
    {
        // Arrange
        var client = _factory.CreateClient(); // Use a standard client for this public endpoint

        // Act
        // Send a GET request to the /openapi/v1.json endpoint (OpenAPI spec document)
        var response = await client.GetAsync("/openapi/v1.json");

        // Assert
        // Ensure the request was successful
        response.EnsureSuccessStatusCode(); // Status Code 200-299
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // This test now requires authentication (SuperAdmin)
    [Fact]
    public async Task Get_Organizations_WhenSuperAdmin_ReturnsSuccessAndData() // Renamed test
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync(TestConstants.SuperAdminEmail); // Use constant

        // Act
        var response = await client.GetAsync("/api/organizations");

        // Assert
        // This will initially FAIL with 401 Unauthorized until [AllowAnonymous] is removed
        // After removing [AllowAnonymous], it should pass.
        response.EnsureSuccessStatusCode(); // Status Code 200-299
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Deserialize the response to the DTO
        var organizations = await response.Content.ReadFromJsonAsync<List<OrganizationDto>>();

        Assert.NotNull(organizations);
        // SuperAdmin should see the 3 orgs seeded by the test factory (Org1, Org2, System)
        Assert.Equal(3, organizations.Count); // Corrected expected count
        Assert.Contains(organizations, org => org.Name == TestConstants.Org1Name && org.Id == TestConstants.Org1Id); // Use constant
        Assert.Contains(organizations, org => org.Name == TestConstants.Org2Name && org.Id == TestConstants.Org2Id); // Use constant
        Assert.Contains(organizations, org => org.Name == TestConstants.SystemOrgName && org.Id == TestConstants.SystemOrgId); // Check for System Org
        // Removed check for 'Sagra di Tencarola' as it's not seeded in the Testing environment
    }

    // Example of a test that should fail without authentication
    // Test that Admin cannot access Organization endpoints
    [Fact]
    public async Task Get_Organizations_WhenOrgAdmin_ReturnsForbidden()
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync(TestConstants.Org1AdminEmail); // Use constant

        // Act
        var response = await client.GetAsync("/api/organizations");

        // Assert
        // This should pass with 403 Forbidden because the user is authenticated but lacks the SuperAdmin role.
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Post_Organization_WhenSuperAdmin_ReturnsCreated()
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync(TestConstants.SuperAdminEmail); // Use constant
        var newOrg = new Organization { Name = "New Test Org From Post" }; // Don't set ID, let DB handle

        // Act
        var response = await client.PostAsJsonAsync("/api/organizations", newOrg);

        // Assert
        response.EnsureSuccessStatusCode(); // Status Code 201 Created
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var createdOrg = await response.Content.ReadFromJsonAsync<Organization>();
        Assert.NotNull(createdOrg);
        Assert.Equal(newOrg.Name, createdOrg.Name);
        Assert.True(createdOrg.Id > 0); // Ensure an ID was assigned

        // Verify Location header
        Assert.NotNull(response.Headers.Location);
        // Adjust expected casing to match actual API response (PascalCase controller name)
        Assert.EndsWith($"/api/Organizations/{createdOrg.Id}", response.Headers.Location.ToString());

        // Optional: Verify the org was actually saved in the DB
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var orgInDb = await dbContext.Organizations.FindAsync(createdOrg.Id);
        Assert.NotNull(orgInDb);
        Assert.Equal(newOrg.Name, orgInDb.Name);
    }

    [Fact]
    public async Task Put_Organization_WhenSuperAdmin_ReturnsNoContent()
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync(TestConstants.SuperAdminEmail); // Use constant
        const int orgIdToUpdate = TestConstants.Org1Id; // Use constant
        var updatedOrgData = new Organization { Id = orgIdToUpdate, Name = "Updated Test Org 1 Via Put" }; // Changed name slightly

        // Act
        var response = await client.PutAsJsonAsync($"/api/organizations/{orgIdToUpdate}", updatedOrgData);

        // Assert
        response.EnsureSuccessStatusCode(); // Status Code 2xx
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode); // Standard for successful PUT

        // Optional: Verify the org was actually updated in the DB
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        // Important: Use AsNoTracking for verification after update if the context might still track the old entity
        var orgInDb = await dbContext.Organizations.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orgIdToUpdate);
        Assert.NotNull(orgInDb);
        Assert.Equal(updatedOrgData.Name, orgInDb.Name);

        // Clean up / Reset state if necessary for other tests (or use separate DB per test)
        // For now, we assume tests can handle the updated state or are independent.
        // If not, add logic here to revert the change or use a transaction.
    }

    [Fact]
    public async Task Delete_Organization_WhenSuperAdmin_ReturnsNoContent()
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync(TestConstants.SuperAdminEmail); // Use constant
        int orgIdToDelete;

        // Create an organization specifically for this test to delete, use a different ID than seeded ones
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var orgToDelete = new Organization { Name = "Org To Delete" };
            dbContext.Organizations.Add(orgToDelete);
            await dbContext.SaveChangesAsync();
            orgIdToDelete = orgToDelete.Id; // Get the ID assigned by the database
        }

        // Act
        var response = await client.DeleteAsync($"/api/organizations/{orgIdToDelete}");

        // Assert
        response.EnsureSuccessStatusCode(); // Status Code 2xx
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode); // Standard for successful DELETE

        // Verify the org was actually deleted from the DB
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var orgInDb = await dbContext.Organizations.FindAsync(orgIdToDelete);
            Assert.Null(orgInDb); // Should not exist anymore
        }
    }

    // --- Role Authorization Tests ---

    // Removed duplicate [Theory] attribute
    [Theory]
    [InlineData(TestConstants.Org1AdminEmail)] // Use constant
    [InlineData(TestConstants.Org1CashierEmail)] // Use constant
    public async Task Get_Organizations_WhenNotSuperAdmin_ReturnsForbidden(string userName)
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync(userName);

        // Act
        var response = await client.GetAsync("/api/organizations"); // Check GET all

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // Combine other Forbidden checks using Theory
    [Theory]
    [InlineData(TestConstants.Org1AdminEmail)] // Use constant
    [InlineData(TestConstants.Org1CashierEmail)] // Use constant
    public async Task Post_Organization_WhenNotSuperAdmin_ReturnsForbidden(string userName)
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync(userName);
        var newOrg = new Organization { Name = "Attempt by NonSuper" };

        // Act
        var response = await client.PostAsJsonAsync("/api/organizations", newOrg);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData(TestConstants.Org1AdminEmail)] // Use constant
    [InlineData(TestConstants.Org1CashierEmail)] // Use constant
    public async Task Put_Organization_WhenNotSuperAdmin_ReturnsForbidden(string userName)
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync(userName);
        const int orgIdToUpdate = TestConstants.Org1Id; // Use constant
        var updatedOrgData = new Organization { Id = orgIdToUpdate, Name = "Attempt by NonSuper" };

        // Act
        var response = await client.PutAsJsonAsync($"/api/organizations/{orgIdToUpdate}", updatedOrgData);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData(TestConstants.Org1AdminEmail)] // Use constant
    [InlineData(TestConstants.Org1CashierEmail)] // Use constant
    public async Task Delete_Organization_WhenNotSuperAdmin_ReturnsForbidden(string userName)
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync(userName);
        const int orgIdToDelete = TestConstants.Org1Id; // Use constant

        // Act
        var response = await client.DeleteAsync($"/api/organizations/{orgIdToDelete}");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }


    // TODO: Add tests checking multi-tenancy (user can only access their own org's data) - Requires service/repo layer changes first.
}
