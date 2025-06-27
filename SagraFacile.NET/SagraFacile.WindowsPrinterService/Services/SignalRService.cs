using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using SagraFacile.WindowsPrinterService.Printing; // For IRawPrinter
using SagraFacile.WindowsPrinterService.Models; // For PrintMode, PrinterConfigDto, PrintJobItem, ProfileSettings
using System;
using System.Collections.Concurrent; // For ConcurrentQueue
using System.Drawing; // For Color
using System.Net.Http; // For HttpClient
using System.Net.Http.Json; // For ReadFromJsonAsync
using System.Text.Json; // For JsonSerializerOptions
using System.Text.Json.Serialization; // For JsonStringEnumConverter
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Drawing.Printing;
// using SagraFacile.WindowsPrinterService; // No longer needed directly if SettingsForm doesn't expose static config methods

namespace SagraFacile.WindowsPrinterService.Services
{
    public class SignalRService : IAsyncDisposable
    {
        private readonly ILogger<SignalRService> _logger;
        private readonly IRawPrinter _rawPrinter;
        private HubConnection? _hubConnection;
        private CancellationTokenSource? _cts;
        private SettingsForm? _settingsForm; // For UI updates if settings form is open
        private string _lastStatusMessage = "Inizializzazione..."; 
        private Color _lastStatusColor = Color.Orange; 

        private ProfileSettings? _activeProfileSettings; // To store the loaded profile
        public string? CurrentProfileName => _activeProfileSettings?.ProfileName;

        private PrintMode _currentPrintMode = PrintMode.Immediate;
        private string? _configuredWindowsPrinterName; 

        public event EventHandler<string>? ConnectionStatusChanged;
        public event EventHandler<int>? OnDemandQueueCountChanged;

        private readonly IPdfPrintingService _pdfPrintingService;
        private readonly IPrinterConfigurationService _printerConfigurationService;
        private readonly IPrintJobManager _printJobManager;

        public SignalRService(
            ILogger<SignalRService> logger, 
            IRawPrinter rawPrinter,
            IPdfPrintingService pdfPrintingService,
            IPrinterConfigurationService printerConfigurationService,
            IPrintJobManager printJobManager)
        {
            _logger = logger;
            _rawPrinter = rawPrinter;
            _pdfPrintingService = pdfPrintingService;
            _printerConfigurationService = printerConfigurationService;
            _printJobManager = printJobManager;
            _lastStatusMessage = "Servizio non avviato (nessun profilo caricato)";
            _lastStatusColor = Color.Gray;

            // Subscribe to print job manager events
            _printJobManager.QueueCountChanged += (sender, count) => OnDemandQueueCountChanged?.Invoke(this, count);
        }

        public void SetActiveProfile(ProfileSettings profileSettings)
        {
            _activeProfileSettings = profileSettings ?? throw new ArgumentNullException(nameof(profileSettings));
            _logger.LogInformation($"Profilo attivo impostato su: {profileSettings.ProfileName}");
            _lastStatusMessage = "Profilo caricato, in attesa di avvio...";
            _lastStatusColor = Color.Orange;
            // ConnectionStatusChanged?.Invoke(this, _lastStatusMessage); // Optionally notify immediately
        }

        public void SetSettingsForm(SettingsForm? form) 
        {
            _settingsForm = form;
            if (_settingsForm != null)
            {
                 _logger.LogDebug($"SettingsForm linked. Last known status internally: {_lastStatusMessage}");
                 // Immediately update the form with the current status
                 _settingsForm.UpdateConnectionStatus(_lastStatusMessage, _lastStatusColor);
            }
        }

        public (string LastStatusMessage, Color LastStatusColor) GetCurrentStatus()
        {
            return (_lastStatusMessage, _lastStatusColor);
        }

