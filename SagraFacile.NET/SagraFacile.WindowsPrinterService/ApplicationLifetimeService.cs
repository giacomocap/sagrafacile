using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SagraFacile.WindowsPrinterService.Services;
using SagraFacile.WindowsPrinterService.Models; // Required for ProfileSettings
using System; 
using System.IO; 
using System.Windows.Forms; // Required for ApplicationContext, NotifyIcon etc.
using System.Drawing; // Required for Icon
using System.Threading; // Required for ManualResetEvent, SynchronizationContext

namespace SagraFacile.WindowsPrinterService
{
    public class ApplicationLifetimeService : IHostedService
    {
        private readonly ILogger<ApplicationLifetimeService> _logger;
        private readonly SignalRService _signalRService;
        private readonly IHostApplicationLifetime _appLifetime;
        private readonly IServiceProvider _serviceProvider;
        private readonly ProfileSettings _profileSettings; // Injected selected profile
        private ApplicationContext? _applicationContext;
        private NotifyIcon? _notifyIcon;
        private PrintStationForm? _printStationForm;
        private SynchronizationContext? _uiContext;
        private readonly ManualResetEvent _uiThreadReady = new ManualResetEvent(false);

        private Icon? _iconDefault;
        private Icon? _iconConnecting;
        private Icon? _iconConnected;
        private Icon? _iconError;
        private Icon? _iconDisconnected;

        public ApplicationLifetimeService(
            ILogger<ApplicationLifetimeService> logger,
            SignalRService signalRService,
            IHostApplicationLifetime appLifetime,
            IServiceProvider serviceProvider,
            ProfileSettings profileSettings) // Inject ProfileSettings
        {
            _logger = logger;
            _signalRService = signalRService;
            _appLifetime = appLifetime;
            _serviceProvider = serviceProvider;
            _profileSettings = profileSettings; // Store injected profile

            _signalRService.ConnectionStatusChanged += UpdateTrayIconTooltip;
            _logger.LogDebug($"Subscribed UpdateTrayIconTooltip to SignalRService.ConnectionStatusChanged in constructor for profile: {_profileSettings.ProfileName}.");
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation($"ApplicationLifetimeService starting for profile: {_profileSettings.ProfileName}...");
            _appLifetime.ApplicationStopping.Register(OnStopping);
            _appLifetime.ApplicationStopped.Register(OnStopped);

            var thread = new Thread(() =>
            {
                try
                {
                    WindowsFormsSynchronizationContext.AutoInstall = false; 
                    SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
                    _logger.LogInformation($"Explicitly set WindowsFormsSynchronizationContext for UI thread (Profile: {_profileSettings.ProfileName}).");

                    InitializeMainUI(); 

                    if (_printStationForm == null || _applicationContext == null)
                    {
                        _logger.LogError($"Main UI (PrintStationForm or ApplicationContext) failed to initialize for profile '{_profileSettings.ProfileName}'. Cannot start WinForms message loop.");
                        _appLifetime.StopApplication(); 
                        return; 
                    }
                    
                    _logger.LogInformation($"WinForms UI initialized for profile '{_profileSettings.ProfileName}'. Starting message loop.");
                    _uiThreadReady.Set();
                    _logger.LogInformation($"UI thread ready for profile '{_profileSettings.ProfileName}', signaled main thread to start SignalR service.");
                    
                    Application.Run(_applicationContext); 
                    _logger.LogInformation($"WinForms message loop stopped for profile '{_profileSettings.ProfileName}'.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Unhandled exception in WinForms thread (Profile: {_profileSettings.ProfileName}).");
                    _appLifetime.StopApplication();
                }
            });
            thread.Name = $"WinFormsThread-{_profileSettings.ProfileName}"; 
            thread.SetApartmentState(ApartmentState.STA); 
            thread.Start();

            _ = Task.Run(async () => 
            {
                _logger.LogInformation($"Waiting for UI thread to be ready before starting SignalR service (Profile: {_profileSettings.ProfileName})...");
                _uiThreadReady.WaitOne(); 
                _logger.LogInformation($"UI thread is ready, calling OnStarted to start SignalR service (Profile: {_profileSettings.ProfileName}).");
                // SignalRService already has the profile set via Program.cs before host.Run()
                await _signalRService.StartAsync(cancellationToken); 
            }, cancellationToken); 

            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation($"ApplicationLifetimeService stopping... (Profile: {_profileSettings.ProfileName})");
            await _signalRService.StopAsync(); 

            if (_printStationForm != null && !_printStationForm.IsDisposed)
            {
                _printStationForm.Close(); 
                _printStationForm.Dispose();
                _printStationForm = null;
            }
            if (_notifyIcon != null)
            {
                if (_signalRService != null) 
                {
                    _signalRService.ConnectionStatusChanged -= UpdateTrayIconTooltip; 
                }
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _notifyIcon = null;
                _logger.LogInformation($"Tray icon disposed for profile {_profileSettings.ProfileName}.");
            }
            if (Application.MessageLoop)
            {
                Application.ExitThread();
            }
        }

