using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using SagraFacile.WindowsPrinterService.Printing; // For IRawPrinter
using SagraFacile.WindowsPrinterService.Models; // For PrintMode, PrinterConfigDto, PrintJobItem
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
using SagraFacile.WindowsPrinterService; // Added to resolve SettingsForm type

namespace SagraFacile.WindowsPrinterService.Services
{
    public class SignalRService : IAsyncDisposable
    {
        private readonly ILogger<SignalRService> _logger;
        private readonly IRawPrinter _rawPrinter;
        private HubConnection? _hubConnection;
        private CancellationTokenSource? _cts;
        private SettingsForm? _settingsForm;
        private string _lastStatusMessage = "Inizializzazione..."; // Store last status
        private Color _lastStatusColor = Color.Orange; // Store color for last status

        private string _hubHostAndPort = "localhost:7055"; // Default, will be overridden by settings
        private string _instanceGuid = "";
        // RegistrationToken removed

        private PrintMode _currentPrintMode = PrintMode.Immediate;
        private string? _configuredWindowsPrinterName; // This will be the printer name fetched from backend config for OnDemand mode
        private readonly ConcurrentQueue<PrintJobItem> _onDemandPrintQueue = new ConcurrentQueue<PrintJobItem>();
        private static readonly HttpClient _httpClient; // For fetching printer config
        private static readonly JsonSerializerOptions _jsonSerializerOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };

