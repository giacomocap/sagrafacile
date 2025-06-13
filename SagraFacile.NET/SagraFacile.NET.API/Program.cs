using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SagraFacile.NET.API.Data;
using SagraFacile.NET.API.Models;
using SagraFacile.NET.API.Services; // Add using for Services
using SagraFacile.NET.API.Services.Interfaces; // Add using for Service Interfaces
using SagraFacile.NET.API.Hubs; // Add using for SignalR Hubs
using Microsoft.AspNetCore.Authentication.JwtBearer; // Added for JWT Authentication
using Microsoft.IdentityModel.Tokens; // Added for Token Validation Parameters
using System.Text;
using System.Security.Claims; // Added for Encoding
using SagraFacile.NET.API.BackgroundServices; // Add this using

// Ensure extended encodings are available
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

var builder = WebApplication.CreateBuilder(args);

// Define CORS policy name
var MyAllowSpecificOrigins = "_myAllowSpecificOrigins";

// Add services to the container.

// 1. Add DbContext
// Conditionally register Npgsql provider based on environment
if (!builder.Environment.IsEnvironment("Testing")) // Use a custom "Testing" environment name
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseNpgsql(connectionString)); // Use PostgreSQL provider
}
// The DbContext will be added by the CustomWebApplicationFactory when Environment is "Testing"

// 2. Add Identity
builder.Services.AddIdentity<User, IdentityRole>(options => options.SignIn.RequireConfirmedAccount = true) // Configure Identity options as needed
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders(); // Add default token providers

// 2a. Add Authentication and JWT Bearer Configuration
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key not configured."))),
        // Explicitly tell the middleware which claim contains role information
        RoleClaimType = ClaimTypes.Role
    };
});


// 3. Add Custom Services (Scoped is typical for services using DbContext)
builder.Services.AddScoped<IOrganizationService, OrganizationService>();
builder.Services.AddScoped<IAreaService, AreaService>();
builder.Services.AddScoped<IMenuCategoryService, MenuCategoryService>();
builder.Services.AddScoped<IMenuItemService, MenuItemService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IAccountService, AccountService>(); // Register Account Service
builder.Services.AddScoped<IEmailService, EmailService>(); // Register Email Service
builder.Services.AddScoped<IKdsStationService, KdsStationService>(); // Register KDS Station Service
builder.Services.AddScoped<IDayService, DayService>(); // Register Day Service
builder.Services.AddScoped<ISyncConfigurationService, SyncConfigurationService>(); // Register Sync Configuration Service
builder.Services.AddScoped<IMenuSyncService, MenuSyncService>(); // Register Menu Sync Service
builder.Services.AddScoped<IPreOrderPollingService, PreOrderPollingService>(); // Register the polling logic service
builder.Services.AddScoped<IPrinterService, PrinterService>(); // Add Printer Service
builder.Services.AddScoped<IPrinterAssignmentService, PrinterAssignmentService>(); // Add Printer Assignment Service
builder.Services.AddScoped<ICashierStationService, CashierStationService>();
builder.Services.AddScoped<IQueueService, QueueService>(); // Add QueueService registration
builder.Services.AddScoped<IAdMediaItemService, AdMediaItemService>();
builder.Services.AddScoped<IAdAreaAssignmentService, AdAreaAssignmentService>();

// Register the Background Service conditionally
var enablePollingService = false; //builder.Configuration.GetValue<bool?>("ENABLE_PREORDER_POLLING_SERVICE");

// Explicitly check for null to distinguish from a "false" value.
// If the variable is not present at all, we default to true (as per docker-compose).
// If it's present and "false", we disable. If present and "true", we enable.
if (enablePollingService == null || enablePollingService == true)
{
    builder.Services.AddHostedService<PreOrderPollingBackgroundService>();
    Console.WriteLine("PreOrderPollingBackgroundService is ENABLED."); // Log for clarity
}
else
{
    Console.WriteLine("PreOrderPollingBackgroundService is DISABLED by configuration."); // Log for clarity
}


// 4. Configure Options Pattern for Settings
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));

// 5. Add SignalR
builder.Services.AddSignalR();

// 6. Add HttpClientFactory for making HTTP requests
builder.Services.AddHttpClient();

// Add HttpContextAccessor for accessing user claims in services
builder.Services.AddHttpContextAccessor();

