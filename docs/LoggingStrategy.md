# SagraFacile .NET API Logging Strategy

This document outlines the strategy for implementing comprehensive and effective logging within the SagraFacile .NET API using Serilog.

## 1. Goals of Logging

*   **Debugging & Troubleshooting:** Provide detailed information to diagnose and resolve issues quickly.
*   **Performance Monitoring:** Capture request durations and identify bottlenecks in critical operations.
*   **Error Tracking:** Log all exceptions with sufficient context to understand their root cause.
*   **Operational Insight:** Record key business events to understand application flow and usage patterns.
*   **Security Auditing (Future):** Lay groundwork for logging security-relevant events if needed.

## 2. Chosen Framework: Serilog

Serilog is chosen for its flexibility, structured logging capabilities, wide range of sinks, and strong integration with ASP.NET Core.

## 3. Implementation Phases

### Phase 1: Basic Serilog Setup & Configuration

**A. Install Serilog NuGet Packages:**
*   `Serilog.AspNetCore`
*   `Serilog.Sinks.Console`
*   `Serilog.Enrichers.Environment`
*   `Serilog.Enrichers.Thread`
*   `Serilog.Settings.Configuration`

**B. Configure Serilog in `Program.cs`:**
Set up Serilog as the primary logging provider early in the application startup.

```csharp
// Program.cs (Illustrative example, adapt to your actual Program.cs structure)
using Serilog;
using Serilog.Events;

public class Program
{
    public static void Main(string[] args)
    {
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

        try
        {
            Log.Information("Starting SagraFacile.NET.API host");
            var builder = WebApplication.CreateBuilder(args); // Or Host.CreateDefaultBuilder for older .NET

            builder.Host.UseSerilog((context, services, configuration) => configuration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .Enrich.WithMachineName()
                .Enrich.WithThreadId());

            // ... Add your services, configure pipeline ...
            // Example: builder.Services.AddControllers();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            app.UseSerilogRequestLogging(); // Add Serilog's request logging middleware

            // ... Other middleware ...
            // Example: app.UseRouting(); app.UseAuthentication(); app.UseAuthorization();
            // app.MapControllers();

            app.Run();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "SagraFacile.NET.API host terminated unexpectedly");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}
```

**C. Configure Serilog in `appsettings.json`:**
Control log levels and basic sink configuration.

```json
// appsettings.json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information", // Default for your application code
      "Override": {
        "Microsoft": "Warning",
        "Microsoft.AspNetCore": "Warning",
        "Microsoft.Hosting.Lifetime": "Information",
        "Microsoft.EntityFrameworkCore.Database.Command": "Information", // Set to "Warning" in strict production
        "System": "Warning"
      }
    },
    "Enrich": ["FromLogContext", "WithMachineName", "WithThreadId"],
    // Console sink is configured in Program.cs for structured output,
    // but can also be defined here if preferred.
    // "WriteTo": [
    //   { "Name": "Console" }
    // ],
    "Properties": {
      "ApplicationName": "SagraFacile.NET.API"
    }
  },
  // ... other configurations
}
```

### Phase 2: Implementing Logging in Application Code

**A. Request/Response Logging:**
Utilize `app.UseSerilogRequestLogging();` in `Program.cs` (or `Startup.Configure`). This middleware automatically logs:
*   Request method and path
*   Response status code
*   Request processing duration

**B. Contextual Logging in Services and Controllers:**
*   Inject `ILogger<T>` into constructors. Serilog uses `T` as the `SourceContext`.
*   **Log Levels:**
    *   `LogTrace` / `LogVerbose`: Highly detailed, for deep debugging.
    *   `LogDebug`: Developer-focused, for diagnosing specific flows.
    *   `LogInformation`: Normal application behavior, successful operations, key events.
    *   `LogWarning`: Unexpected but recoverable situations, potential issues.
    *   `LogError`: Errors that caused an operation to fail. Include the `Exception` object.
    *   `LogCritical` / `LogFatal`: Severe errors causing application shutdown.
*   **Structured Logging with Properties:** Use message templates with named placeholders. For complex objects, use the `@` destructuring operator.

    ```csharp
    // Example in OrderService.cs
    _logger.LogInformation("Creating order for User {UserId} in Area {AreaId} with {ItemCount} items.", userId, areaId, itemCount);
    _logger.LogInformation("Order {OrderId} created successfully. Details: {@OrderDto}", newOrder.Id, orderDto);

    try
    {
        // ... operation ...
    }
    catch (SpecificException ex)
    {
        _logger.LogError(ex, "Failed to process payment for Order {OrderId} due to SpecificException. PaymentProviderResponse: {Response}", orderId, providerResponse);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "An unexpected error occurred while processing Order {OrderId}", orderId);
    }
    ```

**C. Logging Key Business Events:**
Identify and log critical events in your application's workflow:
*   User authentication (success/failure - be careful with sensitive data).
*   Order creation, modification, status changes.
*   Payment processing steps.
*   KDS interactions (item confirmation, order completion).
*   Stock level changes (especially critical thresholds).
*   Printer job dispatch and status (if possible).
*   Administrative actions (e.g., Area configuration changes).

**D. Error and Exception Logging:**
*   Ensure all caught exceptions are logged with `LogError`, including the exception object itself.
*   Unhandled exceptions will be caught by Serilog if `UseSerilogRequestLogging` is used or by ASP.NET Core's default exception handling, which Serilog can also capture.

### Phase 3: Review and Refine

*   **Test Thoroughly:** Execute all major application workflows and verify logs are generated as expected.
*   **Check Log Clarity & Context:** Ensure messages are understandable and provide enough information.
*   **Adjust Log Levels:** Fine-tune log levels in `appsettings.json` to balance detail with noise, especially for production.
*   **Monitor Log Volume:** Be mindful of disk space if logging to files in the future.

## 4. Best Practices

*   **Be Consistent:** Use consistent log levels and message formats.
*   **Don't Log Sensitive Data:** Avoid logging passwords, full credit card numbers, API keys, etc., unless properly masked or in highly secure, development-only scenarios.
*   **Use Structured Properties:** Prefer logging data as distinct properties rather than embedding it all in the message string. This makes querying and filtering much more powerful.
*   **SourceContext is Your Friend:** `ILogger<T>` automatically provides the `SourceContext`, which is invaluable for filtering logs from specific classes.
*   **Log Asynchronously (Default for most Sinks):** Serilog sinks are typically asynchronous, minimizing performance impact on your application threads.

## 5. Future Considerations

*   **File Sink:** For persistent logs on the server. Configure rolling files to manage disk space.
    *   `Serilog.Sinks.File`
*   **Seq Sink:** For excellent local log viewing and querying during development and staging.
    *   `Serilog.Sinks.Seq`
*   **Centralized Log Management:** For production, consider Elasticsearch + Kibana (ELK), Grafana Loki, or cloud-based services (Azure Monitor, AWS CloudWatch Logs, Datadog, etc.).
*   **Correlation IDs:** Implement correlation IDs to trace a single request across multiple services or asynchronous operations.
*   **Performance Metrics:** Integrate with tools like Prometheus and Grafana for application performance monitoring (APM).

This strategy provides a robust starting point for logging in SagraFacile, enabling better insights and maintainability.
