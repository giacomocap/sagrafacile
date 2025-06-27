using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SagraFacile.WindowsPrinterService.Services;
using SagraFacile.WindowsPrinterService.Printing;
using SagraFacile.WindowsPrinterService.Models; // Required for ProfileSettings
using System.Text;
using System.Windows.Forms; // Required for Application.Run and DialogResult
using System; // Required for STAThread
using System.IO; // Added for Path and File operations
using System.Text.Json; // Added for JSON deserialization

namespace SagraFacile.WindowsPrinterService;

static class Program
{
    public static ProfileSettings? SelectedProfile { get; private set; }

    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize(); // Standard WinForms initialization
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        // Attempt to load profile from command-line arguments
        string? profileGuidFromArgs = ParseProfileGuidFromArgs(args);
        if (!string.IsNullOrEmpty(profileGuidFromArgs))
        {
            string profilesDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SagraFacilePrinterService", "profiles");
            // Ensure the directory exists before trying to read from it, though it should if profiles were ever saved.
            Directory.CreateDirectory(profilesDir); // Ensures profilesDir exists, harmless if it already does.
            string profilePath = Path.Combine(profilesDir, $"{profileGuidFromArgs}.json");

            if (File.Exists(profilePath))
            {
                try
                {
                    string json = File.ReadAllText(profilePath);
                    Program.SelectedProfile = JsonSerializer.Deserialize<ProfileSettings>(json);
                    if (Program.SelectedProfile != null)
                    {
                        Console.WriteLine($"Profile '{Program.SelectedProfile.ProfileName}' (GUID: {profileGuidFromArgs}) loaded from command line argument.");
                    }
                    else
                    {
                        Console.WriteLine($"Failed to deserialize profile for GUID '{profileGuidFromArgs}' from command line. Profile data might be corrupt.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading profile for GUID '{profileGuidFromArgs}' from command line: {ex.Message}");
                    Program.SelectedProfile = null; // Ensure it's null if loading failed
                }
            }
            else
            {
                Console.WriteLine($"Profile file not found for GUID '{profileGuidFromArgs}' from command line. Path: {profilePath}");
            }
        }

        // If no profile loaded from command line, show selection form
        if (Program.SelectedProfile == null)
        {
            Application.EnableVisualStyles(); // Ensure visual styles are enabled for all forms
            Application.SetCompatibleTextRenderingDefault(false);
            using (var profileForm = new ProfileSelectionForm())
            {
                if (profileForm.ShowDialog() == DialogResult.OK)
                {
                    Program.SelectedProfile = profileForm.SelectedProfileSettings;
                }
            }
        }

        // Check if a profile is selected/loaded
        if (Program.SelectedProfile == null)
        {
            MessageBox.Show("Nessun profilo selezionato o caricato. L'applicazione verrà chiusa.", "Chiusura Applicazione", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return; 
        }
        
        // Program.SelectedProfile is now the single source of truth for the selected profile.
        // The local 'selectedProfile' variable is no longer needed throughout this method.

        var host = CreateHostBuilder(args, Program.SelectedProfile).Build();
        
        // Set the active profile in SignalRService before the host runs.
        // ApplicationLifetimeService will also receive Program.SelectedProfile via DI from CreateHostBuilder.
        var signalRService = host.Services.GetRequiredService<SignalRService>();
        signalRService.SetActiveProfile(Program.SelectedProfile);

        host.Run(); // This will start ApplicationLifetimeService, which in turn starts SignalRService
    }

    private static string? ParseProfileGuidFromArgs(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].Equals("--profile-guid", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                // Basic validation for GUID format can be added here if desired, but not strictly necessary for parsing.
                return args[i + 1];
            }
        }
        return null;
    }

    public static IHostBuilder CreateHostBuilder(string[] args, ProfileSettings profileSettings) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureServices((hostContext, services) =>
            {
                services.AddLogging(configure => configure.AddConsole().AddDebug());
                services.AddTransient<IRawPrinter, RawPrinterHelperService>();
                
                // Register new services for better separation of concerns
                services.AddSingleton<IPdfPrintingService, PdfPrintingService>();
                services.AddSingleton<IPrinterConfigurationService, PrinterConfigurationService>();
                services.AddSingleton<IPrintJobManager, PrintJobManager>();
                
                // SignalRService is singleton, its active profile is set before host.Run()
                services.AddSingleton<SignalRService>();
                
                // Forms
                // SettingsForm constructor now requires profileName and profilesDirectory.
                // When resolved by DI (e.g. from ApplicationLifetimeService), it needs these.
                // This might require ApplicationLifetimeService to pass them or a factory.
                // For now, we assume SettingsForm opened via ProfileSelectionForm or ApplicationLifetimeService will handle this.
                services.AddTransient<SettingsForm>(sp => {
                    // This factory is a placeholder. SettingsForm is typically opened with context.
                    // If DI needs to create it without context, this would be an issue.
                    // However, our current flow is:
                    // 1. ProfileSelectionForm -> new SettingsForm(profileName, dir) -> OK
                    // 2. ApplicationLifetimeService -> new SettingsForm(Program.SelectedProfile.ProfileName, dir) -> OK
                    // So direct DI resolution of SettingsForm without parameters might not be strictly needed.
                    // Let's provide a basic one that would imply creating a new profile if used directly.
                    string appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SagraFacilePrinterService");
                    string profilesDir = Path.Combine(appDataFolder, "profiles");
                    return new SettingsForm(null, profilesDir); 
                });
                services.AddTransient<PrintStationForm>();


                // Pass ProfileSettings to ApplicationLifetimeService
                services.AddSingleton(profileSettings); // Make selected profile settings available via DI
                services.AddHostedService<ApplicationLifetimeService>();
            })
            .ConfigureLogging(logging =>
            {
                logging.SetMinimumLevel(LogLevel.Information);
            });
}