        static SignalRService() // Static constructor for _httpClient initialization
        {
            var httpClientHandler = new HttpClientHandler();
            // DANGER: Trusts all certificates. For DEBUGGING/DEVELOPMENT ONLY.
            // TODO: Implement proper certificate validation for production environments.
            httpClientHandler.ServerCertificateCustomValidationCallback = (sender, certificate, chain, sslPolicyErrors) =>
            {
                // This warning will appear in the console output of the WindowsPrinterService.
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
            // Initialize with a default status
            _lastStatusMessage = "Servizio non avviato";
            _lastStatusColor = Color.Gray;
            // Configure HttpClient timeout if necessary
            // _httpClient.Timeout = TimeSpan.FromSeconds(30); 
        }

        public void SetSettingsForm(SettingsForm? form) // Allow null to also detach if needed, though Clear is more explicit
        {
            _settingsForm = form;
            // The SettingsForm will now request the initial status update itself via GetCurrentStatus() on its Load event.
            if (_settingsForm != null)
            {
                 _logger.LogDebug($"SettingsForm linked. It should request initial status on Load/Shown event using GetCurrentStatus(). Last known status internally: {_lastStatusMessage}");
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
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            OnConnectionStatusChanged("Inizializzazione servizio SignalR...");

            _logger.LogInformation("Tentativo di caricamento impostazioni per SignalR...");
            string rawHubHostSetting;
            (rawHubHostSetting, _instanceGuid) = SettingsForm.GetSignalRConfig();

            if (string.IsNullOrWhiteSpace(rawHubHostSetting) || string.IsNullOrWhiteSpace(_instanceGuid) || !Guid.TryParse(_instanceGuid, out _))
            {
                _logger.LogError("Impostazioni SignalR (URL Base Hub o GUID Istanza) non valide o non configurate. Il servizio non può avviarsi.");
                OnConnectionStatusChanged("Errore: Configurazione mancante/invalida");
                return;
            }

            _hubHostAndPort = rawHubHostSetting.Trim();

            // Normalize _hubHostAndPort: ensure scheme and remove any path.
            // It should end up like "https://your.domain.com" or "http://localhost:port"
            try
            {
                string originalInputForLog = _hubHostAndPort;
                Uri tempUri;

                if (!_hubHostAndPort.Contains("://"))
                {
                    // No scheme, assume https for non-localhost, http for localhost
                    if (_hubHostAndPort.StartsWith("localhost", StringComparison.OrdinalIgnoreCase) || _hubHostAndPort.StartsWith("127.0.0.1"))
                        tempUri = new Uri("http://" + _hubHostAndPort); // Allow http for localhost development
                    else
                        tempUri = new Uri("https://" + _hubHostAndPort);
                }
                else
                {
                    tempUri = new Uri(_hubHostAndPort);
                }
                
                // Reconstruct without path and query
                _hubHostAndPort = $"{tempUri.Scheme}://{tempUri.DnsSafeHost}";
                if (!tempUri.IsDefaultPort)
                {
                    _hubHostAndPort += $":{tempUri.Port}";
                }

                if(originalInputForLog != _hubHostAndPort)
                {
                    _logger.LogInformation($"URL Base Hub normalizzato da '{originalInputForLog}' a '{_hubHostAndPort}'.");
                }
            }
            catch (UriFormatException ex)
            {
                _logger.LogError(ex, $"Formato URL Base Hub '{rawHubHostSetting}' non valido dopo tentativo di normalizzazione.");
                OnConnectionStatusChanged("Errore: Formato URL Hub non valido (normalizzazione)");
                return;
            }
            
            // Validate the normalized _hubHostAndPort as a base URL
            if (!Uri.TryCreate(_hubHostAndPort, UriKind.Absolute, out Uri? baseUri) ||
                (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
            {
                // This error should ideally not be hit if normalization is correct
                _logger.LogError($"URL Base Hub normalizzato '{_hubHostAndPort}' non è valido. Controllare la logica di normalizzazione.");
                OnConnectionStatusChanged("Errore: Formato URL Hub non valido (post-normalizzazione)");
                return;
            }

            // Fetch printer configuration before starting SignalR connection
            // This uses the now-normalized _hubHostAndPort
            await FetchPrinterConfigurationAsync(_cts.Token);

            // baseUri is now the normalized scheme://host:port
            // The service appends "/api/orderhub" to this base.
            string constructedHubUrl = new Uri(baseUri, "api/orderhub").ToString();
            _logger.LogInformation($"Avvio Servizio SignalR. URL Hub: {constructedHubUrl}, GUID Istanza: {_instanceGuid}, PrintMode: {_currentPrintMode}");

            _hubConnection = new HubConnectionBuilder()
                .WithUrl(constructedHubUrl, options =>
                {
                    options.HttpMessageHandlerFactory = (message) =>
                    {
                        if (message is HttpClientHandler clientHandler)
                        {
                            // DANGER: Trusts all certificates. For DEBUGGING/DEVELOPMENT ONLY.
                            // TODO: Implement proper certificate validation for production environments.
                            clientHandler.ServerCertificateCustomValidationCallback = (sender, certificate, chain, sslPolicyErrors) =>
                            {
                                _logger.LogWarning("SOLO SVILUPPO: Validazione certificato SSL bypassata (SignalR Hub). NON USARE IN PRODUZIONE.");
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
                _logger.LogWarning(error, "Riconnessione SignalR in corso...");
                OnConnectionStatusChanged("Riconnessione in corso...");
                return Task.CompletedTask;
            };

            _hubConnection.Reconnected += connectionId =>
            {
                _logger.LogInformation($"Connessione SignalR ristabilita con ID: {connectionId}. Nuova registrazione client...");
                OnConnectionStatusChanged("Riconnesso. Registrazione...");
                _ = RegisterClientAsync(); 
                return Task.CompletedTask;
            };

            _hubConnection.Closed += async (error) =>
            {
                _logger.LogError(error, "Connessione SignalR chiusa.");
                OnConnectionStatusChanged("Disconnesso");
                // Consider attempting to reconnect after a delay if the closure was unexpected
                // await Task.Delay(TimeSpan.FromSeconds(5), _cts?.Token ?? CancellationToken.None);
                // if (_cts != null && !_cts.IsCancellationRequested) await ConnectWithRetriesAsync();
            };

            _hubConnection.On<string, string, byte[]>("PrintJob", HandlePrintJobAsync);

            await ConnectWithRetriesAsync();
        }

        private async Task ConnectWithRetriesAsync()
        {
            if (_hubConnection == null || _cts == null || _cts.IsCancellationRequested)
            {
                _logger.LogWarning("ConnectWithRetriesAsync chiamato ma la connessione hub è nulla o la cancellazione è richiesta.");
                return;
            }

            OnConnectionStatusChanged("Connessione in corso...");
            try
            {
                await _hubConnection.StartAsync(_cts.Token);
                _logger.LogInformation("Connessione SignalR stabilita. Registrazione client...");
                OnConnectionStatusChanged("Connesso. Registrazione...");
                await RegisterClientAsync();
            }
            catch (Exception ex) 
            {
                _logger.LogError(ex, "Connessione all'Hub SignalR fallita.");
                string shortErrorMessage = ex.Message.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? ex.Message;
                OnConnectionStatusChanged($"Connessione Fallita: {shortErrorMessage}");
            }
        }
        
        private async Task FetchPrinterConfigurationAsync(CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(_hubHostAndPort) || string.IsNullOrWhiteSpace(_instanceGuid))
            {
                _logger.LogError("Impossibile recuperare la configurazione della stampante: URL Base Hub o GUID Istanza mancanti.");
                _currentPrintMode = PrintMode.Immediate; // Default to immediate if config cannot be fetched
                _configuredWindowsPrinterName = null;
                return;
            }

            if (!Uri.TryCreate(_hubHostAndPort.Trim(), UriKind.Absolute, out Uri? baseUri) ||
                (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
            {
                _logger.LogError($"URL Base Hub '{_hubHostAndPort}' non valido durante il recupero della configurazione.");
                _currentPrintMode = PrintMode.Immediate;
                _configuredWindowsPrinterName = null;
                return;
            }

            string configUrl = new Uri(baseUri, $"/api/printers/config/{_instanceGuid}").ToString();
            _logger.LogInformation($"Recupero configurazione stampante da: {configUrl}");

            try
            {
                PrinterConfigDto? printerConfig = await _httpClient.GetFromJsonAsync<PrinterConfigDto>(configUrl, _jsonSerializerOptions, cancellationToken);

                if (printerConfig != null)
                {
                    _currentPrintMode = printerConfig.PrintMode;
                    _configuredWindowsPrinterName = printerConfig.WindowsPrinterName;
                    _logger.LogInformation($"Configurazione stampante recuperata: PrintMode={_currentPrintMode}, WindowsPrinterName='{_configuredWindowsPrinterName}'");
                }
                else
                {
                    _logger.LogWarning("Configurazione stampante non trovata o vuota dal backend. Utilizzo PrintMode.Immediate.");
                    _currentPrintMode = PrintMode.Immediate;
                    _configuredWindowsPrinterName = null;
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, $"Errore HTTP durante il recupero della configurazione della stampante da {configUrl}.");
                _currentPrintMode = PrintMode.Immediate; // Fallback
                _configuredWindowsPrinterName = null;
                OnConnectionStatusChanged($"Errore Config: {ex.StatusCode}");
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, $"Errore JSON durante il parsing della configurazione della stampante da {configUrl}.");
                _currentPrintMode = PrintMode.Immediate; // Fallback
                _configuredWindowsPrinterName = null;
                OnConnectionStatusChanged("Errore Config: JSON Invalido");
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                _logger.LogError(ex, $"Timeout durante il recupero della configurazione della stampante da {configUrl}.");
                _currentPrintMode = PrintMode.Immediate; // Fallback
                _configuredWindowsPrinterName = null;
                OnConnectionStatusChanged("Errore Config: Timeout");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Errore generico durante il recupero della configurazione della stampante da {configUrl}.");
                _currentPrintMode = PrintMode.Immediate; // Fallback
                _configuredWindowsPrinterName = null;
                OnConnectionStatusChanged("Errore Config: Generico");
            }
        }

        private async Task RegisterClientAsync()
        {   
            if (_hubConnection == null || _hubConnection.State != HubConnectionState.Connected)
            {
                _logger.LogWarning("Impossibile registrare il client, HubConnection non è connesso.");
                OnConnectionStatusChanged("Registrazione Fallita (Non Connesso)");
                return;
            }

            try
            {
                _logger.LogInformation($"Invocazione RegisterPrinterClient con GUID: {_instanceGuid}");
                await _hubConnection.InvokeAsync("RegisterPrinterClient", _instanceGuid, _cts?.Token ?? CancellationToken.None);
                _logger.LogInformation("Registrazione client avvenuta con successo.");
                OnConnectionStatusChanged($"Registrato ({_currentPrintMode})");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Registrazione client con l'Hub SignalR fallita.");
                string shortErrorMessage = ex.Message.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? ex.Message;
                OnConnectionStatusChanged($"Registrazione Fallita: {shortErrorMessage}");
            }
        }

        private async void HandlePrintJobAsync(string jobId, string windowsPrinterName, byte[] rawData)
        {
            _logger.LogInformation($"Print job received. JobID: {jobId}, Printer: {windowsPrinterName}, Data Length: {rawData?.Length ?? 0}, Mode: {_currentPrintMode}");

            if (rawData != null)
            {
                string hexString = string.Join(" ", rawData.Select(b => b.ToString("X2")));
                _logger.LogInformation("Received Print Job Data (HEX) for JobID {JobId}: {HexString}", jobId, hexString);
            }

            if (string.IsNullOrWhiteSpace(windowsPrinterName))
            {
                _logger.LogWarning("Print job received with no target printer name specified. JobID: {JobId}", jobId);
                // Potentially report back error to hub if feedback mechanism exists
                return;
            }
            if (rawData == null || rawData.Length == 0)
            {
                _logger.LogWarning("Print job received with empty ESC/POS data. JobID: {JobId}, Printer: {PrinterName}", jobId, windowsPrinterName);
                // Potentially report back error to hub
                return;
            }

            if (_currentPrintMode == PrintMode.OnDemandWindows)
            {
                var printJobItem = new PrintJobItem(jobId, windowsPrinterName, rawData);
                _onDemandPrintQueue.Enqueue(printJobItem);
                _logger.LogInformation($"Job {jobId} accodato per la stampante {windowsPrinterName}. Comande in coda: {_onDemandPrintQueue.Count}");
                OnDemandQueueCountChanged?.Invoke(this, _onDemandPrintQueue.Count);
                // Optionally, send an ack to the hub that the job was queued
            }
            else // Immediate mode
            {
                try
                {
                    bool success = await _rawPrinter.PrintRawAsync(windowsPrinterName, rawData);
                    if (success)
                    {
                        _logger.LogInformation($"Print job {jobId} sent successfully to printer {windowsPrinterName}.");
                        // Optionally, report success back to hub
                    }
                    else
                    {
                        _logger.LogError($"Failed to send print job {jobId} to printer {windowsPrinterName}. Check RawPrinter logs.");
                        // Optionally, report failure back to hub
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Exception while processing print job {jobId} for printer {windowsPrinterName}.");
                    // Optionally, report failure back to hub
                }
            }
        }

        protected virtual void OnConnectionStatusChanged(string status)
        {
            ConnectionStatusChanged?.Invoke(this, status);
            _logger.LogInformation("Stato SignalR: {Status}", status); 

            _lastStatusMessage = status; // Update last known status
            _lastStatusColor = DetermineColorForStatus(status); // Update last known color

            if (_settingsForm != null && !_settingsForm.IsDisposed && _settingsForm.IsHandleCreated) // Added IsHandleCreated check
            {
                try
                {
                    // The call to UpdateConnectionStatus will use _lastStatusMessage and _lastStatusColor
                    // if invoked directly from SetSettingsForm, or the current 'status' and 'statusColor' if called during an event.
                    // For consistency, always use the just-determined status and color here.
                    _settingsForm.UpdateConnectionStatus(status, _lastStatusColor);
                }
                catch (Exception ex) when (ex is ObjectDisposedException || ex is InvalidOperationException)
                {
                    // This can happen if the form is closed/disposed right as a status update comes in.
                    _logger.LogWarning(ex, $"Failed to update SettingsForm status due to form/control being disposed. Status was: {status}");
                }
            }
        }

        private Color DetermineColorForStatus(string status)
        {
            // Status messages passed here will be in Italian from OnConnectionStatusChanged calls
            string lowerStatus = status.ToLowerInvariant(); 
            if (lowerStatus.Contains("errore") || lowerStatus.Contains("fallita") || lowerStatus.Contains("non valido") || lowerStatus.Contains("disconnesso"))
                return Color.Red;
            if (lowerStatus.Contains("riconnessione") || lowerStatus.Contains("connessione in corso") || lowerStatus.Contains("inizializzazione"))
                return Color.Orange;
            if (lowerStatus.Contains("connesso") || lowerStatus.Contains("registrazione") || lowerStatus.Contains("registrato")) // Adjusted for new status
                return Color.Green;
            
            return Color.Black; // Colore predefinito
        }

        public async Task StopAsync()
        {
            OnConnectionStatusChanged("Arresto del servizio...");
            _logger.LogInformation("SignalRService stopping...");
            if (_cts != null)
            {
                _cts.Cancel();
            }
            if (_hubConnection != null)
            {
                await _hubConnection.StopAsync(); // Use the CancellationToken from _cts if StopAsync overload supports it
            }
        }

        public async ValueTask DisposeAsync()
        {
            _logger.LogInformation("Disposing SignalRService...");
            if (_cts != null) 
            {
                if (!_cts.IsCancellationRequested) _cts.Cancel();
                _cts.Dispose();
            }
            if (_hubConnection != null)
            {
                await _hubConnection.DisposeAsync();
            }
            _httpClient.Dispose(); // Dispose HttpClient
            GC.SuppressFinalize(this);
        }

        public async Task RestartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("SignalRService restarting...");
            OnConnectionStatusChanged("Riavvio del servizio in corso..."); // New status for UI
            await StopAsync(); 
            // Small delay to ensure resources are released if needed, though StopAsync should handle it.
            await Task.Delay(500, cancellationToken); 
            await StartAsync(cancellationToken);
        }

        public async Task<bool> TestPrintAsync(string printerName, string testData)
        {
            if (string.IsNullOrWhiteSpace(printerName))
            {
                _logger.LogWarning("Test print requested with no printer name specified.");
                return false;
            }
            if (string.IsNullOrWhiteSpace(testData))
            {
                _logger.LogWarning("Test print requested with no data for printer {PrinterName}.", printerName);
                return false;
            }

            _logger.LogInformation("Attempting test print to printer: {PrinterName}", printerName);
            try
            {
                // For test print, we always print immediately, regardless of _currentPrintMode
                bool success = await _rawPrinter.PrintRawAsync(printerName, System.Text.Encoding.UTF8.GetBytes(testData)); // Assuming testData is string
                if (success)
                {
                    _logger.LogInformation("Test print sent successfully to printer {PrinterName}.", printerName);
                }
                else
                {
                    _logger.LogError("Failed to send test print to printer {PrinterName}. Check RawPrinter logs.", printerName);
                }
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception during test print to printer {PrinterName}.", printerName);
                return false;
            }
        }

        // New methods for OnDemand Queue Management
        public PrintJobItem? DequeuePrintJob()
        {
            if (_onDemandPrintQueue.TryDequeue(out PrintJobItem? jobItem))
            {
                _logger.LogInformation($"Dequeued job {jobItem.JobId}. Remaining in queue: {_onDemandPrintQueue.Count}");
                OnDemandQueueCountChanged?.Invoke(this, _onDemandPrintQueue.Count);
                return jobItem;
            }
            _logger.LogInformation("Attempted to dequeue job, but queue is empty.");
            return null;
        }

        public int GetOnDemandQueueCount()
        {
            return _onDemandPrintQueue.Count;
        }

        // Method to be called by PrintStationForm to print a dequeued job
        public async Task<bool> PrintQueuedJobAsync(PrintJobItem jobItem)
        {
            if (jobItem == null)
            {
                _logger.LogError("PrintQueuedJobAsync called with null jobItem.");
                return false;
            }

            _logger.LogInformation($"Attempting to print queued job {jobItem.JobId} to {jobItem.TargetWindowsPrinterName}");
            try
            {
                // Use the TargetWindowsPrinterName from the job item, 
                // or fall back to _configuredWindowsPrinterName if the job's target is somehow empty (should not happen with current logic)
                string printerToUse = !string.IsNullOrWhiteSpace(jobItem.TargetWindowsPrinterName) 
                                      ? jobItem.TargetWindowsPrinterName 
                                      : _configuredWindowsPrinterName ?? string.Empty;

                if (string.IsNullOrWhiteSpace(printerToUse))
                {
                    _logger.LogError($"Cannot print job {jobItem.JobId}: No target printer name specified in job and no default configured printer name.");
                    return false;
                }

                bool success = await _rawPrinter.PrintRawAsync(printerToUse, jobItem.RawData);
                if (success)
                {
                    _logger.LogInformation($"Successfully printed queued job {jobItem.JobId} to {printerToUse}.");
                }
                else
                {
                    _logger.LogError($"Failed to print queued job {jobItem.JobId} to {printerToUse}.");
                }
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Exception while printing queued job {jobItem.JobId} to {jobItem.TargetWindowsPrinterName}.");
                return false;
            }
        }
    }
}