// Optional: Configure a named client for the platform API
builder.Services.AddHttpClient("PreOrderPlatformClient", client =>
{
    // Configure base address or default headers if needed, although base address is dynamic
    // client.BaseAddress = new Uri(...);
    client.Timeout = TimeSpan.FromSeconds(30); // Example timeout
});

builder.Services.AddControllers();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Add CORS services
builder.Services.AddCors(options =>
{
    options.AddPolicy(name: MyAllowSpecificOrigins,
                      policy =>
                      {
                          policy.WithOrigins(
                                    "http://localhost:3000",    // Allow local dev server (HTTP)
                                    "https://localhost:3000",   // Allow local dev server (HTTPS)
                                    "http://192.168.1.219:3000", // Allow frontend from local network IP (HTTP)
                                    "https://192.168.1.219:3000", // Allow frontend from local network IP (HTTPS)
                                    "https://192.168.1.38:3000",
                                    "https://192.168.1.237:3000",
                                    "https://192.168.1.24:3000"
                                )
                                .AllowAnyHeader()
                                .AllowAnyMethod()
                                .AllowCredentials(); // Added for SignalR
                          // In production, you might want to be more restrictive
                          // with headers and methods allowed.
                          // For production builds (e.g., running on the same domain or specific domains):
                          // policy.WithOrigins("https://your-production-domain.com")
                          //       .AllowAnyHeader()
                          //       .AllowAnyMethod();
                      });
});


var app = builder.Build();

// Configure the HTTP request pipeline.
// Map OpenAPI/Swagger also in Testing environment for integration tests
if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    app.MapOpenApi();
}

// app.UseHttpsRedirection(); // Commented out for local dev to avoid certificate issues with mobile/network access

// Enable static file serving from wwwroot
app.UseStaticFiles();

// Add Routing middleware BEFORE Auth middleware
app.UseRouting(); // Explicitly add UseRouting

// Apply CORS middleware - AFTER UseRouting, BEFORE UseAuthentication, UseAuthorization
app.UseCors(MyAllowSpecificOrigins);

// Add Authentication middleware BEFORE Authorization
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Map SignalR Hub
app.MapHub<OrderHub>("/api/orderHub"); // Added /api prefix for consistency

// --- Apply Migrations ---
// Ensure the database is created and migrations are applied on startup.
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    // Avoid applying migrations during integration tests if using a separate test DB strategy
    if (!app.Environment.IsEnvironment("Testing"))
    {
        try
        {
            var context = services.GetRequiredService<ApplicationDbContext>();
            await context.Database.MigrateAsync(); // Apply pending migrations
            logger.LogInformation("Database migrations applied successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while applying database migrations.");
            // Consider stopping the application if migrations fail critically
            // throw;
        }
    }
}
// --- End Apply Migrations ---


// --- Seed System Organization ---
// Ensures a default organization exists for system users like SuperAdmin.
// Skip seeding in Testing environment as it's handled by the test factory
if (!app.Environment.IsEnvironment("Testing"))
{
    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;
        var logger = services.GetRequiredService<ILogger<Program>>();
        var dbContext = services.GetRequiredService<ApplicationDbContext>();
        string systemOrgName = "System";

        try
        {
            var systemOrg = await dbContext.Organizations.FirstOrDefaultAsync(o => o.Name == systemOrgName);
            if (systemOrg == null)
            {
                logger.LogInformation("Creating '{SystemOrgName}' organization.", systemOrgName);
                systemOrg = new Organization { Name = systemOrgName }; // Removed Description
                dbContext.Organizations.Add(systemOrg);
                await dbContext.SaveChangesAsync(); // Save immediately to get the ID
                logger.LogInformation("'{SystemOrgName}' organization created successfully with ID {OrgId}.", systemOrgName, systemOrg.Id);
            }
            else
            {
                logger.LogInformation("'{SystemOrgName}' organization already exists with ID {OrgId}.", systemOrgName, systemOrg.Id);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while seeding the System organization.");
            // Consider stopping the application
            // throw;
        }
    }
}
// --- End Seed System Organization ---


