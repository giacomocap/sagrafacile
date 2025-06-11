using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using SagraFacile.NET.API.Data;
using SagraFacile.NET.API.Models;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions; // Added for slug generation
using System.Threading.Tasks;
using Moq; // Added for mocking
using SagraFacile.NET.API.Services.Interfaces; // Added for IEmailService

namespace SagraFacile.NET.API.Tests.Integration;

// Moved factory to its own file for better organization and accessibility
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    // Flag to ensure seeding only happens once per factory instance lifetime
    private static bool _databaseSeeded = false;
    private static readonly object _seedLock = new object();

    // Store configuration for JWT settings
    private IConfiguration _configuration = null!;

    // Expose the mock for verification in tests
    public Mock<IEmailService> MockEmailService { get; }

    public CustomWebApplicationFactory()
    {
        // Initialize the mock here
        MockEmailService = new Mock<IEmailService>();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Remove the app's DbContext registration
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            // Add DbContext using an in-memory database with a fixed name for this factory instance
            // This ensures scopes within the same factory instance share the same DB state.
            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseInMemoryDatabase("InMemoryDbForTesting"); // Fixed name for shared state within factory instance
            });

            // Ensure Identity services are registered (Ensure this is compatible with AddDbContext scope)
            services.AddIdentityCore<User>(options => { /* Configure Identity options if needed */ })
                    .AddRoles<IdentityRole>()
                    .AddEntityFrameworkStores<ApplicationDbContext>()
                    .AddDefaultTokenProviders(); // Ensure token providers are added

            // Explicitly add RoleManager<IdentityRole> - might help resolve DI issues in test scope
            services.AddScoped<RoleManager<IdentityRole>>();

            // Explicitly add HttpContextAccessor for accessing ClaimsPrincipal in services
            services.AddHttpContextAccessor();

            // --- Mock Email Service ---
            // Remove the real service registration if it exists (it shouldn't in Testing env)
            var emailServiceDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IEmailService));
            if (emailServiceDescriptor != null)
            {
                services.Remove(emailServiceDescriptor);
            }
            // Register the mock instance
            services.AddSingleton(MockEmailService.Object); // Register the mocked object instance
            // --- End Mock Email Service ---

            var sp = services.BuildServiceProvider();
            _configuration = sp.GetRequiredService<IConfiguration>();

            // Ensure seeding only happens once using a static flag and lock
            lock (_seedLock)
            {
                 if (!_databaseSeeded)
                 {
                    using (var scope = sp.CreateScope())
                    {
                        var scopedServices = scope.ServiceProvider;
                        var db = scopedServices.GetRequiredService<ApplicationDbContext>();
                        var userManager = scopedServices.GetRequiredService<UserManager<User>>();
                        var roleManager = scopedServices.GetRequiredService<RoleManager<IdentityRole>>();
                        var logger = scopedServices.GetRequiredService<ILogger<CustomWebApplicationFactory>>();

                        db.Database.EnsureCreated(); // Ensure DB is created before seeding

                        try
                        {
                            // Wait for seeding to complete using GetAwaiter().GetResult() for safer blocking
                            SeedDatabaseAsync(db, userManager, roleManager).GetAwaiter().GetResult();
                            _databaseSeeded = true; // Mark as seeded only on success
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "An error occurred seeding the database. Error: {Message}", ex.InnerException?.Message ?? ex.Message);
                            // Optionally rethrow or handle more gracefully depending on test requirements
                            throw; // Rethrow to fail test setup if seeding is critical
                        }
                    }
                 }
            }
        });
    }

    // Corrected Idempotent Seeding Logic
    private static async Task SeedDatabaseAsync(ApplicationDbContext context, UserManager<User> userManager, RoleManager<IdentityRole> roleManager)
    {
        // Seed Roles first
        await EnsureRoleExists(roleManager, "SuperAdmin");
        await EnsureRoleExists(roleManager, "Admin"); // Changed OrgAdmin to Admin
        await EnsureRoleExists(roleManager, "Cashier");
        await EnsureRoleExists(roleManager, "Waiter"); // Added Waiter role

        // Seed Organizations
        var org1 = await EnsureOrganizationExists(context, TestConstants.Org1Id, TestConstants.Org1Name); // Use Constants
        var org2 = await EnsureOrganizationExists(context, TestConstants.Org2Id, TestConstants.Org2Name); // Use Constants
        var systemOrg = await EnsureOrganizationExists(context, TestConstants.SystemOrgId, TestConstants.SystemOrgName); // Seed System Org (use ID 3 as per memory/constants)

        // Seed Users and assign roles
        // Assign SuperAdmin to the System Organization
        var superAdmin = await EnsureUserExists(userManager, context, TestConstants.SuperAdminEmail, "Super", "Admin", systemOrg.Id, TestConstants.DefaultPassword); // Use Constants & System Org ID
        await EnsureUserRole(userManager, context, superAdmin, "SuperAdmin");

        // Assign Org Admins/Cashiers to their respective operational organizations
        var org1Admin = await EnsureUserExists(userManager, context, TestConstants.Org1AdminEmail, "Org1", "Admin", org1.Id, TestConstants.DefaultPassword); // Use Constants
        await EnsureUserRole(userManager, context, org1Admin, "Admin"); // Changed OrgAdmin to Admin

        var org2Admin = await EnsureUserExists(userManager, context, TestConstants.Org2AdminEmail, "Org2", "Admin", org2.Id, TestConstants.DefaultPassword); // Use Constants
        await EnsureUserRole(userManager, context, org2Admin, "Admin"); // Changed OrgAdmin to Admin

        var org1Cashier = await EnsureUserExists(userManager, context, TestConstants.Org1CashierEmail, TestConstants.Org1CashierFirstName, TestConstants.Org1CashierLastName, org1.Id, TestConstants.DefaultPassword); // Use Constants
        await EnsureUserRole(userManager, context, org1Cashier, "Cashier");

        var org1Waiter = await EnsureUserExists(userManager, context, TestConstants.Org1WaiterEmail, TestConstants.Org1WaiterFirstName, TestConstants.Org1WaiterLastName, org1.Id, TestConstants.DefaultPassword); // Added Waiter user
        await EnsureUserRole(userManager, context, org1Waiter, "Waiter"); // Assign Waiter role

        // Seed Areas
        var area1 = await EnsureAreaExists(context, 1, "Org1 Area 1", org1.Id);
        var area2 = await EnsureAreaExists(context, 2, "Org2 Area 1", org2.Id);
        var area3 = await EnsureAreaExists(context, 3, "Org1 Area 2", org1.Id);

        // Seed Menu Categories
        var category1 = await EnsureMenuCategoryExists(context, 1, "Primi", area1.Id); // Should be Area 1
        var category2 = await EnsureMenuCategoryExists(context, 2, "Antipasti", area2.Id); // Should be Area 2
        var category3 = await EnsureMenuCategoryExists(context, 3, "Secondi", area1.Id); // Should be Area 1 (Mistake in original? Let's keep for now, test should catch if wrong) -> Corrected: Should be Area 3? No, Area 3 is Org1 Area 2. Let's put it in Area 3.
        category3 = await EnsureMenuCategoryExists(context, 3, "Secondi", area3.Id); // Corrected: Category 3 in Area 3 (Org1 Area 2)

        // Seed Menu Items
        var item1 = await EnsureMenuItemExists(context, 1, "Pasta al Pesto", 8.00m, category1.Id, false, null); // Cat 1 (Area 1)
        var item2 = await EnsureMenuItemExists(context, 2, "Tagliatelle al Ragu", 9.00m, category1.Id, false, null); // Cat 1 (Area 1)
        var item3 = await EnsureMenuItemExists(context, 3, "Olive Ascolane", 5.00m, category2.Id, false, null); // Cat 2 (Area 2)
        var item4 = await EnsureMenuItemExists(context, 4, "Grigliata Mista", 15.00m, category3.Id, true, "Specificare tipo carne"); // Cat 3 (Area 3)

        // Seed Orders (Need user IDs)
        // Note: Order IDs are auto-generated, so we don't specify them here.
        // We use explicit IDs in EnsureOrderExists only for the check, not for insertion.
        await EnsureOrderExists(context, 1, org1.Id, area1.Id, org1Cashier.Id, new List<OrderItemSeedDto> // Order in Org1/Area1 by Org1Cashier
        {
            new OrderItemSeedDto { MenuItemId = item1.Id, Quantity = 2, UnitPrice = item1.Price },
            new OrderItemSeedDto { MenuItemId = item2.Id, Quantity = 1, UnitPrice = item2.Price }
        }); // Order 1 remains Completed (default)
         await EnsureOrderExists(context, 2, org2.Id, area2.Id, org2Admin.Id, new List<OrderItemSeedDto> // Order in Org2/Area2 by Org2Admin
        {
            new OrderItemSeedDto { MenuItemId = item3.Id, Quantity = 3, UnitPrice = item3.Price }
        });
         await EnsureOrderExists(context, 3, org1.Id, area3.Id, org1Cashier.Id, new List<OrderItemSeedDto> // Order in Org1/Area3 by Org1Cashier
        {
            new OrderItemSeedDto { MenuItemId = item4.Id, Quantity = 1, UnitPrice = item4.Price, Note = "Ben cotta" }
        }); // Order 3 remains Completed (default)
         await EnsureOrderExists(context, 4, org1.Id, area1.Id, null, new List<OrderItemSeedDto> // PreOrder in Org1/Area1 (no cashier)
        {
            new OrderItemSeedDto { MenuItemId = item1.Id, Quantity = 1, UnitPrice = item1.Price }
        }, OrderStatus.PreOrder); // Order 4 remains PreOrder
         await EnsureOrderExists(context, 5, org1.Id, area1.Id, org1Cashier.Id, new List<OrderItemSeedDto> // Order 5 in Org1/Area1 by Cashier (Paid)
        {
            new OrderItemSeedDto { MenuItemId = item2.Id, Quantity = 1, UnitPrice = item2.Price }
        }, OrderStatus.Paid); // Set Order 5 status to Paid
         await EnsureOrderExists(context, 6, org1.Id, area1.Id, null, new List<OrderItemSeedDto> // Order 6 PreOrder in Org1/Area1 (PreOrder)
        {
            new OrderItemSeedDto { MenuItemId = item1.Id, Quantity = 3, UnitPrice = item1.Price }
        }, OrderStatus.PreOrder); // Set Order 6 status to PreOrder
    }

    // --- Seeding Helper Methods ---
    private static async Task EnsureRoleExists(RoleManager<IdentityRole> roleManager, string roleName)
    {
        if (!await roleManager.RoleExistsAsync(roleName))
        {
            await roleManager.CreateAsync(new IdentityRole(roleName));
        }
    }

    // DTO for seeding order items easily
    private class OrderItemSeedDto
    {
        public int MenuItemId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public string? Note { get; set; }
    }

    // Updated helper to accept optional status and nullable cashierId
    private static async Task EnsureOrderExists(ApplicationDbContext context, int checkOrderId, int orgId, int areaId, string? cashierId, List<OrderItemSeedDto> items, OrderStatus status = OrderStatus.Completed)
    {
        // Generate a predictable string ID for testing
        string predictableOrderId = $"SEED-ORDER-{checkOrderId}";

        // Check if order already exists using the predictable ID
        if (await context.Orders.AnyAsync(o => o.Id == predictableOrderId))
        {
            // Order already seeded, return to prevent duplicates
            return;
        }

        // Ensure related entities exist ONLY if creating the order
        if (!await context.Organizations.AnyAsync(o => o.Id == orgId)) throw new InvalidOperationException($"Cannot seed order, organization {orgId} does not exist.");
        if (!await context.Areas.AnyAsync(a => a.Id == areaId)) throw new InvalidOperationException($"Cannot seed order, area {areaId} does not exist.");
        // Allow null cashierId for PreOrders
        if (cashierId != null && !await context.Users.AnyAsync(u => u.Id == cashierId)) throw new InvalidOperationException($"Cannot seed order, cashier {cashierId} does not exist.");

        decimal totalAmount = 0;
        var orderItems = new List<OrderItem>();
        foreach(var itemDto in items)
        {
             if (!await context.MenuItems.AnyAsync(mi => mi.Id == itemDto.MenuItemId)) throw new InvalidOperationException($"Cannot seed order item, menu item {itemDto.MenuItemId} does not exist.");
             orderItems.Add(new OrderItem
             {
                 MenuItemId = itemDto.MenuItemId,
                 Quantity = itemDto.Quantity,
                 UnitPrice = itemDto.UnitPrice,
                 Note = itemDto.Note
              // OrderId set by EF Core
             });
             totalAmount += itemDto.Quantity * itemDto.UnitPrice;
        }

        // Generate a predictable string ID for testing
        // string predictableOrderId = $"SEED-ORDER-{checkOrderId}";

        var order = new Order
        {
            Id = predictableOrderId, // Assign predictable string ID
            OrganizationId = orgId,
            AreaId = areaId,
            CashierId = cashierId, // Can be null now
            // OrderNumber removed
            Status = status, // Use provided status
            OrderDateTime = DateTime.UtcNow.AddMinutes(-checkOrderId), // Stagger times slightly
            TotalAmount = totalAmount,
            OrderItems = orderItems
        };

        context.Orders.Add(order);
        await context.SaveChangesAsync(); // Save order and its items
    }

    private static async Task<MenuItem> EnsureMenuItemExists(ApplicationDbContext context, int id, string name, decimal price, int categoryId, bool isNoteRequired, string? noteSuggestion)
    {
        var menuItem = await context.MenuItems.FindAsync(id);
        if (menuItem == null)
        {
            // Ensure the Category actually exists before assigning FK
            if (!await context.MenuCategories.AnyAsync(mc => mc.Id == categoryId))
            {
                 throw new InvalidOperationException($"Cannot seed menu item {name}, category {categoryId} does not exist.");
            }
            menuItem = new MenuItem
            {
                Id = id, // Use provided ID for seeding
                Name = name,
                Price = price,
                MenuCategoryId = categoryId,
                IsNoteRequired = isNoteRequired,
                NoteSuggestion = noteSuggestion
            };
            context.MenuItems.Add(menuItem);
            await context.SaveChangesAsync(); // Save item creation immediately
        }
        else // Menu item exists, ensure its properties match the desired seed state
        {
             bool updated = false;
             if (menuItem.Name != name) { menuItem.Name = name; updated = true; }
             if (menuItem.Price != price) { menuItem.Price = price; updated = true; }
             if (menuItem.MenuCategoryId != categoryId) { menuItem.MenuCategoryId = categoryId; updated = true; }
             if (menuItem.IsNoteRequired != isNoteRequired) { menuItem.IsNoteRequired = isNoteRequired; updated = true; }
             if (menuItem.NoteSuggestion != noteSuggestion) { menuItem.NoteSuggestion = noteSuggestion; updated = true; }
             if (updated) { await context.SaveChangesAsync(); }
        }
        return menuItem;
    }

    private static async Task<MenuCategory> EnsureMenuCategoryExists(ApplicationDbContext context, int id, string name, int areaId)
    {
        var category = await context.MenuCategories.FindAsync(id);
        if (category == null)
        {
            // Ensure the Area actually exists before assigning FK
            if (!await context.Areas.AnyAsync(a => a.Id == areaId))
            {
                 throw new InvalidOperationException($"Cannot seed menu category {name}, area {areaId} does not exist.");
            }
            category = new MenuCategory { Id = id, Name = name, AreaId = areaId }; // Use provided ID
            context.MenuCategories.Add(category);
            await context.SaveChangesAsync(); // Save category creation immediately
        }
        else // Category exists, ensure its properties match the desired seed state
        {
            bool updated = false;
            if (category.Name != name) { category.Name = name; updated = true; }
            if (category.AreaId != areaId) { category.AreaId = areaId; updated = true; }
            if (updated) { await context.SaveChangesAsync(); }
        }
        return category;
    }

    private static async Task<Organization> EnsureOrganizationExists(ApplicationDbContext context, int id, string name)
    {
        var organization = await context.Organizations.FindAsync(id);
        bool isNew = organization == null;

        if (isNew)
        {
            organization = new Organization { Id = id, Name = name, Slug = GenerateSlug(name) }; // Generate slug on creation
            context.Organizations.Add(organization);
            await context.SaveChangesAsync();
        }
        else // Organization exists, ensure its properties match the desired seed state
        {
            bool nameUpdated = false;
            if (organization.Name != name)
            {
                organization.Name = name;
                nameUpdated = true;
            }
            // Always generate slug based on current name and update if different or missing
            var generatedSlug = GenerateSlug(name);
            if (organization.Slug != generatedSlug)
            {
                organization.Slug = generatedSlug;
                await context.SaveChangesAsync(); // Save if slug was updated
            }
            else if (nameUpdated) // Save if only name changed but slug was already correct
            {
                 await context.SaveChangesAsync();
            }
        }
        return organization;
    }

     private static async Task<User> EnsureUserExists(UserManager<User> userManager, ApplicationDbContext context, string userName, string firstName, string lastName, int organizationId, string password)
    {
        var user = await userManager.FindByNameAsync(userName);
        if (user == null)
        {
            if (!await context.Organizations.AnyAsync(o => o.Id == organizationId))
            {
                 throw new InvalidOperationException($"Cannot seed user {userName}, organization {organizationId} does not exist.");
            }
            user = new User
            {
                UserName = userName,
                Email = userName,
                FirstName = firstName,
                LastName = lastName,
                OrganizationId = organizationId,
                EmailConfirmed = true // Assume confirmed for testing
            };
            var result = await userManager.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                throw new Exception($"Failed to create user {userName}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
            }
            // No need for SaveChangesAsync here as CreateAsync handles it
        }
        else // User exists, ensure properties match desired seed state (except password)
        {
            bool updated = false;
            if (user.FirstName != firstName) { user.FirstName = firstName; updated = true; }
            if (user.LastName != lastName) { user.LastName = lastName; updated = true; }
            if (user.OrganizationId != organizationId) { user.OrganizationId = organizationId; updated = true; }
            if (user.Email != userName) { user.Email = userName; updated = true; } // Ensure email matches username if changed
            if (!user.EmailConfirmed) { user.EmailConfirmed = true; updated = true; } // Ensure confirmed

            if (updated)
            {
                var result = await userManager.UpdateAsync(user);
                if (!result.Succeeded)
                {
                     throw new Exception($"Failed to update user {userName}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
                }
            }
        }
        return user;
    }

    private static async Task EnsureUserRole(UserManager<User> userManager, ApplicationDbContext context, User user, string roleName)
    {
        // This check is inherently idempotent
        if (!await userManager.IsInRoleAsync(user, roleName))
        {
            var result = await userManager.AddToRoleAsync(user, roleName);
             if (!result.Succeeded)
            {
                 throw new Exception($"Failed to add user {user.UserName} to role {roleName}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
            }
            // No need for SaveChangesAsync here as AddToRoleAsync handles it (usually)
        }
    }

     private static async Task<Area> EnsureAreaExists(ApplicationDbContext context, int id, string name, int organizationId)
    {
        var area = await context.Areas.FindAsync(id);
        bool isNew = area == null;

        if (isNew)
        {
            if (!await context.Organizations.AnyAsync(o => o.Id == organizationId))
            {
                 throw new InvalidOperationException($"Cannot seed area {name}, organization {organizationId} does not exist.");
            }
            area = new Area { Id = id, Name = name, OrganizationId = organizationId, Slug = GenerateSlug(name) }; // Generate slug on creation
            context.Areas.Add(area);
            await context.SaveChangesAsync();
        }
         else // Area exists, ensure its properties match the desired seed state
        {
            bool nameUpdated = false;
             if (area.Name != name)
             {
                 area.Name = name;
                 nameUpdated = true;
             }
             if (area.OrganizationId != organizationId) { area.OrganizationId = organizationId; nameUpdated = true; } // Consider nameUpdated true if org changes too? Or separate flag? Let's assume name change implies potential slug change.

            // Always generate slug based on current name and update if different or missing
            var generatedSlug = GenerateSlug(name);
            if (area.Slug != generatedSlug)
            {
                area.Slug = generatedSlug;
                await context.SaveChangesAsync(); // Save if slug was updated
            }
            else if (nameUpdated) // Save if only name/org changed but slug was already correct
            {
                 await context.SaveChangesAsync();
            }
        }
        return area;
    }

    // Simple slug generation helper (copied from OrganizationService)
    private static string GenerateSlug(string phrase)
    {
        string str = phrase.ToLowerInvariant();
        // invalid chars           \s+
        str = Regex.Replace(str, @"[^a-z0-9\s-]", ""); // remove invalid chars
        str = Regex.Replace(str, @"\s+", " ").Trim(); // convert multiple spaces into one space
        str = str.Substring(0, str.Length <= 100 ? str.Length : 100).Trim(); // cut and trim
        str = Regex.Replace(str, @"\s", "-"); // replace spaces with hyphens
        return str;
    }

    // --- Authentication Helper Methods ---
    public async Task<HttpClient> CreateAuthenticatedClientAsync(string userName = "superadmin@test.org")
    {
        var clientOptions = new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        };
        var client = CreateClient(clientOptions);
        var token = await GenerateJwtTokenAsync(userName);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<string> GenerateJwtTokenAsync(string userName)
    {
        using var scope = Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<CustomWebApplicationFactory>>();

        // No need to EnsureCreated here, should be done during setup

        var user = await userManager.FindByNameAsync(userName);
        if (user == null)
        {
            // Fallback lookup in case UserManager cache is stale in test scope
            user = await dbContext.Users.FirstOrDefaultAsync(u => u.UserName == userName);
        }

        if (user == null)
        {
            var availableUsers = await dbContext.Users.Select(u => u.UserName).ToListAsync();
            logger.LogError($"Test user {userName} not found during token generation. Available users: {string.Join(", ", availableUsers)}");
            throw new Exception($"Test user {userName} not found during token generation. Ensure seeding is correct and complete.");
        }

        var roles = await userManager.GetRolesAsync(user);

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty), // Use null-coalescing
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim("organizationId", user.OrganizationId.ToString())
        };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var jwtSettings = _configuration.GetSection("Jwt");
        var keyString = jwtSettings["Key"];
        var issuer = jwtSettings["Issuer"];
        var audience = jwtSettings["Audience"];

        if (string.IsNullOrEmpty(keyString) || string.IsNullOrEmpty(issuer) || string.IsNullOrEmpty(audience))
        {
            throw new InvalidOperationException("JWT Key, Issuer, or Audience not configured correctly in appsettings for testing.");
        }

        // Use null-forgiving operator (!) since we've checked for null/empty above
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyString!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer!,
            audience: audience!,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
