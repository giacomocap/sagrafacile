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
using SagraFacile.NET.API.Models.Enums;
using SagraFacile.NET.API.Services.SaaS; // Add this using for SaaSSubscriptionService
using Microsoft.AspNetCore.DataProtection; // Added for Data Protection
using PuppeteerSharp; // Added for Puppeteer
using Serilog; // Added for Serilog

// Ensure extended encodings are available
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

// Check if running in design-time mode (EF tools)
var isDesignTime = args.Contains("--design-time") ||
                   Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "DesignTime" ||
                   args.Any(arg => arg.Contains("ef")) ||
                   AppDomain.CurrentDomain.GetAssemblies().Any(a => a.FullName?.Contains("EntityFrameworkCore.Design") == true);


// --- Serilog Bootstrap Logger ---
if (!isDesignTime)
{
    Log.Logger = new LoggerConfiguration()
        .MinimumLevel.Debug() // More verbose for bootstrap
        .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning) // More verbose for Microsoft components during bootstrap
        .MinimumLevel.Override("Microsoft.Hosting.Lifetime", Serilog.Events.LogEventLevel.Information)
        .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", Serilog.Events.LogEventLevel.Information)
        .Enrich.FromLogContext()
        .Enrich.WithMachineName()
        .Enrich.WithThreadId()
        .WriteTo.Console(outputTemplate: // Structured console output for Docker
            "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}{Properties:j}")
        .CreateBootstrapLogger();
}
else
{
    // Minimal logging for design-time
    Log.Logger = new LoggerConfiguration()
        .MinimumLevel.Warning()
        .WriteTo.Console()
        .CreateBootstrapLogger();
}
// --- End Serilog Bootstrap Logger ---

