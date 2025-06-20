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
using SagraFacile.NET.API.Data; // Added for InitialDataSeeder
using Serilog; // Added for Serilog
using Serilog.Events; // Added for Serilog LogEventLevel

// Ensure extended encodings are available
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

// --- Serilog Bootstrap Logger ---
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithThreadId()
    .WriteTo.Console(outputTemplate: // Structured console output for Docker
        "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}{Properties:j}")
    .CreateBootstrapLogger();
// --- End Serilog Bootstrap Logger ---

try // Added for Serilog try-finally block
{
    Log.Information("Starting SagraFacile.NET.API host"); // Added for Serilog

    var builder = WebApplication.CreateBuilder(args);

    // --- Configure Serilog for WebApplicationBuilder ---
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithMachineName()
        .Enrich.WithThreadId());
    // --- End Configure Serilog for WebApplicationBuilder ---

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
    builder.Services.AddScoped<IAnalyticsService, AnalyticsService>(); // Register Analytics Service
    builder.Services.AddScoped<IInitialDataSeeder, InitialDataSeeder>(); // Register InitialDataSeeder

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
    builder.Services.AddHttpClient("PreOrderPlatformClient");

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

    // --- Seed Database (System Defaults, Demo Data OR Initial Org/Admin) ---
    await app.SeedDatabaseAsync();
    // --- End Seed Database ---


    // --- Serilog Request Logging ---
    app.UseSerilogRequestLogging(); // Add Serilog's request logging middleware
    // --- End Serilog Request Logging ---

    app.Run();
} // Added for Serilog try-finally block
catch (Exception ex) // Added for Serilog try-finally block
{
    Log.Fatal(ex, "SagraFacile.NET.API host terminated unexpectedly");
}
finally // Added for Serilog try-finally block
{
    Log.CloseAndFlush();
}


// Add this partial declaration to make the implicit Program class public
// so it can be used by WebApplicationFactory in the integration test project.
public partial class Program { }