        private void InitializeMainUI()
        {
            _printStationForm = ResolvePrintStationForm();
            if (_printStationForm == null)
            {
                _logger.LogError($"Failed to resolve PrintStationForm for profile '{_profileSettings.ProfileName}'. Application cannot start main UI.");
                return;
            }
            _printStationForm.Text = $"Stazione Stampa Comande - Profilo: {_profileSettings.ProfileName}"; 
            _printStationForm.FormClosing += PrintStationForm_FormClosing;
            
            _applicationContext = new ApplicationContext(_printStationForm); 
            
            _uiContext = SynchronizationContext.Current;
            if (_uiContext == null)
            {
                _logger.LogWarning($"Could not capture UI SynchronizationContext for profile '{_profileSettings.ProfileName}'. Tray icon updates might not work correctly.");
            }

            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add($"Mostra/Nascondi Stazione ({_profileSettings.ProfileName})", null, OnShowHidePrintStationClicked);
            contextMenu.Items.Add($"Impostazioni ({_profileSettings.ProfileName})...", null, OnSettingsClicked);
            contextMenu.Items.Add("-"); 
            contextMenu.Items.Add($"Esci ({_profileSettings.ProfileName})", null, OnExitClicked);

            _iconDefault = SystemIcons.Application; 
            _iconConnecting = SystemIcons.Information; 
            _iconConnected = SystemIcons.Shield;       
            _iconError = SystemIcons.Error;           
            _iconDisconnected = SystemIcons.Warning;    

            _notifyIcon = new NotifyIcon
            {
                ContextMenuStrip = contextMenu,
                Text = $"SagraFacile Printer - {_profileSettings.ProfileName}", 
                Visible = true,
                Icon = _iconDefault 
            };
            
            UpdateTrayIconTooltip(null, _signalRService.GetCurrentStatus().LastStatusMessage); // Use current status from SignalRService

            _logger.LogInformation($"Main UI and Tray icon initialized for profile '{_profileSettings.ProfileName}'.");
        }

        private PrintStationForm? ResolvePrintStationForm()
        {
            try
            {
                var form = _serviceProvider.GetRequiredService<PrintStationForm>();
                return form;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to resolve or create PrintStationForm for profile '{_profileSettings.ProfileName}'.");
                return null;
            }
        }