try // Added for Serilog try-finally block
{
    Console.WriteLine("Starting SagraFacile.NET.API host"); // Replaced Log.Information

    var builder = WebApplication.CreateBuilder(args);

    // --- Configure Serilog for WebApplicationBuilder ---
    if (!isDesignTime)
    {
        builder.Host.UseSerilog((context, services, configuration) => configuration
            .MinimumLevel.Debug() // More verbose for general logging
            .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning) // More verbose for Microsoft components
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", Serilog.Events.LogEventLevel.Information) // Explicitly set EF command logs to Information
            .ReadFrom.Configuration(context.Configuration) // appsettings.json can override these if needed
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithThreadId());
    }
    // --- End Configure Serilog for WebApplicationBuilder ---

    // Define CORS policy name
    var MyAllowSpecificOrigins = "_myAllowSpecificOrigins";

    // Add services to the container.

    // 1. Add DbContext
    // Conditionally register Npgsql provider based on environment
    if (!builder.Environment.IsEnvironment("Testing")) // Use a custom "Testing" environment name
    {
        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrEmpty(connectionString))
        {
            Console.WriteLine("ERROR: Connection string 'DefaultConnection' not found or is empty. Please check configuration (environment variables or appsettings.json)."); // Replaced Log.Error
            throw new InvalidOperationException("Connection string 'DefaultConnection' not found or is empty.");
        }
        // Console.WriteLine($"Found connection string"); // This log is redundant
        builder.Services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString)); // Use PostgreSQL provider
    }
    // The DbContext will be added by the CustomWebApplicationFactory when Environment is "Testing"

    // 2. Add Identity
    builder.Services.AddIdentity<User, IdentityRole>(options => options.SignIn.RequireConfirmedAccount = true) // Configure Identity options as needed
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders(); // Add default token providers

    // 2a. Configure Data Protection
    // This ensures that tokens (like for email confirmation) are valid across container restarts.
    // It stores the keys in a shared volume defined in docker-compose.
    var dataProtectionKeysPath = Path.Combine(builder.Environment.ContentRootPath, "keys");
    if (!Directory.Exists(dataProtectionKeysPath))
    {
        Directory.CreateDirectory(dataProtectionKeysPath);
    }
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath))
        .SetApplicationName("SagraFacile");


    // 2b. Add Authentication and JWT Bearer Configuration (skip during design-time)
    if (!isDesignTime)
    {
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
                ValidIssuer = builder.Configuration["JWT_ISSUER"] ?? builder.Configuration["Jwt:Issuer"] ?? throw new InvalidOperationException("JWT_ISSUER not configured. Check environment variables."),
                ValidAudience = builder.Configuration["JWT_AUDIENCE"] ?? builder.Configuration["Jwt:Audience"] ?? throw new InvalidOperationException("JWT_AUDIENCE not configured. Check environment variables."),
                // Use the JWT_SECRET environment variable directly, as this is what's being set.
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["JWT_SECRET"] ?? builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT_SECRET not configured. Check environment variables."))),
                // Explicitly tell the middleware which claim contains role information
                RoleClaimType = ClaimTypes.Role
            };
        });
    }


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
    builder.Services.AddScoped<IPrintJobService, PrintJobService>(); // Add Print Job Service
    builder.Services.AddScoped<IPrinterAssignmentService, PrinterAssignmentService>(); // Add Printer Assignment Service
    builder.Services.AddScoped<ICashierStationService, CashierStationService>();
    builder.Services.AddScoped<IQueueService, QueueService>(); // Add QueueService registration
    builder.Services.AddScoped<IAdMediaItemService, AdMediaItemService>();
    builder.Services.AddScoped<IAdAreaAssignmentService, AdAreaAssignmentService>();
    builder.Services.AddScoped<IAnalyticsService, AnalyticsService>(); // Register Analytics Service
    builder.Services.AddScoped<IPdfService, PdfService>(); // Register PDF Service
    builder.Services.AddScoped<IPrintTemplateService, PrintTemplateService>(); // Register Print Template Service
    builder.Services.AddScoped<IInitialDataSeeder, InitialDataSeeder>(); // Register InitialDataSeeder

    // Conditional Dependency Injection for ISubscriptionService based on APP_MODE
    var appMode = builder.Configuration["APP_MODE"] ?? builder.Configuration["AppSettings:AppMode"];
    var isSaaSMode = appMode?.ToLower() == "saas";
    // Log the detected app mode for debugging
    Console.WriteLine($"[CONFIG] Reading APP_MODE. Value: '{appMode}'");

    if (isSaaSMode)
    {
        builder.Services.AddScoped<ISubscriptionService, SaaSSubscriptionService>();
        Console.WriteLine("[CONFIG] SaaS mode enabled. Registered SaaSSubscriptionService.");
    }
    else
    {
        builder.Services.AddScoped<ISubscriptionService, OpenSourceSubscriptionService>();
        Console.WriteLine("[CONFIG] Open Source mode enabled (or APP_MODE not set). Registered OpenSourceSubscriptionService.");
    }

    // Register the Background Service conditionally
    var enablePollingService = builder.Configuration.GetValue<bool?>("ENABLE_PREORDER_POLLING_SERVICE");

    // Skip background services during design-time
    if (!isDesignTime)
    {
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

        // Register the new PrintJobProcessor background service
        builder.Services.AddHostedService<PrintJobProcessor>();

        if (isSaaSMode)
        {
            // Register the new DataRetentionService background service
            builder.Services.AddHostedService<DataRetentionService>();
            Console.WriteLine("[CONFIG] SaaS mode enabled. Registered DataRetentionService background service.");
        }
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
                                        "http://localhost:3000",
                                        "https://localhost:3000",
                                        "https://192.168.1.236:3000"
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

    Console.WriteLine("All services configured. Attempting to build the application..."); // Replaced Log.Information

    var app = builder.Build();
    Console.WriteLine("Application built successfully. Configuring HTTP request pipeline..."); // Replaced Log.Information

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

    // Add Authentication middleware BEFORE Authorization (skip during design-time)
    if (!isDesignTime)
    {
        app.UseAuthentication();
        app.UseAuthorization();
    }

    app.MapControllers();

    // Map SignalR Hub
    app.MapHub<OrderHub>("/api/orderHub"); // Added /api prefix for consistency

    // --- Apply Migrations ---
    // Ensure the database is created and migrations are applied on startup.
    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;
        // var logger = services.GetRequiredService<ILogger<Program>>(); // Serilog logger
        var appLogger = services.GetRequiredService<ILoggerFactory>().CreateLogger<Program>(); // Standard logger
                                                                                               // Avoid applying migrations during integration tests if using a separate test DB strategy
        if (!app.Environment.IsEnvironment("Testing"))
        {
            try
            {
                var context = services.GetRequiredService<ApplicationDbContext>();
                await context.Database.MigrateAsync(); // Apply pending migrations
                appLogger.LogInformation("Database migrations applied successfully."); // Replaced logger.LogInformation
            }
            catch (Exception ex)
            {
                appLogger.LogError(ex, "An error occurred while applying database migrations. Re-throwing exception."); // Replaced logger.LogError
                                                                                                                        // Consider stopping the application if migrations fail critically
                throw; // Re-throw the exception to be caught by the main try-catch
            }
        }
    }
    // --- End Apply Migrations ---

    // --- Seed Database (System Defaults, Demo Data OR Initial Org/Admin) ---
    if (!isDesignTime)
    {
        await app.SeedDatabaseAsync();
    }
    // --- End Seed Database ---

    // --- Download Chromium for Puppeteer ---
    // Only run Puppeteer download if not in Testing and not in design-time mode
    if (!app.Environment.IsEnvironment("Testing") && !isDesignTime)
    {
        try
        {
            var logger = app.Services.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("Checking for Puppeteer browser revision...");
            var browserFetcher = new BrowserFetcher();
            var revisionInfo = await browserFetcher.DownloadAsync();
            logger.LogInformation("Puppeteer browser revision {Revision} is available at {Path}", revisionInfo.Platform, revisionInfo.GetExecutablePath());
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Could not download or verify Puppeteer browser. PDF generation may fail.");
        }
    }
    // --- End Download Chromium ---

    // --- Serilog Request Logging ---
    if (!isDesignTime)
    {
        app.UseSerilogRequestLogging(); // Add Serilog's request logging middleware
    }
    // --- End Serilog Request Logging ---

    app.Run();
} // Added for Serilog try-finally block
catch (Exception ex) when (ex is not HostAbortedException && ex.Source != "Microsoft.EntityFrameworkCore.Design")
{
    Log.Fatal(ex, "Web host terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}


// Add this partial declaration to make the implicit Program class public
// so it can be used by WebApplicationFactory in the integration test project.
public partial class Program { }
