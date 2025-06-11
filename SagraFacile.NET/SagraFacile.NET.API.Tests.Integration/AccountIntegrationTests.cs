using Microsoft.AspNetCore.Identity; // For IdentityError if needed
using SagraFacile.NET.API.DTOs;
using SagraFacile.NET.API.Models; // Assuming User model is here
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json; // Requires System.Net.Http.Json package
using System.Threading.Tasks;
using Xunit;
using SagraFacile.NET.API.Services.Interfaces; // Added for AssignRoleDto

namespace SagraFacile.NET.API.Tests.Integration;

[Collection("Sequential")] // Ensure tests using the shared factory run sequentially
public class AccountIntegrationTests : IClassFixture<CustomWebApplicationFactory> // Use non-generic factory
{
    private readonly CustomWebApplicationFactory _factory; // Use non-generic factory

    // Use constants from TestConstants.cs
    // private const string SuperAdminEmail = "superadmin@test.org"; // Replaced by TestConstants.SuperAdminEmail
    // ... other emails replaced similarly

    public AccountIntegrationTests(CustomWebApplicationFactory factory) // Use non-generic factory
    {
        _factory = factory;
        // Note: Clients are created per test method to ensure authentication context
    }

    // Helper to get user ID from email (assuming seeding is consistent)
    // In a real scenario, might need a more robust way or query the DB if necessary
    private async Task<string> GetUserIdByEmail(string email)
    {
        // This is a simplification for testing. Relies on seeded data.
        // A more robust approach might involve a dedicated lookup or querying the test DB.
        var client = await _factory.CreateAuthenticatedClientAsync(TestConstants.SuperAdminEmail); // Use constant
        var response = await client.GetAsync("/api/Accounts");
        response.EnsureSuccessStatusCode();
        var users = await response.Content.ReadFromJsonAsync<List<UserDto>>();
        var user = users?.FirstOrDefault(u => u.Email.Equals(email, StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(user); // Fail test if seeded user not found
        return user.Id;
    }

    // --- GET /api/Accounts ---

    [Fact]
    public async Task GetUsers_WhenSuperAdmin_ReturnsSuccessAndAllUsers()
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync(TestConstants.SuperAdminEmail); // Use constant

        // Act
        var response = await client.GetAsync("/api/Accounts");

        // Assert
        response.EnsureSuccessStatusCode(); // Status Code 200-299
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var users = await response.Content.ReadFromJsonAsync<List<UserDto>>();
        Assert.NotNull(users);
        // Check if specific seeded users are present by email
        Assert.Contains(users, u => u.Email == TestConstants.SuperAdminEmail); // Use constant
        Assert.Contains(users, u => u.Email == TestConstants.Org1AdminEmail); // Use constant
        Assert.Contains(users, u => u.Email == TestConstants.Org2AdminEmail); // Use constant
        Assert.Contains(users, u => u.Email == TestConstants.Org1CashierEmail); // Use constant
        // Check roles are included
        Assert.Contains(users.First(u => u.Email == TestConstants.SuperAdminEmail).Roles, r => r == "SuperAdmin"); // Use constant
        Assert.Contains(users.First(u => u.Email == TestConstants.Org1AdminEmail).Roles, r => r == "Admin"); // Changed OrgAdmin to Admin
    }

    [Fact]
    public async Task GetUsers_WhenOrg1Admin_ReturnsSuccessAndOnlyOrg1Users()
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync(TestConstants.Org1AdminEmail); // Use constant

        // Act
        var response = await client.GetAsync("/api/Accounts");

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var users = await response.Content.ReadFromJsonAsync<List<UserDto>>();
        Assert.NotNull(users);
        // Should contain Org1Admin and Org1Cashier, but NOT SuperAdmin or Org2Admin
        Assert.Contains(users, u => u.Email == TestConstants.Org1AdminEmail); // Use constant
        Assert.Contains(users, u => u.Email == TestConstants.Org1CashierEmail); // Use constant
        Assert.DoesNotContain(users!, u => u.Email == TestConstants.SuperAdminEmail); // Use constant, added !
        Assert.DoesNotContain(users!, u => u.Email == TestConstants.Org2AdminEmail); // Use constant, added !
        // Ensure all returned users implicitly belong to Org1 (service filters this)
    }

