using Microsoft.Extensions.DependencyInjection; // Added for CreateScope()
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
// using SagraFacile.WindowsPrinterService.Services; // WebSocketListenerService no longer used directly here
using SagraFacile.WindowsPrinterService.Services;

namespace SagraFacile.WindowsPrinterService
{
    public class ApplicationLifetimeService : IHostedService
    {
        private readonly ILogger<ApplicationLifetimeService> _logger;
        // private readonly WebSocketListenerService _webSocketListenerService; // Removed
        private readonly SignalRService _signalRService; // Added
        private readonly IHostApplicationLifetime _appLifetime;
        private readonly IServiceProvider _serviceProvider; // To resolve forms/services later
        private ApplicationContext? _applicationContext; // To manage WinForms lifecycle
        private NotifyIcon? _notifyIcon; // Tray Icon
        private PrintStationForm? _printStationForm; // Main application form
        private SynchronizationContext? _uiContext; // To marshal UI updates
        private readonly ManualResetEvent _uiThreadReady = new ManualResetEvent(false); // Signal when UI thread is ready

        // Icons for different states
        private Icon? _iconDefault;
        private Icon? _iconConnecting;
        private Icon? _iconConnected;
        private Icon? _iconError;
        private Icon? _iconDisconnected;

        // Inject IServiceProvider to resolve dependencies like Forms later
        public ApplicationLifetimeService(
            ILogger<ApplicationLifetimeService> logger,
            // WebSocketListenerService webSocketListenerService, // Removed
            SignalRService signalRService, // Added
            IHostApplicationLifetime appLifetime,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            // _webSocketListenerService = webSocketListenerService; // Removed
            _signalRService = signalRService; // Added
            _appLifetime = appLifetime;
            _serviceProvider = serviceProvider;

            // Subscribe to the new event here
            // _webSocketListenerService.ListenerStatusChanged += UpdateTrayIconTooltip; // Removed
            _signalRService.ConnectionStatusChanged += UpdateTrayIconTooltip; // Added
            _logger.LogDebug("Subscribed UpdateTrayIconTooltip to SignalRService.ConnectionStatusChanged in constructor.");
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("ApplicationLifetimeService starting...");

            // Register the stopping/stopped handlers
            _appLifetime.ApplicationStopping.Register(OnStopping);
            _appLifetime.ApplicationStopped.Register(OnStopped);

            // We'll call OnStarted manually after the UI thread is ready

            // We need to run the WinForms message loop on a separate thread
            // because the Host's RunAsync blocks the main thread.
            var thread = new Thread(() =>
            {
                try
                {
                    // Explicitly install the WindowsFormsSynchronizationContext for this thread.
                    // This is often done automatically when Application.Run(Form) is called,
                    // but might be needed when using Application.Run(ApplicationContext).
                    WindowsFormsSynchronizationContext.AutoInstall = false; // Prevent potential double-install
                    SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
                    _logger.LogInformation("Explicitly set WindowsFormsSynchronizationContext for UI thread.");

                    // Initialize WinForms application context first
                    InitializeMainUI(); // This will now create and show PrintStationForm

                    if (_printStationForm == null || _applicationContext == null)
                    {
                        _logger.LogError("Main UI (PrintStationForm or ApplicationContext) failed to initialize. Cannot start WinForms message loop.");
                        _appLifetime.StopApplication(); // Signal host to shut down
                        return; // Exit the thread
                    }
                    
                    _logger.LogInformation("WinForms UI initialized. Starting message loop.");

                    // Signal that the UI thread is ready
                    _uiThreadReady.Set();
                    _logger.LogInformation("UI thread ready, signaled main thread to start SignalR service.");

                    // Application.Run will use the ApplicationContext which is aware of the main form if set,
                    // or just run the message loop if no main form is explicitly passed to Application.Run.
                    // If PrintStationForm is shown before Application.Run(ApplicationContext), it works.
                    Application.Run(_applicationContext); 
                    _logger.LogInformation("WinForms message loop stopped.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unhandled exception in WinForms thread.");
                    // Optionally trigger host shutdown
                    _appLifetime.StopApplication();
                }
            });
            thread.SetApartmentState(ApartmentState.STA); // Required for WinForms
            thread.Start();

            // Wait for the UI thread to be ready before starting the SignalR service
            _ = Task.Run(async () => // Made async Task
            {
                _logger.LogInformation("Waiting for UI thread to be ready before starting SignalR service...");
                _uiThreadReady.WaitOne(); // Block until UI thread signals it's ready
                _logger.LogInformation("UI thread is ready, calling OnStarted to start SignalR service.");
                await OnStartedAsync(cancellationToken); // Call new async OnStarted, pass CancellationToken
            }, cancellationToken); // Pass CancellationToken to Task.Run

            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("ApplicationLifetimeService stopping...");
            // Signal the SignalR service to stop
            await _signalRService.StopAsync(); // Call StopAsync on SignalRService

            // Clean up NotifyIcon and main form
            if (_printStationForm != null && !_printStationForm.IsDisposed)
            {
                _printStationForm.Close(); // This should trigger FormClosing, then Dispose
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
                _logger.LogInformation("Tray icon disposed.");
            }
             // Ensure Application.ExitThread is called if the message loop is still technically running
            if (Application.MessageLoop)
            {
                Application.ExitThread();
            }
        }