// --- Seed Roles ---
// This block ensures default roles exist in the database on startup.
// Skip seeding in Testing environment as it's handled by the test factory
if (!app.Environment.IsEnvironment("Testing"))
{
    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;
        var logger = services.GetRequiredService<ILogger<Program>>(); // Get logger
        try
        {
            var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
            // Removed "OrgAdmin" from the list
            string[] roleNames = { "SuperAdmin", "Admin", "AreaAdmin", "Cashier", "Waiter" }; // Renamed OrgAdmin to Admin, kept others
            IdentityResult roleResult;

            foreach (var roleName in roleNames)
            {
                var roleExist = await roleManager.RoleExistsAsync(roleName);
                if (!roleExist)
                {
                    // Create the roles and seed them to the database
                    roleResult = await roleManager.CreateAsync(new IdentityRole(roleName));
                    if (roleResult.Succeeded)
                    {
                        logger.LogInformation("Role '{RoleName}' created successfully.", roleName);
                    }
                    else
                    {
                        // Log errors during role creation
                        foreach (var error in roleResult.Errors)
                        {
                            logger.LogError("Error creating role '{RoleName}': {ErrorDescription}", roleName, error.Description);
                        }
                    }
                }
            }

            // --- Explicitly Remove Old "OrgAdmin" Role ---
            // string oldRoleNameToRemove = "OrgAdmin";
            // var oldRoleExists = await roleManager.RoleExistsAsync(oldRoleNameToRemove);
            // if (oldRoleExists)
            // {
            //     var orgAdminRole = await roleManager.FindByNameAsync(oldRoleNameToRemove);
            //     if (orgAdminRole != null)
            //     {
            //         var deleteResult = await roleManager.DeleteAsync(orgAdminRole);
            //         if (deleteResult.Succeeded)
            //         {
            //             logger.LogInformation("Successfully removed obsolete role '{OldRoleName}'.", oldRoleNameToRemove);
            //         }
            //         else
            //         {
            //             logger.LogError("Failed to remove obsolete role '{OldRoleName}'. Errors: {Errors}", oldRoleNameToRemove, string.Join(", ", deleteResult.Errors.Select(e => e.Description)));
            //         }
            //     }
            //     else
            //     {
            //          logger.LogWarning("Obsolete role '{OldRoleName}' was found by RoleExistsAsync but not by FindByNameAsync.", oldRoleNameToRemove);
            //     }
            // }
            // else
            // {
            //      logger.LogInformation("Obsolete role '{OldRoleName}' not found, no removal needed.", oldRoleNameToRemove);
            // }
            // --- End Remove Old Role ---

            logger.LogInformation("Role seeding completed.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while seeding the roles.");
            // Depending on the severity, you might want to stop the application
            // throw;
        }
    }
}
// --- End Seed Roles ---

