using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SagraFacile.WindowsPrinterService.Services;
using SagraFacile.WindowsPrinterService.Printing; // For IRawPrinter and RawPrinterHelperService
using System.Text; // Required for Encoding

namespace SagraFacile.WindowsPrinterService;

static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main(string[] args) // Add args parameter
    {
        // Register encoding providers for extended character sets like IBM850
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        // To customize application configuration such as set high DPI settings or default font,
        // see https://aka.ms/applicationconfiguration.
        // ApplicationConfiguration.Initialize(); // Moved initialization to where Application runs

        var host = CreateHostBuilder(args).Build();

        // We don't run the main form directly anymore.
        // The ApplicationLifetimeService will manage the WinForms message loop.
        host.Run();
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            // Configure host options if needed, e.g., shutdown timeout
            .ConfigureServices((hostContext, services) =>
            {
                // Register Logging
                services.AddLogging(configure => configure.AddConsole().AddDebug()); // Add basic logging providers

                // Register new Raw Printer Service
                services.AddTransient<IRawPrinter, RawPrinterHelperService>();

                // Register SignalR Service
                services.AddSingleton<SignalRService>();

                // Register Forms
                // Remove the default Form1 registration if it's no longer needed
                // services.AddTransient<Form1>();
                services.AddTransient<SettingsForm>(); // Register SettingsForm

                // Register the main application lifetime manager as a Hosted Service
                services.AddHostedService<ApplicationLifetimeService>();
            })
            // Optional: Configure logging further if needed
            .ConfigureLogging(logging =>
            {
                logging.SetMinimumLevel(LogLevel.Information); // Set default log level
                // Add other providers like EventLog if necessary
            });
}
