using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SagraFacile.WindowsPrinterService.Services;
using SagraFacile.WindowsPrinterService.Printing;
using SagraFacile.WindowsPrinterService.Models; // Required for ProfileSettings
using System.Text;
using System.Windows.Forms; // Required for Application.Run and DialogResult
using System; // Required for STAThread

namespace SagraFacile.WindowsPrinterService;

static class Program
{
    public static ProfileSettings? SelectedProfile { get; private set; }

    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize(); // Standard WinForms initialization
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        // Show ProfileSelectionForm first
        Application.EnableVisualStyles(); // Ensure visual styles are enabled for all forms
        Application.SetCompatibleTextRenderingDefault(false);

        ProfileSettings? selectedProfile = null;
        using (var profileForm = new ProfileSelectionForm())
        {
            if (profileForm.ShowDialog() == DialogResult.OK)
            {
                selectedProfile = profileForm.SelectedProfileSettings;
            }
        }

        if (selectedProfile == null)
        {
            MessageBox.Show("Nessun profilo selezionato. L'applicazione verrà chiusa.", "Chiusura Applicazione", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return; // Exit if no profile selected
        }
        
        SelectedProfile = selectedProfile; // Store for services to access

        var host = CreateHostBuilder(args, selectedProfile).Build();
        
        // Set the active profile in SignalRService before the host runs and ApplicationLifetimeService starts it.
        var signalRService = host.Services.GetRequiredService<SignalRService>();
        signalRService.SetActiveProfile(selectedProfile);

        // Pass selected profile to ApplicationLifetimeService if needed, or it can get it from Program.SelectedProfile
        // For now, ApplicationLifetimeService will be modified to use Program.SelectedProfile

        host.Run(); // This will start ApplicationLifetimeService, which in turn starts SignalRService
    }

    public static IHostBuilder CreateHostBuilder(string[] args, ProfileSettings profileSettings) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureServices((hostContext, services) =>
            {
                services.AddLogging(configure => configure.AddConsole().AddDebug());
                services.AddTransient<IRawPrinter, RawPrinterHelperService>();
                
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
                services.AddTransient<InputDialogForm>((sp) => new InputDialogForm("Default Title", "Default Prompt"));


                // Pass ProfileSettings to ApplicationLifetimeService
                services.AddSingleton(profileSettings); // Make selected profile settings available via DI
                services.AddHostedService<ApplicationLifetimeService>();
            })
            .ConfigureLogging(logging =>
            {
                logging.SetMinimumLevel(LogLevel.Information);
            });
}