// --- Seed SuperAdmin User ---
// This block ensures a default SuperAdmin user exists.
// Skip seeding in Testing environment as it's handled by the test factory
if (!app.Environment.IsEnvironment("Testing"))
{
    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;
        var logger = services.GetRequiredService<ILogger<Program>>();
        var userManager = services.GetRequiredService<UserManager<User>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>(); // Need RoleManager again
        var dbContext = services.GetRequiredService<ApplicationDbContext>(); // Get DbContext here as well

        try
        {
            string systemOrgName = "System"; // Define name again or pass from previous block
            string adminEmail = "superadmin@example.com";
            string adminPassword = "Password123!"; // Use a strong default password
            string adminRole = "SuperAdmin";

            // Check if the SuperAdmin role exists (it should from the previous step)
            if (!await roleManager.RoleExistsAsync(adminRole))
            {
                logger.LogError("'{AdminRole}' role does not exist. Cannot seed SuperAdmin user.", adminRole);
            }
            else
            {
                // Check if the user already exists
                var adminUser = await userManager.FindByEmailAsync(adminEmail);
                if (adminUser == null)
                {
                    // Get the System Organization ID
                    var systemOrg = await dbContext.Organizations.FirstOrDefaultAsync(o => o.Name == systemOrgName);
                    if (systemOrg == null)
                    {
                        logger.LogError("'{SystemOrgName}' organization not found. Cannot seed SuperAdmin user.", systemOrgName);
                        // Optionally throw an exception or return to prevent further execution
                        throw new InvalidOperationException($"'{systemOrgName}' organization not found during SuperAdmin seeding.");
                    }

                    adminUser = new User
                    {
                        UserName = adminEmail, // Often UserName is the same as Email
                        Email = adminEmail,
                        FirstName = "Super",
                        LastName = "Admin",
                        EmailConfirmed = true, // Assume confirmed for seeding
                        OrganizationId = systemOrg.Id // Assign the System Org ID
                    };

                    var createUserResult = await userManager.CreateAsync(adminUser, adminPassword);

                    if (createUserResult.Succeeded)
                    {
                        logger.LogInformation("SuperAdmin user '{AdminEmail}' created successfully.", adminEmail);

                        // Add user to the SuperAdmin role
                        var addToRoleResult = await userManager.AddToRoleAsync(adminUser, adminRole);
                        if (addToRoleResult.Succeeded)
                        {
                            logger.LogInformation("SuperAdmin user '{AdminEmail}' added to '{AdminRole}' role.", adminEmail, adminRole);
                        }
                        else
                        {
                            foreach (var error in addToRoleResult.Errors)
                            {
                                logger.LogError("Error adding SuperAdmin user '{AdminEmail}' to role '{AdminRole}': {ErrorDescription}", adminEmail, adminRole, error.Description);
                            }
                        }
                    }
                    else
                    {
                        foreach (var error in createUserResult.Errors)
                        {
                            logger.LogError("Error creating SuperAdmin user '{AdminEmail}': {ErrorDescription}", adminEmail, error.Description);
                        }
                    }
                }
                else
                {
                    logger.LogInformation("SuperAdmin user '{AdminEmail}' already exists.", adminEmail);
                    // Ensure role assignment even if user exists
                    if (!await userManager.IsInRoleAsync(adminUser, adminRole))
                    {
                        await userManager.AddToRoleAsync(adminUser, adminRole);
                        logger.LogInformation($"Assigned SuperAdmin role to existing user '{adminEmail}'.");
                    }
                    // Ensure OrganizationId is set if user exists but wasn't assigned before
                    if (adminUser.OrganizationId == 0)
                    {
                        var systemOrg = await dbContext.Organizations.FirstOrDefaultAsync(o => o.Name == systemOrgName);
                        if (systemOrg != null)
                        {
                            adminUser.OrganizationId = systemOrg.Id;
                            await userManager.UpdateAsync(adminUser);
                            logger.LogInformation($"Assigned System Organization ID to existing user '{adminEmail}'.");
                        }
                    }
                }
            }
            logger.LogInformation("SuperAdmin user seeding completed.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while seeding the SuperAdmin user.");
            // Depending on the severity, you might want to stop the application
            // throw;
        }
    }
}
// --- End Seed SuperAdmin User ---