        public void ClearSettingsFormReference()
        {
            _logger.LogDebug("SettingsForm reference cleared in SignalRService.");
            _settingsForm = null;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            if (_activeProfileSettings == null)
            {
                _logger.LogError("Impossibile avviare SignalRService: nessun profilo attivo impostato.");
                OnConnectionStatusChanged("Errore: Nessun profilo caricato");
                return;
            }

            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            OnConnectionStatusChanged($"Inizializzazione servizio SignalR per profilo: {_activeProfileSettings.ProfileName}...");

            string? rawHubHostSetting = _activeProfileSettings.HubHostAndPort;
            string? instanceGuid = _activeProfileSettings.InstanceGuid;

            if (string.IsNullOrWhiteSpace(rawHubHostSetting) || string.IsNullOrWhiteSpace(instanceGuid) || !Guid.TryParse(instanceGuid, out _))
            {
                _logger.LogError($"Impostazioni SignalR per il profilo '{_activeProfileSettings.ProfileName}' (URL Base Hub o GUID Istanza) non valide. Il servizio non può avviarsi.");
                OnConnectionStatusChanged($"Errore Config Profilo '{_activeProfileSettings.ProfileName}'");
                return;
            }
            
            string hubHostAndPort = rawHubHostSetting.Trim();
            string currentInstanceGuid = instanceGuid;

            try
            {
                string originalInputForLog = hubHostAndPort;
                Uri tempUri;

                if (!hubHostAndPort.Contains("://"))
                {
                    if (hubHostAndPort.StartsWith("localhost", StringComparison.OrdinalIgnoreCase) || hubHostAndPort.StartsWith("127.0.0.1"))
                        tempUri = new Uri("http://" + hubHostAndPort);
                    else
                        tempUri = new Uri("https://" + hubHostAndPort);
                }
                else
                {
                    tempUri = new Uri(hubHostAndPort);
                }
                
                hubHostAndPort = $"{tempUri.Scheme}://{tempUri.DnsSafeHost}";
                if (!tempUri.IsDefaultPort)
                {
                    hubHostAndPort += $":{tempUri.Port}";
                }

                if(originalInputForLog != hubHostAndPort)
                {
                    _logger.LogInformation($"URL Base Hub normalizzato da '{originalInputForLog}' a '{hubHostAndPort}' per profilo '{_activeProfileSettings.ProfileName}'.");
                }
            }
            catch (UriFormatException ex)
            {
                _logger.LogError(ex, $"Formato URL Base Hub '{rawHubHostSetting}' (Profilo: {_activeProfileSettings.ProfileName}) non valido dopo tentativo di normalizzazione.");
                OnConnectionStatusChanged("Errore: Formato URL Hub non valido (normalizzazione)");
                return;
            }
            
            if (!Uri.TryCreate(hubHostAndPort, UriKind.Absolute, out Uri? baseUri) ||
                (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
            {
                _logger.LogError($"URL Base Hub normalizzato '{hubHostAndPort}' (Profilo: {_activeProfileSettings.ProfileName}) non è valido.");
                OnConnectionStatusChanged("Errore: Formato URL Hub non valido (post-normalizzazione)");
                return;
            }

            await FetchPrinterConfigurationAsync(hubHostAndPort, currentInstanceGuid, _cts.Token);

            string constructedHubUrl = new Uri(baseUri, "api/orderhub").ToString();
            _logger.LogInformation($"Avvio Servizio SignalR per Profilo '{_activeProfileSettings.ProfileName}'. URL Hub: {constructedHubUrl}, GUID Istanza: {currentInstanceGuid}, PrintMode: {_currentPrintMode}");

            _hubConnection = new HubConnectionBuilder()
                .WithUrl(constructedHubUrl, options =>
                {
                    options.HttpMessageHandlerFactory = (message) =>
                    {
                        if (message is HttpClientHandler clientHandler)
                        {
                            clientHandler.ServerCertificateCustomValidationCallback = (sender, certificate, chain, sslPolicyErrors) =>
                            {
                                _logger.LogWarning($"SOLO SVILUPPO (Profilo: {_activeProfileSettings.ProfileName}): Validazione certificato SSL bypassata (SignalR Hub). NON USARE IN PRODUZIONE.");
                                return true;
                            };
                        }
                        return message;
                    };
                })
                .WithAutomaticReconnect(new[] { TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(1) })
                .Build();

            _hubConnection.Reconnecting += error =>
            {
                _logger.LogWarning(error, $"Riconnessione SignalR in corso... (Profilo: {_activeProfileSettings.ProfileName})");
                OnConnectionStatusChanged("Riconnessione in corso...");
                return Task.CompletedTask;
            };

            _hubConnection.Reconnected += connectionId =>
            {
                _logger.LogInformation($"Connessione SignalR ristabilita con ID: {connectionId}. Nuova registrazione client... (Profilo: {_activeProfileSettings.ProfileName})");
                OnConnectionStatusChanged("Riconnesso. Registrazione...");
                _ = RegisterClientAsync(); 
                return Task.CompletedTask;
            };

            _hubConnection.Closed += async (error) =>
            {
                _logger.LogError(error, $"Connessione SignalR chiusa. (Profilo: {_activeProfileSettings?.ProfileName})");
                OnConnectionStatusChanged("Disconnesso");
            };

            // Updated to handle contentType
            _hubConnection.On<Guid, byte[], string>("PrintJob", HandlePrintJobAsync);

            await ConnectWithRetriesAsync();
        }

        private async Task ConnectWithRetriesAsync()
        {
            if (_hubConnection == null || _cts == null || _cts.IsCancellationRequested)
            {
                _logger.LogWarning($"ConnectWithRetriesAsync chiamato ma la connessione hub è nulla o la cancellazione è richiesta. (Profilo: {_activeProfileSettings?.ProfileName})");
                return;
            }

            OnConnectionStatusChanged("Connessione in corso...");
            try
            {
                await _hubConnection.StartAsync(_cts.Token);
                _logger.LogInformation($"Connessione SignalR stabilita. Registrazione client... (Profilo: {_activeProfileSettings?.ProfileName})");
                OnConnectionStatusChanged("Connesso. Registrazione...");
                await RegisterClientAsync();
            }
            catch (Exception ex) 
            {
                _logger.LogError(ex, $"Connessione all'Hub SignalR fallita. (Profilo: {_activeProfileSettings?.ProfileName})");
                string shortErrorMessage = ex.Message.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? ex.Message;
                OnConnectionStatusChanged($"Connessione Fallita: {shortErrorMessage}");
            }
        }
        
        private async Task FetchPrinterConfigurationAsync(string hubHostAndPort, string instanceGuid, CancellationToken cancellationToken)
        {
            var (printMode, printerName) = await _printerConfigurationService.FetchConfigurationAsync(
                hubHostAndPort, 
                instanceGuid, 
                _activeProfileSettings?.SelectedPrinter, 
                cancellationToken);

            _currentPrintMode = printMode;
            _configuredWindowsPrinterName = printerName;
        }

        private async Task RegisterClientAsync()
        {   
            if (_hubConnection == null || _hubConnection.State != HubConnectionState.Connected)
            {
                _logger.LogWarning($"Impossibile registrare il client per profilo '{_activeProfileSettings?.ProfileName}', HubConnection non è connesso.");
                OnConnectionStatusChanged("Registrazione Fallita (Non Connesso)");
                return;
            }
            
            string? currentInstanceGuid = _activeProfileSettings?.InstanceGuid;
            if (string.IsNullOrEmpty(currentInstanceGuid))
            {
                _logger.LogError($"Impossibile registrare il client: GUID Istanza nullo o vuoto per profilo '{_activeProfileSettings?.ProfileName}'.");
                OnConnectionStatusChanged("Errore: GUID Istanza mancante per registrazione");
                return;
            }

            try
            {
                _logger.LogInformation($"Invocazione RegisterPrinterClient per profilo '{_activeProfileSettings?.ProfileName}' con GUID: {currentInstanceGuid}");
                await _hubConnection.InvokeAsync("RegisterPrinterClient", currentInstanceGuid, _cts?.Token ?? CancellationToken.None);
                _logger.LogInformation($"Registrazione client per profilo '{_activeProfileSettings?.ProfileName}' avvenuta con successo.");
                OnConnectionStatusChanged($"Registrato ({_activeProfileSettings?.ProfileName} - {_currentPrintMode})");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Registrazione client con l'Hub SignalR fallita (Profilo: {_activeProfileSettings?.ProfileName}).");
                string shortErrorMessage = ex.Message.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? ex.Message;
                OnConnectionStatusChanged($"Registrazione Fallita: {shortErrorMessage}");
            }
        }

        private async void HandlePrintJobAsync(Guid jobId, byte[] rawData, string contentType)
        {
            string? printerToUse = _configuredWindowsPrinterName ?? _activeProfileSettings?.SelectedPrinter;
            _logger.LogInformation($"Print job received for profile '{_activeProfileSettings?.ProfileName}'. JobID: {jobId}, ContentType: {contentType}, Target Printer: '{printerToUse}', Data Length: {rawData?.Length ?? 0}, Mode: {_currentPrintMode}");

            if (string.IsNullOrWhiteSpace(printerToUse))
            {
                _logger.LogWarning($"Print job received (JobID: {jobId}) but no target printer name configured in profile '{_activeProfileSettings?.ProfileName}'. Cannot print.");
                await ReportStatusToServerAsync(jobId, false, "No printer configured in profile.");
                return;
            }
            if (rawData == null || rawData.Length == 0)
            {
                _logger.LogWarning($"Print job received with empty data. JobID: {jobId}, Target Printer: {printerToUse}");
                await ReportStatusToServerAsync(jobId, false, "Received empty print data.");
                return;
            }

            if (_currentPrintMode == PrintMode.OnDemandWindows)
            {
                // Create print job item with content type and paper size from profile
                var printJobItem = new PrintJobItem(jobId, rawData, contentType, _activeProfileSettings?.PaperSize);
                _printJobManager.EnqueueJob(printJobItem);
            }
            else // Immediate mode
            {
                var printJobItem = new PrintJobItem(jobId, rawData, contentType, _activeProfileSettings?.PaperSize);
                bool success = await _printJobManager.ProcessJobAsync(printJobItem, printerToUse, contentType);
                await ReportStatusToServerAsync(jobId, success, success ? null : "Print job processing failed");
            }
        }


        protected virtual void OnConnectionStatusChanged(string status)
        {
            string statusWithProfile = _activeProfileSettings?.ProfileName != null ? $"[{_activeProfileSettings.ProfileName}] {status}" : status;
            ConnectionStatusChanged?.Invoke(this, statusWithProfile); // Send status with profile name to subscribers (like PrintStationForm)
            _logger.LogInformation($"Stato SignalR (Profilo: {_activeProfileSettings?.ProfileName}): {status}"); 

            _lastStatusMessage = status; // Store the base status message
            _lastStatusColor = DetermineColorForStatus(status);

            if (_settingsForm != null && !_settingsForm.IsDisposed && _settingsForm.IsHandleCreated)
            {
                try
                {
                    // SettingsForm should display the base status, as it's specific to its own context
                    _settingsForm.UpdateConnectionStatus(status, _lastStatusColor);
                }
                catch (Exception ex) when (ex is ObjectDisposedException || ex is InvalidOperationException)
                {
                    _logger.LogWarning(ex, $"Failed to update SettingsForm status due to form/control being disposed. Status was: {status}");
                }
            }
        }

        private Color DetermineColorForStatus(string status)
        {
            string lowerStatus = status.ToLowerInvariant(); 
            if (lowerStatus.Contains("errore") || lowerStatus.Contains("fallita") || lowerStatus.Contains("non valido") || lowerStatus.Contains("disconnesso"))
                return Color.Red;
            if (lowerStatus.Contains("riconnessione") || lowerStatus.Contains("connessione in corso") || lowerStatus.Contains("inizializzazione"))
                return Color.Orange;
            if (lowerStatus.Contains("connesso") || lowerStatus.Contains("registrazione") || lowerStatus.Contains("registrato"))
                return Color.Green;
            
            return Color.Black; 
        }

        public async Task StopAsync()
        {
            OnConnectionStatusChanged("Arresto del servizio...");
            _logger.LogInformation($"SignalRService stopping... (Profilo: {_activeProfileSettings?.ProfileName})");
            if (_cts != null)
            {
                _cts.Cancel();
            }
            if (_hubConnection != null)
            {
                await _hubConnection.StopAsync();
            }
        }

        private Task<bool> PrintStandardTestPageAsync(string printerName, string testData, string? paperSizeName)
        {
            try
            {
                var printDoc = new PrintDocument();
                printDoc.PrinterSettings.PrinterName = printerName;
                printDoc.DocumentName = "SagraFacile Test Print";

                if (!string.IsNullOrEmpty(paperSizeName))
                {
                    bool paperSizeFound = false;
                    foreach (PaperSize ps in printDoc.PrinterSettings.PaperSizes)
                    {
                        if (ps.PaperName == paperSizeName)
                        {
                            printDoc.DefaultPageSettings.PaperSize = ps;
                            paperSizeFound = true;
                            break;
                        }
                    }
                    if (!paperSizeFound)
                    {
                        _logger.LogWarning($"Paper size '{paperSizeName}' not found for printer '{printerName}'. Using printer default.");
                    }
                }

                string contentToPrint = testData;

                printDoc.PrintPage += (sender, e) =>
                {
                    if (e.Graphics == null) return;
                    using (var font = new Font("Arial", 12))
                    using (var brush = new SolidBrush(Color.Black))
                    {
                        e.Graphics.DrawString(
                            contentToPrint,
                            font,
                            brush,
                            new PointF(50, 50)
                        );
                    }
                };

                printDoc.Print();
                _logger.LogInformation($"Successfully spooled GDI test print job to standard printer {printerName}.");
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to print test page to standard printer {printerName}.");
                return Task.FromResult(false);
            }
        }

        public async ValueTask DisposeAsync()
        {
            _logger.LogInformation($"Disposing SignalRService... (Profilo: {_activeProfileSettings?.ProfileName})");
            if (_cts != null) 
            {
                if (!_cts.IsCancellationRequested) _cts.Cancel();
                _cts.Dispose();
            }
            if (_hubConnection != null)
            {
                await _hubConnection.DisposeAsync();
            }
            // _httpClient is static and shared, should not be disposed here per instance.
            // It will be disposed when the application exits if it's managed by DI, or never if truly static.
            // For this service, it's fine as is.
            GC.SuppressFinalize(this);
        }

        public async Task RestartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation($"SignalRService restarting... (Profilo: {_activeProfileSettings?.ProfileName})");
            OnConnectionStatusChanged("Riavvio del servizio in corso...");
            await StopAsync();
            await Task.Delay(500, cancellationToken);
            await StartAsync(cancellationToken);
        }

        public async Task<bool> TestPrintAsync(string printerName, string testData, LocalPrinterType printerType, string? paperSize)
        {
            if (string.IsNullOrWhiteSpace(printerName))
            {
                _logger.LogWarning($"Test print requested with no printer name specified. (Profilo: {_activeProfileSettings?.ProfileName})");
                return false;
            }
            if (string.IsNullOrWhiteSpace(testData))
            {
                _logger.LogWarning($"Test print requested with no data for printer {printerName}. (Profilo: {_activeProfileSettings?.ProfileName})");
                return false;
            }

            _logger.LogInformation($"Attempting test print to printer: {printerName} (Profilo: {_activeProfileSettings?.ProfileName}, Tipo: {printerType})");

            try
            {
                bool success;
                if (printerType == LocalPrinterType.Standard)
                {
                    _logger.LogInformation("Using standard GDI printing method for local test print.");
                    success = await PrintStandardTestPageAsync(printerName, testData, paperSize);
                }
                else
                {
                    _logger.LogInformation("Using raw ESC/POS printing method for local test print.");
                    // Ensure test data for ESC/POS includes cut command if needed
                    string escPosTestData = testData + "\n\n\n\n\x1D\x56\x00"; // Add paper cut command
                    success = await _rawPrinter.PrintRawAsync(printerName, System.Text.Encoding.UTF8.GetBytes(escPosTestData));
                }

                if (success)
                {
                    _logger.LogInformation($"Test print sent successfully to printer {printerName}.");
                }
                else
                {
                    _logger.LogError($"Failed to send test print to printer {printerName}.");
                }
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Exception during test print to printer {printerName}.");
                return false;
            }
        }

        public PrintJobItem? DequeuePrintJob()
        {
            return _printJobManager.DequeueJob();
        }

        public int GetOnDemandQueueCount()
        {
            return _printJobManager.GetQueueCount();
        }

        public async Task<bool> PrintQueuedJobAsync(PrintJobItem jobItem)
        {
            string? printerToUse = _configuredWindowsPrinterName ?? _activeProfileSettings?.SelectedPrinter;
            if (string.IsNullOrWhiteSpace(printerToUse))
            {
                await ReportStatusToServerAsync(jobItem.JobId, false, "No printer configured in profile.");
                return false;
            }

            // Use ESC/POS content type for queued jobs (typically comandas)
            bool success = await _printJobManager.ProcessJobAsync(jobItem, printerToUse, "application/vnd.escpos");
            await ReportStatusToServerAsync(jobItem.JobId, success, success ? null : "Print job processing failed");
            return success;
        }

        private async Task ReportStatusToServerAsync(Guid jobId, bool success, string? errorMessage)
        {
            if (_hubConnection == null || _hubConnection.State != HubConnectionState.Connected)
            {
                _logger.LogWarning($"Cannot report status for job {jobId}, hub is not connected.");
                return;
            }

            try
            {
                await _hubConnection.InvokeAsync("ReportPrintJobStatus", jobId, success, errorMessage);
                _logger.LogInformation("Successfully reported status for job {JobId} to server. Success: {Success}", jobId, success);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to report print job status for job {JobId} to server.", jobId);
            }
        }
    }
}
