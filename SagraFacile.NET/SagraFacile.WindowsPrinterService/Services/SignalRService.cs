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
        private readonly ConcurrentQueue<PrintJobItem> _onDemandPrintQueue = new ConcurrentQueue<PrintJobItem>();
        private static readonly HttpClient _httpClient; 
        private static readonly JsonSerializerOptions _jsonSerializerOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };

        static SignalRService() 
        {
            var httpClientHandler = new HttpClientHandler();
            httpClientHandler.ServerCertificateCustomValidationCallback = (sender, certificate, chain, sslPolicyErrors) =>
            {
                Console.WriteLine("[WARN] SOLO SVILUPPO: Validazione certificato SSL bypassata per HttpClient (recupero configurazione stampante). NON USARE IN PRODUZIONE.");
                return true;
            };
            _httpClient = new HttpClient(httpClientHandler);
        }

        public event EventHandler<string>? ConnectionStatusChanged;
        public event EventHandler<int>? OnDemandQueueCountChanged;

        public SignalRService(ILogger<SignalRService> logger, IRawPrinter rawPrinter)
        {
            _logger = logger;
            _rawPrinter = rawPrinter;
            _lastStatusMessage = "Servizio non avviato (nessun profilo caricato)";
            _lastStatusColor = Color.Gray;
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
                _logger.LogError(error, $"Connessione SignalR chiusa. (Profilo: {_activeProfileSettings.ProfileName})");
                OnConnectionStatusChanged("Disconnesso");
            };

            // Backend no longer sends windowsPrinterName
            _hubConnection.On<string, byte[]>("PrintJob", HandlePrintJobAsync);

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
            if (string.IsNullOrWhiteSpace(hubHostAndPort) || string.IsNullOrWhiteSpace(instanceGuid))
            {
                _logger.LogError($"Impossibile recuperare la configurazione della stampante per profilo '{_activeProfileSettings?.ProfileName}': URL Base Hub o GUID Istanza mancanti.");
                _currentPrintMode = PrintMode.Immediate;
                _configuredWindowsPrinterName = null;
                return;
            }

            if (!Uri.TryCreate(hubHostAndPort.Trim(), UriKind.Absolute, out Uri? baseUri) ||
                (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
            {
                _logger.LogError($"URL Base Hub '{hubHostAndPort}' (Profilo: {_activeProfileSettings?.ProfileName}) non valido durante il recupero della configurazione.");
                _currentPrintMode = PrintMode.Immediate;
                _configuredWindowsPrinterName = null;
                return;
            }

            string configUrl = new Uri(baseUri, $"/api/printers/config/{instanceGuid}").ToString();
            _logger.LogInformation($"Recupero configurazione stampante per profilo '{_activeProfileSettings?.ProfileName}' da: {configUrl}");

            try
            {
                // The DTO from backend now only contains PrintMode
                var backendConfig = await _httpClient.GetFromJsonAsync<PrinterConfigDto>(configUrl, _jsonSerializerOptions, cancellationToken);

                if (backendConfig != null)
                {
                    _currentPrintMode = backendConfig.PrintMode;
                    // WindowsPrinterName is now sourced from the local profile
                    _configuredWindowsPrinterName = _activeProfileSettings?.SelectedPrinter; 
                    _logger.LogInformation($"Configurazione stampante recuperata per profilo '{_activeProfileSettings?.ProfileName}': PrintMode={_currentPrintMode}. WindowsPrinterName (da profilo)='{_configuredWindowsPrinterName}'");
                }
                else
                {
                    _logger.LogWarning($"Configurazione PrintMode non trovata o vuota dal backend per profilo '{_activeProfileSettings?.ProfileName}'. Utilizzo PrintMode.Immediate. PrinterName da profilo.");
                    _currentPrintMode = PrintMode.Immediate;
                    _configuredWindowsPrinterName = _activeProfileSettings?.SelectedPrinter;
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, $"Errore HTTP durante il recupero della configurazione della stampante da {configUrl} (Profilo: {_activeProfileSettings?.ProfileName}).");
                _currentPrintMode = PrintMode.Immediate; 
                _configuredWindowsPrinterName = null;
                OnConnectionStatusChanged($"Errore Config: {ex.StatusCode}");
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, $"Errore JSON durante il parsing della configurazione della stampante da {configUrl} (Profilo: {_activeProfileSettings?.ProfileName}).");
                _currentPrintMode = PrintMode.Immediate; 
                _configuredWindowsPrinterName = null;
                OnConnectionStatusChanged("Errore Config: JSON Invalido");
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                _logger.LogError(ex, $"Timeout durante il recupero della configurazione della stampante da {configUrl} (Profilo: {_activeProfileSettings?.ProfileName}).");
                _currentPrintMode = PrintMode.Immediate; 
                _configuredWindowsPrinterName = null;
                OnConnectionStatusChanged("Errore Config: Timeout");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Errore generico durante il recupero della configurazione della stampante da {configUrl} (Profilo: {_activeProfileSettings?.ProfileName}).");
                _currentPrintMode = PrintMode.Immediate; 
                _configuredWindowsPrinterName = null;
                OnConnectionStatusChanged("Errore Config: Generico");
            }
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

        // windowsPrinterName argument removed as it's no longer sent by the backend
        private async void HandlePrintJobAsync(string jobId, byte[] rawData)
        {
            // Use _configuredWindowsPrinterName which should be set from the profile
            string? printerToUse = _configuredWindowsPrinterName ?? _activeProfileSettings?.SelectedPrinter;

            _logger.LogInformation($"Print job received for profile '{_activeProfileSettings?.ProfileName}'. JobID: {jobId}, Target Printer (from profile): '{printerToUse}', Data Length: {rawData?.Length ?? 0}, Mode: {_currentPrintMode}");

            if (rawData != null)
            {
                // string hexString = string.Join(" ", rawData.Select(b => b.ToString("X2"))); // Can be very verbose
                // _logger.LogDebug("Received Print Job Data (HEX) for JobID {JobId}: {HexString}", jobId, hexString);
            }

            if (string.IsNullOrWhiteSpace(printerToUse))
            {
                _logger.LogWarning($"Print job received (JobID: {jobId}) but no target printer name configured in profile '{_activeProfileSettings?.ProfileName}'. Cannot print.");
                return;
            }
            if (rawData == null || rawData.Length == 0)
            {
                _logger.LogWarning($"Print job received with empty ESC/POS data. JobID: {jobId}, Target Printer: {printerToUse} (Profilo: {_activeProfileSettings?.ProfileName})");
                return;
            }

            if (_currentPrintMode == PrintMode.OnDemandWindows)
            {
                // PrintJobItem no longer needs targetWindowsPrinterName as it's implicit to the profile
                var printJobItem = new PrintJobItem(jobId, rawData); 
                _onDemandPrintQueue.Enqueue(printJobItem);
                _logger.LogInformation($"Job {jobId} accodato (Profilo: {_activeProfileSettings?.ProfileName}, Printer: {printerToUse}). Comande in coda: {_onDemandPrintQueue.Count}");
                OnDemandQueueCountChanged?.Invoke(this, _onDemandPrintQueue.Count);
            }
            else 
            {
                try
                {
                    bool success = await _rawPrinter.PrintRawAsync(printerToUse, rawData);
                    if (success)
                    {
                        _logger.LogInformation($"Print job {jobId} sent successfully to printer {printerToUse}. (Profilo: {_activeProfileSettings?.ProfileName})");
                    }
                    else
                    {
                        _logger.LogError($"Failed to send print job {jobId} to printer {printerToUse}. Check RawPrinter logs. (Profilo: {_activeProfileSettings?.ProfileName})");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Exception while processing print job {jobId} for printer {printerToUse}. (Profilo: {_activeProfileSettings?.ProfileName})");
                }
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

        public async Task<bool> TestPrintAsync(string printerName, string testData)
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

            _logger.LogInformation($"Attempting test print to printer: {printerName} (Profilo: {_activeProfileSettings?.ProfileName})");
            try
            {
                bool success = await _rawPrinter.PrintRawAsync(printerName, System.Text.Encoding.UTF8.GetBytes(testData)); 
                if (success)
                {
                    _logger.LogInformation($"Test print sent successfully to printer {printerName}. (Profilo: {_activeProfileSettings?.ProfileName})");
                }
                else
                {
                    _logger.LogError($"Failed to send test print to printer {printerName}. Check RawPrinter logs. (Profilo: {_activeProfileSettings?.ProfileName})");
                }
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Exception during test print to printer {printerName}. (Profilo: {_activeProfileSettings?.ProfileName})");
                return false;
            }
        }

        public PrintJobItem? DequeuePrintJob()
        {
            if (_onDemandPrintQueue.TryDequeue(out PrintJobItem? jobItem))
            {
                _logger.LogInformation($"Dequeued job {jobItem.JobId} (Profilo: {_activeProfileSettings?.ProfileName}). Remaining in queue: {_onDemandPrintQueue.Count}");
                OnDemandQueueCountChanged?.Invoke(this, _onDemandPrintQueue.Count);
                return jobItem;
            }
            _logger.LogInformation($"Attempted to dequeue job, but queue is empty. (Profilo: {_activeProfileSettings?.ProfileName})");
            return null;
        }

        public int GetOnDemandQueueCount()
        {
            return _onDemandPrintQueue.Count;
        }

        public async Task<bool> PrintQueuedJobAsync(PrintJobItem jobItem)
        {
            if (jobItem == null)
            {
                _logger.LogError($"PrintQueuedJobAsync called with null jobItem. (Profilo: {_activeProfileSettings?.ProfileName})");
                return false;
            }

            string? printerToUse = _configuredWindowsPrinterName ?? _activeProfileSettings?.SelectedPrinter;

            _logger.LogInformation($"Attempting to print queued job {jobItem.JobId} to profile printer '{printerToUse}' (Profilo: {_activeProfileSettings?.ProfileName})");
            try
            {
                if (string.IsNullOrWhiteSpace(printerToUse))
                {
                    _logger.LogError($"Cannot print job {jobItem.JobId}: No target printer name configured in profile '{_activeProfileSettings?.ProfileName}'.");
                    return false;
                }

                bool success = await _rawPrinter.PrintRawAsync(printerToUse, jobItem.RawData);
                if (success)
                {
                    _logger.LogInformation($"Successfully printed queued job {jobItem.JobId} to {printerToUse}. (Profilo: {_activeProfileSettings?.ProfileName})");
                }
                else
                {
                    _logger.LogError($"Failed to print queued job {jobItem.JobId} to {printerToUse}. (Profilo: {_activeProfileSettings?.ProfileName})");
                }
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Exception while printing queued job {jobItem.JobId} to profile printer '{printerToUse}'. (Profilo: {_activeProfileSettings?.ProfileName})");
                return false;
            }
        }
    }
}