     [Fact]
    public async Task GetUsers_WhenCashier_ReturnsForbidden()
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync(TestConstants.Org1CashierEmail); // Use constant

        // Act
        var response = await client.GetAsync("/api/Accounts");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetUsers_WhenUnauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient(); // Unauthenticated

        // Act
        var response = await client.GetAsync("/api/Accounts");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // --- GET /api/Accounts/{userId} ---

    [Fact]
    public async Task GetUserById_WhenSuperAdmin_CanGetAnyUser()
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync(TestConstants.SuperAdminEmail);
        string userIdToGet = await GetUserIdByEmail(TestConstants.Org1CashierEmail); // Get Org1 Cashier

        // Act
        var response = await client.GetAsync($"/api/Accounts/{userIdToGet}");

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var user = await response.Content.ReadFromJsonAsync<UserDto>();
        Assert.NotNull(user);
        Assert.Equal(userIdToGet, user.Id);
        Assert.Equal(TestConstants.Org1CashierEmail, user.Email);
        Assert.Equal(TestConstants.Org1Id, user.OrganizationId); // Verify OrgId is present
    }

    [Fact]
    public async Task GetUserById_WhenOrg1Admin_CanGetOwnOrgUser()
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync(TestConstants.Org1AdminEmail);
        string userIdToGet = await GetUserIdByEmail(TestConstants.Org1CashierEmail); // Get Org1 Cashier

        // Act
        var response = await client.GetAsync($"/api/Accounts/{userIdToGet}");

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var user = await response.Content.ReadFromJsonAsync<UserDto>();
        Assert.NotNull(user);
        Assert.Equal(userIdToGet, user.Id);
        Assert.Equal(TestConstants.Org1CashierEmail, user.Email);
    }

    [Fact]
    public async Task GetUserById_WhenOrg1Admin_CannotGetOtherOrgUser()
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync(TestConstants.Org1AdminEmail);
        string userIdToGet = await GetUserIdByEmail(TestConstants.Org2AdminEmail); // Try to get Org2 Admin

        // Act
        var response = await client.GetAsync($"/api/Accounts/{userIdToGet}");

        // Assert
        // Service returns null, controller maps to 404 Not Found
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetUserById_WhenUserNotFound_ReturnsNotFound()
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync(TestConstants.SuperAdminEmail);
        var nonExistentUserId = "non-existent-guid";

        // Act
        var response = await client.GetAsync($"/api/Accounts/{nonExistentUserId}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetUserById_WhenCashier_ReturnsForbidden()
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync(TestConstants.Org1CashierEmail);
        string userIdToGet = await GetUserIdByEmail(TestConstants.Org1AdminEmail); // Try to get Org1 Admin

        // Act
        var response = await client.GetAsync($"/api/Accounts/{userIdToGet}");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetUserById_WhenUnauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient(); // Unauthenticated
        string userIdToGet = await GetUserIdByEmail(TestConstants.Org1AdminEmail); // Doesn't matter who, just need an ID

        // Act
        var response = await client.GetAsync($"/api/Accounts/{userIdToGet}");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // --- PUT /api/Accounts/{userId} ---

    [Fact]
    public async Task UpdateUser_WhenSuperAdmin_CanUpdateAnyUser()
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync(TestConstants.SuperAdminEmail); // Use constant
        string userIdToUpdate = await GetUserIdByEmail(TestConstants.Org1CashierEmail); // Use constant
        var updateUserDto = new UpdateUserDto
        {
            FirstName = "UpdatedFirstNameSA",
            LastName = "UpdatedLastNameSA",
            Email = $"updated.{TestConstants.Org1CashierEmail}" // Use constant
        };

        // Act
        var response = await client.PutAsJsonAsync($"/api/Accounts/{userIdToUpdate}", updateUserDto);

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Optional: Verify update by getting the user again
        var verifyResponse = await client.GetAsync("/api/Accounts"); // SuperAdmin gets all
        var users = await verifyResponse.Content.ReadFromJsonAsync<List<UserDto>>();
        var updatedUser = users?.FirstOrDefault(u => u.Id == userIdToUpdate);
        Assert.NotNull(updatedUser);
        Assert.Equal(updateUserDto.FirstName, updatedUser.FirstName);
        Assert.Equal(updateUserDto.LastName, updatedUser.LastName);
        Assert.Equal(updateUserDto.Email, updatedUser.Email);

        // Cleanup: Revert email AND names to avoid conflicts in other tests
        var revertUserDto = new UpdateUserDto {
            FirstName = TestConstants.Org1CashierFirstName, // Use original first name from constants
            LastName = TestConstants.Org1CashierLastName,   // Use original last name from constants
            Email = TestConstants.Org1CashierEmail          // Use original email from constants
        };
        await client.PutAsJsonAsync($"/api/Accounts/{userIdToUpdate}", revertUserDto);
    }

    [Fact]
    public async Task UpdateUser_WhenOrg1Admin_CanUpdateOwnOrgUser()
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync(TestConstants.Org1AdminEmail); // Use constant
        string userIdToUpdate = await GetUserIdByEmail(TestConstants.Org1CashierEmail); // Use constant
        var updateUserDto = new UpdateUserDto
        {
            FirstName = "UpdatedFirstNameOrg1",
            LastName = "UpdatedLastNameOrg1",
            Email = $"updated.org1.{TestConstants.Org1CashierEmail}" // Use constant
        };

        // Act
        var response = await client.PutAsJsonAsync($"/api/Accounts/{userIdToUpdate}", updateUserDto);

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Cleanup: Revert email
        var revertUserDto = new UpdateUserDto { FirstName = "Org1", LastName = "Cashier", Email = TestConstants.Org1CashierEmail }; // Use constant, Assuming original names
        var superAdminClient = await _factory.CreateAuthenticatedClientAsync(TestConstants.SuperAdminEmail); // Use constant
        await superAdminClient.PutAsJsonAsync($"/api/Accounts/{userIdToUpdate}", revertUserDto);
    }

    [Fact]
    public async Task UpdateUser_WhenOrg1Admin_CannotUpdateOtherOrgUser()
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync(TestConstants.Org1AdminEmail); // Use constant
        string userIdToUpdate = await GetUserIdByEmail(TestConstants.Org2AdminEmail); // Use constant
        var updateUserDto = new UpdateUserDto
        {
            FirstName = "AttemptUpdateFirstName",
            LastName = "AttemptUpdateLastName",
            Email = $"attempt.update.{TestConstants.Org2AdminEmail}" // Use constant
        };

        // Act
        var response = await client.PutAsJsonAsync($"/api/Accounts/{userIdToUpdate}", updateUserDto);

        // Assert
        // Service returns "not found" error for security, controller maps to 404
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

     [Fact]
    public async Task UpdateUser_WhenEmailExists_ReturnsBadRequest()
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync(TestConstants.SuperAdminEmail); // Use constant
        string userIdToUpdate = await GetUserIdByEmail(TestConstants.Org1CashierEmail); // Use constant
        var updateUserDto = new UpdateUserDto
        {
            FirstName = "AnyFirstName",
            LastName = "AnyLastName",
            Email = TestConstants.Org2AdminEmail // Use constant
        };

        // Act
        var response = await client.PutAsJsonAsync($"/api/Accounts/{userIdToUpdate}", updateUserDto);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        // Optional: Check error message if needed
    }

    // --- DELETE /api/Accounts/{userId} ---

    // Note: Deleting seeded users can interfere with other tests.
    // It's better to register a temporary user for delete tests.

    [Fact]
    public async Task DeleteUser_WhenSuperAdmin_CanDeleteOtherUser()
    {
        // Arrange: Register a temporary user to delete
        var superAdminClient = await _factory.CreateAuthenticatedClientAsync(TestConstants.SuperAdminEmail); // Use constant
        var tempUserEmail = $"temp.delete.sa.{Guid.NewGuid()}@test.org";
        var registerDto = new RegisterDto { Email = tempUserEmail, Password = TestConstants.DefaultPassword, ConfirmPassword = TestConstants.DefaultPassword, FirstName = "Temp", LastName = "DeleteSA" }; // Use constant, Add ConfirmPassword
        // SuperAdmin needs a way to assign OrgId or have a default. Assuming service handles this or uses a default org for now.
        // If registration requires OrgId for SA, this test needs adjustment.
        // Let's assume OrgAdmins register users for their org. We'll use Org1Admin to register.
        var org1AdminClient = await _factory.CreateAuthenticatedClientAsync(TestConstants.Org1AdminEmail); // Use constant
        var registerResponse = await org1AdminClient.PostAsJsonAsync("/api/Accounts/register", registerDto);
        registerResponse.EnsureSuccessStatusCode();
        // Parse response explicitly instead of using dynamic
        var registeredUserData = await registerResponse.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        string? userIdToDelete = registeredUserData?["userId"];
        Assert.NotNull(userIdToDelete);


        // Act: SuperAdmin deletes the temporary user
        var deleteResponse = await superAdminClient.DeleteAsync($"/api/Accounts/{userIdToDelete}");

        // Assert
        deleteResponse.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // Optional: Verify deletion
        var verifyResponse = await superAdminClient.GetAsync("/api/Accounts");
        var users = await verifyResponse.Content.ReadFromJsonAsync<List<UserDto>>();
        Assert.DoesNotContain(users, u => u.Id == userIdToDelete);
    }

     [Fact]
    public async Task DeleteUser_WhenOrgAdmin_CanDeleteOwnOrgUser()
    {
        // Arrange: Register a temporary user in Org1
        var org1AdminClient = await _factory.CreateAuthenticatedClientAsync(TestConstants.Org1AdminEmail); // Use constant
        var tempUserEmail = $"temp.delete.org1.{Guid.NewGuid()}@test.org";
        var registerDto = new RegisterDto { Email = tempUserEmail, Password = TestConstants.DefaultPassword, ConfirmPassword = TestConstants.DefaultPassword, FirstName = "Temp", LastName = "DeleteOrg1" }; // Use constant, Add ConfirmPassword
        var registerResponse = await org1AdminClient.PostAsJsonAsync("/api/Accounts/register", registerDto);
        registerResponse.EnsureSuccessStatusCode();
        // Parse response explicitly instead of using dynamic
        var registeredUserData = await registerResponse.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        string? userIdToDelete = registeredUserData?["userId"];
        Assert.NotNull(userIdToDelete);

        // Act: Org1Admin deletes the temporary user
        var deleteResponse = await org1AdminClient.DeleteAsync($"/api/Accounts/{userIdToDelete}");

        // Assert
        deleteResponse.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteUser_WhenOrgAdmin_CannotDeleteOtherOrgUser()
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync(TestConstants.Org1AdminEmail); // Use constant
        string userIdToDelete = await GetUserIdByEmail(TestConstants.Org2AdminEmail); // Use constant

        // Act
        var response = await client.DeleteAsync($"/api/Accounts/{userIdToDelete}");

        // Assert
        // Service returns "not found" for security, controller maps to 404
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteUser_CannotDeleteSelf()
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync(TestConstants.Org1AdminEmail); // Use constant
        string selfUserId = await GetUserIdByEmail(TestConstants.Org1AdminEmail); // Use constant

        // Act
        var response = await client.DeleteAsync($"/api/Accounts/{selfUserId}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        // Optional: Check error message
    }


    // --- GET /api/Accounts/roles ---

    [Fact]
    public async Task GetRoles_WhenAdmin_ReturnsSuccessAndListOfRoles()
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync(TestConstants.Org1AdminEmail); // Use constant

        // Act
        var response = await client.GetAsync("/api/Accounts/roles");

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var roles = await response.Content.ReadFromJsonAsync<List<string>>();
        Assert.NotNull(roles);
        Assert.Contains("SuperAdmin", roles);
        // Assert.Contains("OrgAdmin", roles); // Removed check for obsolete role
        Assert.Contains("Admin", roles); // Check for the current Admin role
        Assert.Contains("Cashier", roles);
        Assert.Contains("Waiter", roles); // Ensure Waiter role is checked
        // Assert.Contains("AreaAdmin", roles); // AreaAdmin role was removed/not standard
    }

    [Fact]
    public async Task GetRoles_WhenCashier_ReturnsForbidden()
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync(TestConstants.Org1CashierEmail); // Use constant

        // Act
        var response = await client.GetAsync("/api/Accounts/roles");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // --- POST /api/Accounts/roles ---

    [Fact]
    public async Task CreateRole_WhenSuperAdmin_CreatesRoleSuccessfully()
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync(TestConstants.SuperAdminEmail); // Use constant
        var newRoleName = $"TestRole_{Guid.NewGuid()}";
        var createRoleDto = new CreateRoleDto { RoleName = newRoleName };

        // Act
        var response = await client.PostAsJsonAsync("/api/Accounts/roles", createRoleDto);

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        // Parse response explicitly instead of using dynamic
        var createdRoleData = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.Equal(newRoleName, createdRoleData?["roleName"]); // Check returned name

        // Verify role exists now
        var getRolesResponse = await client.GetAsync("/api/Accounts/roles");
        var roles = await getRolesResponse.Content.ReadFromJsonAsync<List<string>>();
        Assert.Contains(newRoleName, roles);

        // Cleanup: Ideally, delete the created role if a delete endpoint exists.
    }

    [Fact]
    public async Task CreateRole_WhenRoleExists_ReturnsBadRequest()
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync(TestConstants.SuperAdminEmail); // Use constant
        var existingRoleName = "Admin"; // Use a known existing role (changed from OrgAdmin)
        var createRoleDto = new CreateRoleDto { RoleName = existingRoleName };

        // Act
        var response = await client.PostAsJsonAsync("/api/Accounts/roles", createRoleDto);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateRole_WhenOrgAdmin_ReturnsForbidden()
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync(TestConstants.Org1AdminEmail); // Use constant
        var createRoleDto = new CreateRoleDto { RoleName = "AttemptRoleByOrgAdmin" };

        // Act
        var response = await client.PostAsJsonAsync("/api/Accounts/roles", createRoleDto);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // --- POST /api/Accounts/assign-role ---

    [Fact]
    public async Task AssignRole_WhenSuperAdmin_AssignsRoleSuccessfully()
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync(TestConstants.SuperAdminEmail);
        string userIdToAssign = await GetUserIdByEmail(TestConstants.Org1CashierEmail); // Assign a new role to Org1 Cashier
        var uniqueRoleName = $"AssignTestRole_{Guid.NewGuid()}"; // Create a unique role for this test

        // 1. Create the unique role first
        var createRoleDto = new CreateRoleDto { RoleName = uniqueRoleName };
        var createRoleResponse = await client.PostAsJsonAsync("/api/Accounts/roles", createRoleDto);
        createRoleResponse.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Created, createRoleResponse.StatusCode);

        // 2. Prepare DTO to assign the new unique role
        var assignRoleDto = new AssignRoleDto { UserId = userIdToAssign, RoleName = uniqueRoleName };

        // Act: Assign the unique role
        var response = await client.PostAsJsonAsync("/api/Accounts/assign-role", assignRoleDto);

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Verify role assignment by getting the user again
        var verifyResponse = await client.GetAsync($"/api/Accounts/{userIdToAssign}"); // Get the specific user
        verifyResponse.EnsureSuccessStatusCode();
        var updatedUser = await verifyResponse.Content.ReadFromJsonAsync<UserDto>();
        Assert.NotNull(updatedUser);
        Assert.Contains(uniqueRoleName, updatedUser.Roles!); // Check for the unique role, added !

        // Cleanup: Unassign the unique role using the new endpoint
        var unassignRoleDto = new UnassignRoleDto { UserId = userIdToAssign, RoleName = uniqueRoleName };
        var cleanupResponse = await client.PostAsJsonAsync("/api/Accounts/unassign-role", unassignRoleDto);
        cleanupResponse.EnsureSuccessStatusCode(); // Ensure cleanup was successful

        // Optional: Verify cleanup
        var verifyCleanupResponse = await client.GetAsync($"/api/Accounts/{userIdToAssign}");
        verifyCleanupResponse.EnsureSuccessStatusCode();
        var userAfterCleanup = await verifyCleanupResponse.Content.ReadFromJsonAsync<UserDto>();
        Assert.NotNull(userAfterCleanup);
        Assert.DoesNotContain(uniqueRoleName, userAfterCleanup.Roles!); // Added !
        // We don't delete the role itself to keep cleanup simpler
    }

    [Fact]
    public async Task AssignRole_WhenRoleDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync(TestConstants.SuperAdminEmail);
        string userIdToAssign = await GetUserIdByEmail(TestConstants.Org1CashierEmail);
        var assignRoleDto = new AssignRoleDto { UserId = userIdToAssign, RoleName = "NonExistentRole" };

        // Act
        var response = await client.PostAsJsonAsync("/api/Accounts/assign-role", assignRoleDto);

        // Assert
        // Controller returns 404 when service indicates role not found
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

     [Fact]
    public async Task AssignRole_WhenUserDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync(TestConstants.SuperAdminEmail);
        var assignRoleDto = new AssignRoleDto { UserId = "non-existent-user-id", RoleName = "OrgAdmin" };

        // Act
        var response = await client.PostAsJsonAsync("/api/Accounts/assign-role", assignRoleDto);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AssignRole_WhenOrgAdmin_ReturnsForbidden()
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync(TestConstants.Org1AdminEmail);
        string userIdToAssign = await GetUserIdByEmail(TestConstants.Org1CashierEmail);
        var assignRoleDto = new AssignRoleDto { UserId = userIdToAssign, RoleName = "AreaAdmin" };

        // Act
        var response = await client.PostAsJsonAsync("/api/Accounts/assign-role", assignRoleDto);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }


    // --- POST /api/Accounts/register ---

    [Fact]
    public async Task RegisterUser_WhenOrgAdmin_AssignsCorrectOrganizationId()
    {
        // Arrange
        var orgAdminClient = await _factory.CreateAuthenticatedClientAsync(TestConstants.Org1AdminEmail); // Org1 Admin
        var newUserEmail = $"newuser.org1.{Guid.NewGuid()}@test.org";
        var registerDto = new RegisterDto
        {
            Email = newUserEmail,
            Password = TestConstants.DefaultPassword,
            ConfirmPassword = TestConstants.DefaultPassword,
            FirstName = "New",
            LastName = "Org1User"
        };

        // Act: OrgAdmin registers a new user
        var registerResponse = await orgAdminClient.PostAsJsonAsync("/api/Accounts/register", registerDto);

        // Assert: Registration successful
        registerResponse.EnsureSuccessStatusCode(); // Expect 200 OK or 201 Created
        // We need the user's ID or a way to fetch them to verify OrgId
        // Let's assume the response body might contain the user ID or we fetch all users as SuperAdmin

        // Arrange: Get SuperAdmin client to fetch all users for verification
        var superAdminClient = await _factory.CreateAuthenticatedClientAsync(TestConstants.SuperAdminEmail);

        // Act: Fetch all users to find the new one
        var getUsersResponse = await superAdminClient.GetAsync("/api/Accounts");
        getUsersResponse.EnsureSuccessStatusCode();
        var allUsers = await getUsersResponse.Content.ReadFromJsonAsync<List<UserDto>>();
        var newUser = allUsers?.FirstOrDefault(u => u.Email.Equals(newUserEmail, StringComparison.OrdinalIgnoreCase));

        // Assert: New user found and has the correct OrganizationId
        Assert.NotNull(newUser);
        Assert.Equal(TestConstants.Org1Id, newUser.OrganizationId); // Verify OrgId matches Org1

        // Cleanup: Delete the created user (optional but good practice)
        if (newUser != null)
        {
            await superAdminClient.DeleteAsync($"/api/Accounts/{newUser.Id}");
        }
    }

    [Fact]
    public async Task RegisterUser_WhenSuperAdmin_WithValidOrgId_AssignsCorrectOrganizationId()
    {
        // Arrange
        var superAdminClient = await _factory.CreateAuthenticatedClientAsync(TestConstants.SuperAdminEmail);
        var newUserEmail = $"newuser.sa.{Guid.NewGuid()}@test.org";
        var registerDto = new RegisterDto
        {
            Email = newUserEmail,
            Password = TestConstants.DefaultPassword,
            ConfirmPassword = TestConstants.DefaultPassword,
            FirstName = "NewSA",
            LastName = "User",
            OrganizationId = TestConstants.Org2Id // SuperAdmin explicitly assigns to Org2
        };

        // Act: SuperAdmin registers a new user specifying OrgId
        var registerResponse = await superAdminClient.PostAsJsonAsync("/api/Accounts/register", registerDto);

        // Assert: Registration successful
        registerResponse.EnsureSuccessStatusCode(); // Expect 200 OK or 201 Created

        // Act: Fetch all users to find the new one
        var getUsersResponse = await superAdminClient.GetAsync("/api/Accounts");
        getUsersResponse.EnsureSuccessStatusCode();
        var allUsers = await getUsersResponse.Content.ReadFromJsonAsync<List<UserDto>>();
        var newUser = allUsers?.FirstOrDefault(u => u.Email.Equals(newUserEmail, StringComparison.OrdinalIgnoreCase));

        // Assert: New user found and has the correct OrganizationId
        Assert.NotNull(newUser);
        Assert.Equal(TestConstants.Org2Id, newUser.OrganizationId); // Verify OrgId matches the one provided

        // Cleanup: Delete the created user
        if (newUser != null)
        {
            await superAdminClient.DeleteAsync($"/api/Accounts/{newUser.Id}");
        }
    }

    [Fact]
    public async Task RegisterUser_WhenSuperAdmin_WithoutOrgId_ReturnsBadRequest()
    {
        // Arrange
        var superAdminClient = await _factory.CreateAuthenticatedClientAsync(TestConstants.SuperAdminEmail);
        var newUserEmail = $"newuser.sa.noorg.{Guid.NewGuid()}@test.org";
        var registerDto = new RegisterDto
        {
            Email = newUserEmail,
            Password = TestConstants.DefaultPassword,
            ConfirmPassword = TestConstants.DefaultPassword,
            FirstName = "NewSA",
            LastName = "NoOrg",
            OrganizationId = null // Explicitly null or omitted
        };

        // Act: SuperAdmin attempts to register without OrgId
        var registerResponse = await superAdminClient.PostAsJsonAsync("/api/Accounts/register", registerDto);

        // Assert: Should fail because SuperAdmin must specify OrgId
        Assert.Equal(HttpStatusCode.BadRequest, registerResponse.StatusCode);
        // Optional: Check error message if needed
        // var error = await registerResponse.Content.ReadFromJsonAsync<ValidationProblemDetails>(); // Or IdentityError structure
        // Assert.Contains("SuperAdmin must specify an OrganizationId", error?.Errors.FirstOrDefault().Value.FirstOrDefault());
    }

     [Fact]
    public async Task RegisterUser_WhenSuperAdmin_WithInvalidOrgId_ReturnsBadRequest()
    {
        // Arrange
        var superAdminClient = await _factory.CreateAuthenticatedClientAsync(TestConstants.SuperAdminEmail);
        var newUserEmail = $"newuser.sa.invalidorg.{Guid.NewGuid()}@test.org";
        var invalidOrgId = 99999; // An ID that definitely doesn't exist
        var registerDto = new RegisterDto
        {
            Email = newUserEmail,
            Password = TestConstants.DefaultPassword,
            ConfirmPassword = TestConstants.DefaultPassword,
            FirstName = "NewSA",
            LastName = "InvalidOrg",
            OrganizationId = invalidOrgId
        };

        // Act: SuperAdmin attempts to register with an invalid OrgId
        var registerResponse = await superAdminClient.PostAsJsonAsync("/api/Accounts/register", registerDto);

        // Assert: Should fail because the specified OrgId doesn't exist
        Assert.Equal(HttpStatusCode.BadRequest, registerResponse.StatusCode); // Service returns error, controller maps to BadRequest
        // Optional: Check error message
        // var error = await registerResponse.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        // Assert.Contains($"Organization with ID {invalidOrgId} not found", error?.Errors.FirstOrDefault().Value.FirstOrDefault());
    }


    // --- POST /api/Accounts/unassign-role ---

    [Fact]
    public async Task UnassignRole_WhenSuperAdmin_UnassignsRoleSuccessfully()
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync(TestConstants.SuperAdminEmail);
        string userIdToModify = await GetUserIdByEmail(TestConstants.Org1CashierEmail); // Use Org1 Cashier
        var roleToUnassign = "Cashier"; // Unassign the existing Cashier role

        // Ensure the user has the role first (redundant for seeded data, but good practice)
        var userCheckResponse = await client.GetAsync($"/api/Accounts/{userIdToModify}");
        userCheckResponse.EnsureSuccessStatusCode();
        var userBefore = await userCheckResponse.Content.ReadFromJsonAsync<UserDto>();
        Assert.NotNull(userBefore);
        Assert.Contains(roleToUnassign, userBefore.Roles);

        var unassignRoleDto = new UnassignRoleDto { UserId = userIdToModify, RoleName = roleToUnassign };

        // Act
        var response = await client.PostAsJsonAsync("/api/Accounts/unassign-role", unassignRoleDto);

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Verify role removal by getting the user again
        var verifyResponse = await client.GetAsync($"/api/Accounts/{userIdToModify}");
        verifyResponse.EnsureSuccessStatusCode();
        var updatedUser = await verifyResponse.Content.ReadFromJsonAsync<UserDto>();
        Assert.NotNull(updatedUser);
        Assert.DoesNotContain(roleToUnassign, updatedUser.Roles!); // Added !

        // Cleanup removed: Re-assigning the role might interfere with subsequent tests using InMemory DB.
        // The state should ideally reset, but removing this avoids potential conflicts.
        // var assignRoleDto = new AssignRoleDto { UserId = userIdToModify, RoleName = roleToUnassign };
        // await client.PostAsJsonAsync("/api/Accounts/assign-role", assignRoleDto);
    }

    [Fact]
    public async Task UnassignRole_WhenRoleDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync(TestConstants.SuperAdminEmail);
        string userIdToModify = await GetUserIdByEmail(TestConstants.Org1CashierEmail);
        var unassignRoleDto = new UnassignRoleDto { UserId = userIdToModify, RoleName = "NonExistentRole" };

        // Act
        var response = await client.PostAsJsonAsync("/api/Accounts/unassign-role", unassignRoleDto);

        // Assert
        // Controller returns 404 when service indicates role not found
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UnassignRole_WhenUserDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync(TestConstants.SuperAdminEmail);
        var unassignRoleDto = new UnassignRoleDto { UserId = "non-existent-user-id", RoleName = "Cashier" };

        // Act
        var response = await client.PostAsJsonAsync("/api/Accounts/unassign-role", unassignRoleDto);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UnassignRole_WhenUserNotInRole_ReturnsSuccess()
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync(TestConstants.SuperAdminEmail);
        string userIdToModify = await GetUserIdByEmail(TestConstants.Org1CashierEmail); // Org1 Cashier
        var roleToUnassign = "SuperAdmin"; // A role the cashier doesn't have

        // Ensure the user does not have the role first
        var userCheckResponse = await client.GetAsync($"/api/Accounts/{userIdToModify}");
        userCheckResponse.EnsureSuccessStatusCode();
        var userBefore = await userCheckResponse.Content.ReadFromJsonAsync<UserDto>();
        Assert.NotNull(userBefore);
        Assert.DoesNotContain(roleToUnassign, userBefore.Roles!); // Added !

        var unassignRoleDto = new UnassignRoleDto { UserId = userIdToModify, RoleName = roleToUnassign };

        // Act
        var response = await client.PostAsJsonAsync("/api/Accounts/unassign-role", unassignRoleDto);

        // Assert
        // Service returns success if user is not in the role, controller maps to OK
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UnassignRole_WhenOrgAdmin_ReturnsForbidden()
    {
        // Arrange
        var client = await _factory.CreateAuthenticatedClientAsync(TestConstants.Org1AdminEmail);
        string userIdToModify = await GetUserIdByEmail(TestConstants.Org1CashierEmail);
        var unassignRoleDto = new UnassignRoleDto { UserId = userIdToModify, RoleName = "Cashier" };

        // Act
        var response = await client.PostAsJsonAsync("/api/Accounts/unassign-role", unassignRoleDto);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }


    // --- TODO: Add tests for GET /api/Accounts/{userId} (needs implementation) ---

}
