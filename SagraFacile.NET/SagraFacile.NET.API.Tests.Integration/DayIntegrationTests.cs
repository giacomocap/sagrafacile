using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using SagraFacile.NET.API.DTOs; // Keep DTOs as they are likely needed
using SagraFacile.NET.API.Models; // Keep Models
using Xunit;
using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore; // Added for ToListAsync and EF Core operations
using SagraFacile.NET.API.Data; // Added for ApplicationDbContext
using Microsoft.Extensions.DependencyInjection; // Added for IServiceScopeFactory

namespace SagraFacile.NET.API.Tests.Integration;

[Collection("Sequential")] // Ensure tests using the shared factory run sequentially
public class DayIntegrationTests : IClassFixture<CustomWebApplicationFactory> // Use base factory type
{
    private readonly CustomWebApplicationFactory _factory;

    public DayIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        // No client or scope factory initialization here
    }

    // --- Helper Methods ---

    private async Task<DayDto?> GetCurrentOpenDayDtoAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/days/current");
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
        response.EnsureSuccessStatusCode(); // Throw for other errors
        return await response.Content.ReadFromJsonAsync<DayDto>();
    }

    private async Task EnsureNoOpenDayAsync(HttpClient client)
    {
        var currentDay = await GetCurrentOpenDayDtoAsync(client);
        if (currentDay != null)
        {
            // Attempt to close it
            var closeResponse = await client.PostAsync($"/api/days/{currentDay.Id}/close", null);
            if (!closeResponse.IsSuccessStatusCode && closeResponse.StatusCode != HttpStatusCode.BadRequest) // Ignore BadRequest if already closed
            {
                // Log or throw if closing failed unexpectedly
                Console.WriteLine($"Warning: Failed to close day {currentDay.Id} during test setup. Status: {closeResponse.StatusCode}");
            }
        }
    }

    private async Task<DayDto> OpenANewDayAsync(HttpClient client)
    {
        var response = await client.PostAsync("/api/days/open", null);
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var openedDay = await response.Content.ReadFromJsonAsync<DayDto>();
        Assert.NotNull(openedDay);
        return openedDay;
    }

    // Helper to clear Day entities for a specific org before a test
    private async Task ClearTestDaysForOrgAsync(int organizationId)
    {
        try
        {
            // Correctly create a scope to resolve scoped services like DbContext
            var scopeFactory = _factory.Services.GetRequiredService<IServiceScopeFactory>(); // Get the factory
            using var scope = scopeFactory.CreateScope(); // Create and dispose the scope
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Find days for the specific organization
            var daysToRemove = await dbContext.Days
                .Where(d => d.OrganizationId == organizationId)
                .ToListAsync();

            if (!daysToRemove.Any())
            {
                // Console.WriteLine($"No days found to clear for Org {organizationId}.");
                return; // Nothing to clear
            }

            var dayIdsToRemove = daysToRemove.Select(d => d.Id).ToList();

            // Check for linked orders BEFORE attempting removal
            var linkedOrders = await dbContext.Orders
                                            .Where(o => o.DayId.HasValue && dayIdsToRemove.Contains(o.DayId.Value))
                                            .ToListAsync();

            if (linkedOrders.Any())
            {
                // Log a warning and potentially skip deletion or handle differently
                // Depending on business logic, deleting days with orders might be invalid.
                // For test cleanup, nullifying FK might be an option if schema allows,
                // but simply warning and skipping is safer for now.
                Console.WriteLine($"Warning: Cannot clear days {string.Join(",", dayIdsToRemove)} for Org {organizationId} as they have linked orders ({string.Join(",", linkedOrders.Select(o => o.Id))}). Test state might be inconsistent.");
                // Optionally: Nullify DayId in orders if allowed:
                // foreach(var order in linkedOrders) { order.DayId = null; }
                // await dbContext.SaveChangesAsync();
                // Then proceed to remove days... OR just return here:
                return; // Skip deleting days if orders are linked
            }

            // No linked orders found, proceed with removal
            dbContext.Days.RemoveRange(daysToRemove);
            var changedCount = await dbContext.SaveChangesAsync();
            Console.WriteLine($"Cleared {changedCount} Day entities for Org {organizationId}.");
        }
        catch (Exception ex)
        {
             // Log error during cleanup, but don't necessarily fail the test run
             Console.WriteLine($"Error during test cleanup (ClearTestDaysForOrgAsync for Org {organizationId}): {ex.Message}");
        }
    }


    // --- GET /api/days/current Tests ---

    [Fact]
    public async Task GetCurrentDay_WhenOrg1AdminAndDayIsOpen_ReturnsCurrentDay()
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync(TestConstants.Org1AdminEmail);
        await EnsureNoOpenDayAsync(client); // Ensure clean state first
        var openedDay = await OpenANewDayAsync(client); // Ensure a day is open

        // Act
        var response = await client.GetAsync("/api/days/current");

        // Assert
        response.EnsureSuccessStatusCode(); // Status Code 200-299
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var day = await response.Content.ReadFromJsonAsync<DayDto>();
        Assert.NotNull(day);
        Assert.Null(day.EndTime); // Check if EndTime is null for open day
        Assert.Equal(TestConstants.Org1Id, day.OrganizationId);
        Assert.Equal(openedDay.Id, day.Id); // Check against the actually opened day
    }

    [Fact]
    public async Task GetCurrentDay_WhenOrg2AdminAndNoDayIsOpen_ReturnsNotFound()
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync(TestConstants.Org2AdminEmail);
        await EnsureNoOpenDayAsync(client); // Explicitly ensure no day is open

        // Act
        var response = await client.GetAsync("/api/days/current");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetCurrentDay_WhenOrg1CashierAndDayIsOpen_ReturnsCurrentDay()
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync(TestConstants.Org1CashierEmail); // Cashier role
        // Need Admin client to ensure state
        var adminClient = await _factory.CreateAuthenticatedClientAsync(TestConstants.Org1AdminEmail);
        await EnsureNoOpenDayAsync(adminClient);
        var openedDay = await OpenANewDayAsync(adminClient); // Ensure a day is open using Admin

        // Act
        var response = await client.GetAsync("/api/days/current"); // Cashier performs the action

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var day = await response.Content.ReadFromJsonAsync<DayDto>();
        Assert.NotNull(day);
        Assert.Null(day.EndTime); // Check if EndTime is null for open day
        Assert.Equal(TestConstants.Org1Id, day.OrganizationId);
        Assert.Equal(openedDay.Id, day.Id); // Check against the actually opened day
    }

     [Fact]
    public async Task GetCurrentDay_WhenSuperAdmin_ReturnsForbidden()
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync(TestConstants.SuperAdminEmail); // SuperAdmin role

        // Act
        var response = await client.GetAsync("/api/days/current");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode); // SuperAdmin cannot access org-specific current day
    }


    // --- POST /api/days/open Tests ---

    [Fact]
    public async Task OpenDay_WhenOrg2AdminAndNoDayIsOpen_OpensNewDay()
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync(TestConstants.Org2AdminEmail);
        await EnsureNoOpenDayAsync(client); // Explicitly ensure no day is open

        // Act
        var response = await client.PostAsync("/api/days/open", null); // No request body needed

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Created, response.StatusCode); // Should return 201 Created
        var openedDay = await response.Content.ReadFromJsonAsync<DayDto>();
        Assert.NotNull(openedDay);
        Assert.Null(openedDay.EndTime); // Check if EndTime is null for open day
        Assert.Equal(TestConstants.Org2Id, openedDay.OrganizationId);
        Assert.Null(openedDay.EndTime); // EndTime should be null for an open day (already correct here, but checking)
        Assert.True(openedDay.Id > 0); // Should have a generated ID
        // Optionally: Verify StartTime is recent, but time zones can make this tricky in tests
    }

    [Fact]
    public async Task OpenDay_WhenOrg1AdminAndDayIsAlreadyOpen_ReturnsBadRequest()
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync(TestConstants.Org1AdminEmail);
        await EnsureNoOpenDayAsync(client);
        await OpenANewDayAsync(client); // Ensure a day IS open

        // Act
        var response = await client.PostAsync("/api/days/open", null); // Try to open another one

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); // Service likely returns BadRequest if a day is already open
    }

    [Fact]
    public async Task OpenDay_WhenOrg1Cashier_ReturnsForbidden()
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync(TestConstants.Org1CashierEmail); // Cashier role
        // Ensure state using Admin client (doesn't matter if day is open or not for this auth test)
        var adminClient = await _factory.CreateAuthenticatedClientAsync(TestConstants.Org1AdminEmail);
        await EnsureNoOpenDayAsync(adminClient);

        // Act
        var response = await client.PostAsync("/api/days/open", null); // Cashier attempts action

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode); // Only Admins can open days
    }

    [Fact]
    public async Task OpenDay_WhenSuperAdmin_ReturnsForbidden()
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync(TestConstants.SuperAdminEmail); // SuperAdmin role
        // State doesn't matter for this auth test

        // Act
        var response = await client.PostAsync("/api/days/open", null);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode); // SuperAdmin cannot perform org-specific actions like opening a day
    }


    // --- POST /api/days/{id}/close Tests ---

    [Fact]
    public async Task CloseDay_WhenOrg1AdminAndCorrectOpenDayId_ClosesDayAndSetsEndTime()
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync(TestConstants.Org1AdminEmail);
        await EnsureNoOpenDayAsync(client);
        var openedDay = await OpenANewDayAsync(client); // Ensure a day is open
        // TODO: Seed orders for openedDay.Id in CustomWebApplicationFactory to properly test TotalSales calculation.
        // decimal expectedTotalSales = CalculateExpectedSalesForDay(openedDay.Id); // Helper needed if seeding is done

        // Act
        var response = await client.PostAsync($"/api/days/{openedDay.Id}/close", null); // Close the day we just opened

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); // Close typically returns OK or NoContent
        var closedDay = await response.Content.ReadFromJsonAsync<DayDto>();
        Assert.NotNull(closedDay);
        Assert.Equal(openedDay.Id, closedDay.Id);
        Assert.NotNull(closedDay.EndTime); // Check if EndTime is NOT null for closed day
        Assert.True(closedDay.EndTime > closedDay.StartTime);
        Assert.Equal(TestConstants.Org1Id, closedDay.OrganizationId);
        // Assert.Equal(expectedTotalSales, closedDay.TotalSales); // Uncomment and implement check once seeding is available
    }

    [Fact]
    public async Task CloseDay_WhenOrg1AdminAndDayAlreadyClosed_ReturnsBadRequest()
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync(TestConstants.Org1AdminEmail);
        await EnsureNoOpenDayAsync(client);
        var openedDay = await OpenANewDayAsync(client); // Open a day
        var closeResponse = await client.PostAsync($"/api/days/{openedDay.Id}/close", null); // Close it
        closeResponse.EnsureSuccessStatusCode(); // Make sure it closed correctly

        // Act
        var response = await client.PostAsync($"/api/days/{openedDay.Id}/close", null); // Try to close it again

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); // Cannot close an already closed day
    }

    [Fact]
    public async Task CloseDay_WhenOrg1AdminAndWrongDayId_ReturnsNotFound()
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync(TestConstants.Org1AdminEmail);
        const int nonExistentDayId = 99999;
        // Ensure state doesn't interfere
        await EnsureNoOpenDayAsync(client);

        // Act
        var response = await client.PostAsync($"/api/days/{nonExistentDayId}/close", null);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode); // Day not found
    }

     [Fact]
    public async Task CloseDay_WhenOrg1AdminAndDayIdFromOtherOrg_ReturnsForbidden()
    {
        // Arrange
        var clientOrg1 = await _factory.CreateAuthenticatedClientAsync(TestConstants.Org1AdminEmail);
        var clientOrg2 = await _factory.CreateAuthenticatedClientAsync(TestConstants.Org2AdminEmail);
        await EnsureNoOpenDayAsync(clientOrg2);
        var org2OpenedDay = await OpenANewDayAsync(clientOrg2); // Open a day in Org 2

        // Act
        // Org 1 Admin tries to close Org 2's day
        var response = await clientOrg1.PostAsync($"/api/days/{org2OpenedDay.Id}/close", null);

        // Assert
        // Service/Controller should prevent closing a day from another org. Could be Forbidden or NotFound.
        // Based on BaseService logic, it likely throws UnauthorizedAccessException -> Controller returns Forbidden
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CloseDay_WhenOrg1Cashier_ReturnsForbidden()
    {
        // Arrange
        var cashierClient = await _factory.CreateAuthenticatedClientAsync(TestConstants.Org1CashierEmail);
        var adminClient = await _factory.CreateAuthenticatedClientAsync(TestConstants.Org1AdminEmail);
        await EnsureNoOpenDayAsync(adminClient);
        var openedDay = await OpenANewDayAsync(adminClient); // Ensure a day is open

        // Act
        var response = await cashierClient.PostAsync($"/api/days/{openedDay.Id}/close", null); // Cashier attempts to close

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode); // Cashiers cannot close days
    }

    [Fact]
    public async Task CloseDay_WhenSuperAdmin_ReturnsForbidden()
    {
        // Arrange
        var superAdminClient = await _factory.CreateAuthenticatedClientAsync(TestConstants.SuperAdminEmail);
        var adminClient = await _factory.CreateAuthenticatedClientAsync(TestConstants.Org1AdminEmail);
        await EnsureNoOpenDayAsync(adminClient);
        var openedDay = await OpenANewDayAsync(adminClient); // Ensure a day exists to try and close

        // Act
        var response = await superAdminClient.PostAsync($"/api/days/{openedDay.Id}/close", null); // SuperAdmin attempts to close

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode); // SuperAdmin cannot perform this org-specific action
    }


    // --- GET /api/days Tests ---

    [Fact]
    public async Task GetDays_WhenOrg1Admin_ReturnsDaysForOrg1()
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync(TestConstants.Org1AdminEmail);
        await ClearTestDaysForOrgAsync(TestConstants.Org1Id); // Clean up days from previous tests for this org
        // EnsureNoOpenDayAsync might not be strictly needed now but doesn't hurt
        await EnsureNoOpenDayAsync(client);
        var day1 = await OpenANewDayAsync(client); // Open day 1
        var closeResp = await client.PostAsync($"/api/days/{day1.Id}/close", null); // Close day 1
        closeResp.EnsureSuccessStatusCode(); // Ensure close succeeded
        var day2 = await OpenANewDayAsync(client); // Open day 2

        // Act
        var response = await client.GetAsync("/api/days");

        // Assert
        response.EnsureSuccessStatusCode();
        var days = await response.Content.ReadFromJsonAsync<IEnumerable<DayDto>>();
        Assert.NotNull(days);
        // Assert that *at least* the two days we created are present and correct
        Assert.Contains(days, d => d.Id == day1.Id && d.EndTime != null && d.OrganizationId == TestConstants.Org1Id); // Day 1 closed, Org1
        Assert.Contains(days, d => d.Id == day2.Id && d.EndTime == null && d.OrganizationId == TestConstants.Org1Id); // Day 2 open, Org1
        // Assert that *all* returned days belong to Org1 (filtering works)
        Assert.All(days, d => Assert.Equal(TestConstants.Org1Id, d.OrganizationId));
        // We don't assert the exact count anymore due to shared state.
    }

    [Fact]
    public async Task GetDays_WhenOrg2AdminAndNoDaysExist_ReturnsEmptyList()
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync(TestConstants.Org2AdminEmail);
        await ClearTestDaysForOrgAsync(TestConstants.Org2Id); // Clean up any Org 2 days from previous tests
        // EnsureNoOpenDayAsync might be redundant but safe
        await EnsureNoOpenDayAsync(client);

        // Act
        var response = await client.GetAsync("/api/days");

        // Assert
        response.EnsureSuccessStatusCode();
        var days = await response.Content.ReadFromJsonAsync<IEnumerable<DayDto>>();
        Assert.NotNull(days);
        Assert.Empty(days);
    }

    [Fact]
    public async Task GetDays_WhenOrg1Cashier_ReturnsForbidden()
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync(TestConstants.Org1CashierEmail);

        // Act
        var response = await client.GetAsync("/api/days");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetDays_WhenSuperAdmin_ReturnsForbidden()
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync(TestConstants.SuperAdminEmail);

        // Act
        var response = await client.GetAsync("/api/days");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }


    // --- GET /api/days/{id} Tests ---

    [Fact]
    public async Task GetDayById_WhenOrg1AdminAndCorrectDayId_ReturnsDay()
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync(TestConstants.Org1AdminEmail);
        await EnsureNoOpenDayAsync(client);
        var openedDay = await OpenANewDayAsync(client);

        // Act
        var response = await client.GetAsync($"/api/days/{openedDay.Id}");

        // Assert
        response.EnsureSuccessStatusCode();
        var day = await response.Content.ReadFromJsonAsync<DayDto>();
        Assert.NotNull(day);
        Assert.Equal(openedDay.Id, day.Id);
        Assert.Equal(TestConstants.Org1Id, day.OrganizationId);
        Assert.Null(day.EndTime); // Should be open
    }

    [Fact]
    public async Task GetDayById_WhenOrg1AdminAndNonExistentDayId_ReturnsNotFound()
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync(TestConstants.Org1AdminEmail);
        const int nonExistentDayId = 99998;

        // Act
        var response = await client.GetAsync($"/api/days/{nonExistentDayId}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetDayById_WhenOrg1AdminAndDayIdFromOtherOrg_ReturnsNotFound() // Or Forbidden depending on service impl. NotFound seems more likely based on GetDayByIdForUserAsync logic
    {
        // Arrange
        var clientOrg1 = await _factory.CreateAuthenticatedClientAsync(TestConstants.Org1AdminEmail);
        var clientOrg2 = await _factory.CreateAuthenticatedClientAsync(TestConstants.Org2AdminEmail);
        await EnsureNoOpenDayAsync(clientOrg2);
        var org2OpenedDay = await OpenANewDayAsync(clientOrg2); // Create a day in Org 2

        // Act
        // Org 1 Admin tries to get Org 2's day by ID
        var response = await clientOrg1.GetAsync($"/api/days/{org2OpenedDay.Id}");

        // Assert
        // Service likely returns null if org doesn't match, leading to NotFound in controller
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetDayById_WhenOrg1Cashier_ReturnsForbidden()
    {
        // Arrange
        var adminClient = await _factory.CreateAuthenticatedClientAsync(TestConstants.Org1AdminEmail);
        await EnsureNoOpenDayAsync(adminClient);
        var openedDay = await OpenANewDayAsync(adminClient); // Ensure a day exists

        var cashierClient = await _factory.CreateAuthenticatedClientAsync(TestConstants.Org1CashierEmail);

        // Act
        var response = await cashierClient.GetAsync($"/api/days/{openedDay.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

     [Fact]
    public async Task GetDayById_WhenSuperAdmin_ReturnsForbidden()
    {
        // Arrange
        var adminClient = await _factory.CreateAuthenticatedClientAsync(TestConstants.Org1AdminEmail);
        await EnsureNoOpenDayAsync(adminClient);
        var openedDay = await OpenANewDayAsync(adminClient); // Ensure a day exists

        var superAdminClient = await _factory.CreateAuthenticatedClientAsync(TestConstants.SuperAdminEmail);

        // Act
        var response = await superAdminClient.GetAsync($"/api/days/{openedDay.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