        // Renamed to OnStartedAsync and made async Task
        private async Task OnStartedAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Application started. Starting SignalR service...");
            await _signalRService.StartAsync(cancellationToken); // Pass CancellationToken
        }

        private void InitializeMainUI()
        {
            // Create the main form first
            _printStationForm = ResolvePrintStationForm();
            if (_printStationForm == null)
            {
                _logger.LogError("Failed to resolve PrintStationForm. Application cannot start main UI.");
                // _appLifetime.StopApplication(); // This might be too early or cause issues if called from UI thread setup
                return;
            }

            _printStationForm.FormClosing += PrintStationForm_FormClosing;
            
            // ApplicationContext can be created with the main form, or the main form can be shown before Application.Run(new ApplicationContext())
            // Using ApplicationContext(mainForm) is cleaner.
            _applicationContext = new ApplicationContext(_printStationForm); 
            // _printStationForm.Show(); // ApplicationContext(mainForm) should show it.

            // Capture the synchronization context for the UI thread *after* ApplicationContext is created
            _uiContext = SynchronizationContext.Current;
            if (_uiContext == null)
            {
                _logger.LogWarning("Could not capture UI SynchronizationContext within InitializeTrayIcon. Tray icon updates might not work correctly.");
            }

            // Create Context Menu
            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("Show/Hide Print Station", null, OnShowHidePrintStationClicked);
            contextMenu.Items.Add("Settings...", null, OnSettingsClicked);
            contextMenu.Items.Add("-"); // Separator
            contextMenu.Items.Add("Exit", null, OnExitClicked);

            // Initialize icons (using SystemIcons as placeholders)
            _iconDefault = SystemIcons.Application; // Default/Initial
            _iconConnecting = SystemIcons.Information; // Placeholder for a "connecting" icon
            _iconConnected = SystemIcons.Shield;       // Placeholder for a "connected/secure" icon (Green check often used)
            _iconError = SystemIcons.Error;           // Placeholder for an "error" icon
            _iconDisconnected = SystemIcons.Warning;    // Placeholder for a "disconnected/warning" icon


            // Create NotifyIcon
            _notifyIcon = new NotifyIcon
            {
                ContextMenuStrip = contextMenu,
                Text = "SagraFacile Printer Service",
                Visible = true,
                Icon = _iconDefault // Set initial icon
            };

            // Set initial tooltip (handler is already subscribed in constructor)
            UpdateTrayIconTooltip(null, "Initializing..."); // Initial status

            _logger.LogInformation("Main UI and Tray icon initialized.");
        }

        private PrintStationForm? ResolvePrintStationForm()
        {
            try
            {
                // Resolve directly from the service provider available to ApplicationLifetimeService.
                // ApplicationLifetimeService is a singleton, and PrintStationForm is transient.
                // This instance will be managed by ApplicationLifetimeService.
                var form = _serviceProvider.GetRequiredService<PrintStationForm>();
                return form;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to resolve or create PrintStationForm.");
                return null;
            }
        }