        private void PrintStationForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                _logger.LogInformation($"PrintStationForm closing by user (Profile: {_profileSettings.ProfileName}). Minimizing to tray.");
                e.Cancel = true; 
                _printStationForm?.Hide();
                if (_notifyIcon != null)
                {
                    _notifyIcon.ShowBalloonTip(1000, $"SagraFacile Printer - {_profileSettings.ProfileName}", "L'applicazione è stata minimizzata nell'area di notifica.", ToolTipIcon.Info);
                }
            }
            else
            {
                _logger.LogInformation($"PrintStationForm closing due to {e.CloseReason} (Profile: {_profileSettings.ProfileName}). Allowing close.");
            }
        }
        
        private void OnShowHidePrintStationClicked(object? sender, EventArgs e)
        {
            if (_printStationForm == null || _printStationForm.IsDisposed)
            {
                _logger.LogInformation($"PrintStationForm is null or disposed (Profile: {_profileSettings.ProfileName}). Recreating and showing.");
                _printStationForm = ResolvePrintStationForm();
                if (_printStationForm != null)
                {
                    _printStationForm.Text = $"Stazione Stampa Comande - Profilo: {_profileSettings.ProfileName}";
                    _printStationForm.FormClosing += PrintStationForm_FormClosing;
                    _printStationForm.Show();
                    _printStationForm.Activate();
                }
                else
                {
                     _logger.LogError($"Failed to recreate PrintStationForm on Show/Hide click (Profile: {_profileSettings.ProfileName}).");
                }
            }
            else
            {
                if (_printStationForm.Visible)
                {
                    _logger.LogInformation($"Hiding PrintStationForm (Profile: {_profileSettings.ProfileName}).");
                    _printStationForm.Hide();
                }
                else
                {
                    _logger.LogInformation($"Showing PrintStationForm (Profile: {_profileSettings.ProfileName}).");
                    _printStationForm.Show();
                    _printStationForm.Activate(); 
                }
            }
        }

        private void UpdateTrayIconTooltip(object? sender, string status) 
        {
            _logger.LogDebug($"UpdateTrayIconTooltip triggered with base status: {status} for profile {_profileSettings.ProfileName}");

            Icon? selectedIcon = _iconDefault; 
            string lowerStatus = status.ToLowerInvariant(); 

            if (lowerStatus.Contains("errore") || lowerStatus.Contains("fallita") || lowerStatus.Contains("non valido"))
                selectedIcon = _iconError;
            else if (lowerStatus.Contains("disconnesso"))
                selectedIcon = _iconDisconnected;
            else if (lowerStatus.Contains("riconnessione") || lowerStatus.Contains("connessione in corso") || lowerStatus.Contains("inizializzazione"))
                selectedIcon = _iconConnecting;
            else if (lowerStatus.Contains("connesso") || lowerStatus.Contains("registrazione") || lowerStatus.Contains("registrato")) // Check for "registrato" not "registrato e pronto"
                selectedIcon = _iconConnected;

            if (_uiContext != null)
            {
                _logger.LogDebug($"UI context found for profile '{_profileSettings.ProfileName}', posting update for tray icon text and icon...");
                _uiContext.Post(_ =>
                {
                    _logger.LogDebug($"Executing posted UI update for status: {status} (Profile: {_profileSettings.ProfileName})");
                    if (_notifyIcon != null) 
                    {
                        _logger.LogDebug($"NotifyIcon found for profile '{_profileSettings.ProfileName}', setting text and icon.");
                        // The status received here from SignalRService might already include the profile name.
                        // If SignalRService's OnConnectionStatusChanged sends "base status", then prepend profile here.
                        // If it sends "[Profile] base status", then use 'status' directly.
                        // Based on SignalRService change, it sends "[Profile] base status", so 'status' is fine.
                        _notifyIcon.Text = $"SagraFacile Printer - {status}"; 
                        if (selectedIcon != null)
                        {
                            _notifyIcon.Icon = selectedIcon;
                        }
                    }
                    else
                    {
                        _logger.LogWarning($"NotifyIcon was null when posted UI update executed (Profile: {_profileSettings.ProfileName}).");
                    }
                }, null);
            }
            else
            {
                _logger?.LogWarning($"UI SynchronizationContext not available for profile '{_profileSettings.ProfileName}'. Attempting direct update for NotifyIcon text and icon.");
                if (_notifyIcon != null)
                {
                    _notifyIcon.Text = $"SagraFacile Printer - {status}";
                    if (selectedIcon != null)
                    {
                        _notifyIcon.Icon = selectedIcon;
                    }
                }
            }
        }

        private void OnSettingsClicked(object? sender, EventArgs e)
        {
            _logger.LogDebug($"Settings menu item clicked for profile: {_profileSettings.ProfileName}.");
            
            string appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SagraFacilePrinterService");
            string profilesDir = Path.Combine(appDataFolder, "profiles");
            Directory.CreateDirectory(profilesDir); // Ensure it exists

            using (var settingsForm = new SettingsForm(_profileSettings.ProfileName, profilesDir))
            {
                settingsForm.SignalRServiceInstance = _signalRService; 
                _logger.LogDebug($"SettingsForm instance created and linked with SignalRService for profile '{_profileSettings.ProfileName}'.");
                settingsForm.ShowDialog();
            }
            _logger.LogDebug($"Settings form closed for profile '{_profileSettings.ProfileName}'.");
        }

        private void OnExitClicked(object? sender, EventArgs e)
        {
            _logger.LogInformation($"Exit menu item clicked for profile '{_profileSettings.ProfileName}'. Stopping application instance.");
            if (_printStationForm != null)
            {
                _printStationForm.FormClosing -= PrintStationForm_FormClosing;
            }
            _appLifetime.StopApplication(); 
        }

        private void OnStopping()
        {
            _logger.LogInformation($"Application stopping... (Profile: {_profileSettings.ProfileName})");
            if (_notifyIcon != null)
            {
                if (_signalRService != null) 
                {
                    _signalRService.ConnectionStatusChanged -= UpdateTrayIconTooltip; 
                }
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _notifyIcon = null;
                _logger.LogInformation($"Tray icon disposed during OnStopping (Profile: {_profileSettings.ProfileName}).");
            }
            if (_printStationForm != null && !_printStationForm.IsDisposed)
            {
                if (_uiContext != null && _printStationForm.InvokeRequired)
                {
                    _uiContext.Post(_ => {
                        if (!_printStationForm.IsDisposed) _printStationForm.Close();
                    }, null);
                }
                else if (!_printStationForm.IsDisposed)
                {
                    _printStationForm.Close();
                }
            }
        }

        private void OnStopped()
        {
            _logger.LogInformation($"Application stopped. (Profile: {_profileSettings.ProfileName})");
        }
    }
}