// --- Seed Sagra di Tencarola Data ---
// Skip seeding in Testing environment as it's handled by the test factory
if (!app.Environment.IsEnvironment("Testing"))
{
    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;
        var logger = services.GetRequiredService<ILogger<Program>>();
        var dbContext = services.GetRequiredService<ApplicationDbContext>();
        var userManager = services.GetRequiredService<UserManager<User>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

        try
        {
            string orgName = "Sagra di Tencarola";
            string orgSlug = "sagra-tencarola"; // Example slug

            // 1. Seed Organization
            var sagraOrg = await dbContext.Organizations.FirstOrDefaultAsync(o => o.Name == orgName);
            if (sagraOrg == null)
            {
                logger.LogInformation("Creating '{OrgName}' organization.", orgName);
                sagraOrg = new Organization { Name = orgName, Slug = orgSlug };
                dbContext.Organizations.Add(sagraOrg);
                await dbContext.SaveChangesAsync(); // Save to get ID
                logger.LogInformation("'{OrgName}' organization created successfully with ID {OrgId}.", orgName, sagraOrg.Id);
            }
            else
            {
                logger.LogInformation("'{OrgName}' organization already exists with ID {OrgId}.", orgName, sagraOrg.Id);
                // Ensure slug is set if it wasn't before
                if (string.IsNullOrEmpty(sagraOrg.Slug))
                {
                    sagraOrg.Slug = orgSlug;
                    await dbContext.SaveChangesAsync();
                    logger.LogInformation("Updated slug for '{OrgName}'.", orgName);
                }
            }

            // 2. Seed Users (Cashier and Waiter)
            string cashierEmail = "cashier.tencarola@example.com";
            string waiterEmail = "waiter.tencarola@example.com";
            string defaultPassword = "Password123!";

            // Seed Cashier
            var cashierUser = await userManager.FindByEmailAsync(cashierEmail);
            if (cashierUser == null)
            {
                cashierUser = new User { UserName = cashierEmail, Email = cashierEmail, FirstName = "Cassa", LastName = "Tencarola", EmailConfirmed = true, OrganizationId = sagraOrg.Id };
                var result = await userManager.CreateAsync(cashierUser, defaultPassword);
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(cashierUser, "Cashier");
                    logger.LogInformation("User '{Email}' created and assigned 'Cashier' role for Org ID {OrgId}.", cashierEmail, sagraOrg.Id);
                }
                else { logger.LogError($"Error creating user {cashierEmail}: {string.Join(", ", result.Errors.Select(e => e.Description))}"); }
            }
            else { logger.LogInformation("User '{Email}' already exists.", cashierEmail); }

            // Seed Waiter
            var waiterUser = await userManager.FindByEmailAsync(waiterEmail);
            if (waiterUser == null)
            {
                waiterUser = new User { UserName = waiterEmail, Email = waiterEmail, FirstName = "Cameriere", LastName = "Tencarola", EmailConfirmed = true, OrganizationId = sagraOrg.Id };
                var result = await userManager.CreateAsync(waiterUser, defaultPassword);
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(waiterUser, "Waiter");
                    logger.LogInformation("User '{Email}' created and assigned 'Waiter' role for Org ID {OrgId}.", waiterEmail, sagraOrg.Id);
                }
                else { logger.LogError($"Error creating user {waiterEmail}: {string.Join(", ", result.Errors.Select(e => e.Description))}"); }
            }
            else { logger.LogInformation("User '{Email}' already exists.", waiterEmail); }

            // Seed Admin
            string adminEmail = "admin.tencarola@example.com";
            var adminUser = await userManager.FindByEmailAsync(adminEmail);
            if (adminUser == null)
            {
                adminUser = new User { UserName = adminEmail, Email = adminEmail, FirstName = "Admin", LastName = "Tencarola", EmailConfirmed = true, OrganizationId = sagraOrg.Id };
                var result = await userManager.CreateAsync(adminUser, defaultPassword);
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, "Admin"); // Assign "Admin" role
                    logger.LogInformation("User '{Email}' created and assigned 'Admin' role for Org ID {OrgId}.", adminEmail, sagraOrg.Id);
                }
                else { logger.LogError($"Error creating user {adminEmail}: {string.Join(", ", result.Errors.Select(e => e.Description))}"); }
            }
            else { logger.LogInformation("User '{Email}' already exists.", adminEmail); }


            // 3. Seed Areas
            string area1Name = "Paninoteca";
            string area1Slug = "paninoteca";
            string area2Name = "Stand Gastronomico";
            string area2Slug = "stand-gastronomico";

            var area1 = await dbContext.Areas.FirstOrDefaultAsync(a => a.OrganizationId == sagraOrg.Id && a.Name == area1Name);
            if (area1 == null)
            {
                area1 = new Area { Name = area1Name, Slug = area1Slug, OrganizationId = sagraOrg.Id };
                dbContext.Areas.Add(area1);
                logger.LogInformation("Area '{AreaName}' created for Org ID {OrgId}.", area1Name, sagraOrg.Id);
            }
            else { logger.LogInformation("Area '{AreaName}' already exists for Org ID {OrgId}.", area1Name, sagraOrg.Id); }

            var area2 = await dbContext.Areas.FirstOrDefaultAsync(a => a.OrganizationId == sagraOrg.Id && a.Name == area2Name);
            if (area2 == null)
            {
                area2 = new Area { Name = area2Name, Slug = area2Slug, OrganizationId = sagraOrg.Id };
                dbContext.Areas.Add(area2);
                logger.LogInformation("Area '{AreaName}' created for Org ID {OrgId}.", area2Name, sagraOrg.Id);
            }
            else { logger.LogInformation("Area '{AreaName}' already exists for Org ID {OrgId}.", area2Name, sagraOrg.Id); }

            await dbContext.SaveChangesAsync(); // Save Areas to get IDs if newly created

            // Ensure area IDs are loaded if they existed
            area1 ??= await dbContext.Areas.FirstAsync(a => a.OrganizationId == sagraOrg.Id && a.Name == area1Name);
            area2 ??= await dbContext.Areas.FirstAsync(a => a.OrganizationId == sagraOrg.Id && a.Name == area2Name);


            // 4. Seed Menu Categories for "Stand Gastronomico" (Area 2)
            var categories = new Dictionary<string, List<Tuple<string, decimal, string>>>
        {
            { "Primi", new List<Tuple<string, decimal, string>> {
                Tuple.Create("Bigoli al Ragù d'Anatra", 8.50m, "Pasta fresca con sugo d'anatra"),
                Tuple.Create("Gnocchi al Pomodoro", 7.00m, "Gnocchi di patate fatti in casa"),
                Tuple.Create("Risotto ai Funghi Porcini", 9.00m, "Risotto cremoso con funghi porcini freschi")
            }},
            { "Griglie", new List<Tuple<string, decimal, string>> {
                Tuple.Create("Costine di Maiale", 10.00m, "Costine marinate e grigliate"),
                Tuple.Create("Salsiccia alla Griglia", 6.50m, "Salsiccia nostrana"),
                Tuple.Create("Pollo alla Diavola", 9.50m, "Mezzo pollo piccante"),
                Tuple.Create("Tagliata di Manzo", 15.00m, "Manzo selezionato con rucola e grana")
            }},
            { "Senza Glutine", new List<Tuple<string, decimal, string>> {
                Tuple.Create("Pasta al Pesto (SG)", 8.00m, "Pasta senza glutine con pesto genovese"),
                Tuple.Create("Grigliata Mista (SG)", 14.00m, "Selezione di carni alla griglia")
            }},
            { "Bar", new List<Tuple<string, decimal, string>> {
                Tuple.Create("Acqua Naturale 0.5L", 1.00m, ""),
                Tuple.Create("Acqua Frizzante 0.5L", 1.00m, ""),
                Tuple.Create("Coca Cola", 2.50m, "Lattina 33cl"),
                Tuple.Create("Birra Bionda Media", 4.00m, "Spina 0.4L"),
                Tuple.Create("Vino Rosso (Calice)", 3.00m, "Cabernet locale"),
                Tuple.Create("Caffè", 1.20m, "Espresso")
            }}
        };

            foreach (var kvp in categories)
            {
                string categoryName = kvp.Key;
                var category = await dbContext.MenuCategories.FirstOrDefaultAsync(mc => mc.AreaId == area2.Id && mc.Name == categoryName);
                if (category == null)
                {
                    category = new MenuCategory { Name = categoryName, AreaId = area2.Id };
                    dbContext.MenuCategories.Add(category);
                    await dbContext.SaveChangesAsync(); // Save category to get ID
                    logger.LogInformation("Category '{CategoryName}' created for Area ID {AreaId}.", categoryName, area2.Id);
                }
                else
                {
                    logger.LogInformation("Category '{CategoryName}' already exists for Area ID {AreaId}.", categoryName, area2.Id);
                }
                category ??= await dbContext.MenuCategories.FirstAsync(mc => mc.AreaId == area2.Id && mc.Name == categoryName); // Ensure ID is loaded

                // 5. Seed Menu Items for the current category
                foreach (var itemTuple in kvp.Value)
                {
                    string itemName = itemTuple.Item1;
                    decimal itemPrice = itemTuple.Item2;
                    string itemDescription = itemTuple.Item3;

                    var menuItem = await dbContext.MenuItems.FirstOrDefaultAsync(mi => mi.MenuCategoryId == category.Id && mi.Name == itemName);
                    if (menuItem == null)
                    {
                        menuItem = new MenuItem
                        {
                            Name = itemName,
                            Description = itemDescription,
                            Price = itemPrice,
                            MenuCategoryId = category.Id
                        };
                        dbContext.MenuItems.Add(menuItem);
                        logger.LogInformation("MenuItem '{ItemName}' created for Category ID {CategoryId}.", itemName, category.Id);
                    }
                    else
                    {
                        logger.LogInformation("MenuItem '{ItemName}' already exists for Category ID {CategoryId}.", itemName, category.Id);
                    }
                }
            }

            await dbContext.SaveChangesAsync(); // Save all menu items

            logger.LogInformation("Sagra di Tencarola data seeding completed successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred seeding the Sagra di Tencarola data.");
        }
    }
}
// --- End Seed Sagra di Tencarola Data ---


app.Run();

// Add this partial declaration to make the implicit Program class public
// so it can be used by WebApplicationFactory in the integration test project.
public partial class Program { }