        private void PrintStationForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                _logger.LogInformation("PrintStationForm closing by user. Minimizing to tray.");
                e.Cancel = true; // Prevent the form from actually closing
                _printStationForm?.Hide();
                if (_notifyIcon != null)
                {
                    _notifyIcon.ShowBalloonTip(1000, "SagraFacile Printer Service", "L'applicazione è stata minimizzata nell'area di notifica.", ToolTipIcon.Info);
                }
            }
            else
            {
                _logger.LogInformation($"PrintStationForm closing due to {e.CloseReason}. Allowing close.");
                // If it's not user closing (e.g. ApplicationExitCall, TaskManagerShutdown), let it close.
                // This will eventually lead to ApplicationContext exiting the message loop.
            }
        }
        
        private void OnShowHidePrintStationClicked(object? sender, EventArgs e)
        {
            if (_printStationForm == null || _printStationForm.IsDisposed)
            {
                _logger.LogInformation("PrintStationForm is null or disposed. Recreating and showing.");
                _printStationForm = ResolvePrintStationForm();
                if (_printStationForm != null)
                {
                    _printStationForm.FormClosing += PrintStationForm_FormClosing;
                    _printStationForm.Show();
                    _printStationForm.Activate();
                }
                else
                {
                     _logger.LogError("Failed to recreate PrintStationForm on Show/Hide click.");
                }
            }
            else
            {
                if (_printStationForm.Visible)
                {
                    _logger.LogInformation("Hiding PrintStationForm.");
                    _printStationForm.Hide();
                }
                else
                {
                    _logger.LogInformation("Showing PrintStationForm.");
                    _printStationForm.Show();
                    _printStationForm.Activate(); // Bring to front
                }
            }
        }


        // Modified to accept string status directly
        private void UpdateTrayIconTooltip(object? sender, string status)
        {
            _logger.LogDebug("UpdateTrayIconTooltip triggered with status: {Status}", status);

            Icon? selectedIcon = _iconDefault; // Default icon
            string lowerStatus = status.ToLowerInvariant();

            if (lowerStatus.Contains("errore") || lowerStatus.Contains("fallita") || lowerStatus.Contains("non valido"))
                selectedIcon = _iconError;
            else if (lowerStatus.Contains("disconnesso"))
                selectedIcon = _iconDisconnected;
            else if (lowerStatus.Contains("riconnessione") || lowerStatus.Contains("connessione in corso") || lowerStatus.Contains("inizializzazione"))
                selectedIcon = _iconConnecting;
            else if (lowerStatus.Contains("connesso") || lowerStatus.Contains("registrazione") || lowerStatus.Contains("registrato e pronto"))
                selectedIcon = _iconConnected;

            // Marshal the UI update to the correct thread using the captured SynchronizationContext
            if (_uiContext != null)
            {
                _logger.LogDebug("UI context found, posting update for tray icon text and icon...");
                _uiContext.Post(_ =>
                {
                    _logger.LogDebug("Executing posted UI update for status: {Status}", status);
                    if (_notifyIcon != null) // Check again inside the posted action
                    {
                        _logger.LogDebug("NotifyIcon found, setting text and icon.");
                        _notifyIcon.Text = $"SagraFacile Printer Service - {status}";
                        if (selectedIcon != null)
                        {
                            _notifyIcon.Icon = selectedIcon;
                        }
                    }
                    else
                    {
                        _logger.LogWarning("NotifyIcon was null when posted UI update executed.");
                    }
                }, null);
            }
            else
            {
                // Fallback if context wasn't captured (shouldn't normally happen in WinForms STA thread)
                _logger?.LogWarning("UI SynchronizationContext not available. Attempting direct update for NotifyIcon text and icon.");
                if (_notifyIcon != null)
                {
                    _notifyIcon.Text = $"SagraFacile Printer Service - {status}";
                    if (selectedIcon != null)
                    {
                        _notifyIcon.Icon = selectedIcon;
                    }
                }
            }
        }

        private void OnSettingsClicked(object? sender, EventArgs e)
        {
            _logger.LogDebug("Settings menu item clicked.");
            // Resolve SettingsForm from the service provider and show it.
            // Using a scope ensures any scoped dependencies within the form are handled correctly.
            using var scope = _serviceProvider.CreateScope();
            var settingsForm = scope.ServiceProvider.GetRequiredService<SettingsForm>();
            
            // Pass the SettingsForm instance to SignalRService for status updates
            if (_signalRService != null)
            {
                _signalRService.SetSettingsForm(settingsForm);
                settingsForm.SignalRServiceInstance = _signalRService; // Pass SignalRService to form for restart capability
                _logger.LogDebug("SettingsForm instance linked with SignalRService.");
            }
            else
            {
                _logger.LogWarning("SignalRService instance was null. Cannot link SettingsForm.");
            }

            // ShowDialog ensures the form is modal and blocks until closed.
            settingsForm.ShowDialog();
            _logger.LogDebug("Settings form closed.");
        }

        private void OnExitClicked(object? sender, EventArgs e)
        {
            _logger.LogInformation("Exit menu item clicked. Stopping application.");
            // Unsubscribe from FormClosing to allow the form to close properly during shutdown
            if (_printStationForm != null)
            {
                _printStationForm.FormClosing -= PrintStationForm_FormClosing;
            }
            _appLifetime.StopApplication(); // Trigger graceful shutdown, which will call StopAsync
                                            // StopAsync will then handle closing the form if it's still open.
        }

        private void OnStopping()
        {
            _logger.LogInformation("Application stopping. SignalR service stop is handled in StopAsync.");
            // _webSocketListenerService.StopListening(); // Removed

            if (_notifyIcon != null)
            {
                // _webSocketListenerService.ListenerStatusChanged -= UpdateTrayIconTooltip; // Removed
                if (_signalRService != null) // Check if _signalRService is not null before unsubscribing
                {
                    _signalRService.ConnectionStatusChanged -= UpdateTrayIconTooltip; // Added
                }
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _notifyIcon = null;
                _logger.LogInformation("Tray icon disposed during OnStopping.");
            }
            // If PrintStationForm is still around, ensure it's closed.
            // This might be redundant if StopAsync handles it, but good for safety.
            if (_printStationForm != null && !_printStationForm.IsDisposed)
            {
                 // Ensure it's closed on the UI thread if possible, or just dispose
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
            _logger.LogInformation("Application stopped.");
        }
    }
}
