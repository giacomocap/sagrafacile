using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using SagraFacile.WindowsPrinterService.Printing; // For IRawPrinter
using System;
using System.Drawing; // For Color
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using SagraFacile.WindowsPrinterService; // Added to resolve SettingsForm type
using System.Collections.Concurrent; // For ConcurrentQueue
using SagraFacile.WindowsPrinterService.Models; // For PrintMode and PrintJobItem
using SagraFacile.WindowsPrinterService.DTOs; // For PrinterConfigDto
using System.Net.Http;
using System.Text.Json;

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
        private string? _configuredWindowsPrinterName;
        private readonly ConcurrentQueue<PrintJobItem> _onDemandPrintQueue = new ConcurrentQueue<PrintJobItem>();
        private static readonly HttpClient _httpClient = new HttpClient(); // For fetching config

        public event EventHandler<string>? ConnectionStatusChanged;
        public event EventHandler<int>? OnDemandQueueCountChanged;


        public SignalRService(ILogger<SignalRService> logger, IRawPrinter rawPrinter)
        {
            _logger = logger;
            _rawPrinter = rawPrinter;
            // Initialize with a default status
            _lastStatusMessage = "Servizio non avviato";
            _lastStatusColor = Color.Gray;
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
            // _hubHostAndPort from settings should be the full base URL e.g. https://host:port or http://host:port
            (_hubHostAndPort, _instanceGuid) = SettingsForm.GetSignalRConfig(); 

            if (string.IsNullOrWhiteSpace(_hubHostAndPort) || string.IsNullOrWhiteSpace(_instanceGuid) || !Guid.TryParse(_instanceGuid, out _))
            {
                _logger.LogError("Impostazioni SignalR (URL Base Hub o GUID Istanza) non valide o non configurate. Il servizio non può avviarsi.");
                OnConnectionStatusChanged("Errore: Configurazione mancante/invalida");
                return;
            }

            // Validate _hubHostAndPort as a base URL
            if (!Uri.TryCreate(_hubHostAndPort.Trim(), UriKind.Absolute, out Uri? baseUri) || 
                (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
            {
                _logger.LogError($"URL Base Hub '{_hubHostAndPort}' non valido. Inserire un URL completo come 'http://localhost:5000' o 'https://tuoserver.com:7075'.");
                OnConnectionStatusChanged("Errore: Formato URL Hub non valido");
                return;
            }

            string constructedHubUrl = new Uri(baseUri, "api/orderhub").ToString();
            _logger.LogInformation($"Avvio Servizio SignalR. URL Hub: {constructedHubUrl}, GUID Istanza: {_instanceGuid}");

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
                                _logger.LogWarning("SOLO SVILUPPO: Validazione certificato SSL bypassata. NON USARE IN PRODUZIONE.");
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
                OnConnectionStatusChanged("Registrato. Caricamento configurazione stampante...");
                await FetchPrinterConfigurationAsync(); // Fetch config after successful registration
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Registrazione client con l'Hub SignalR fallita.");
                string shortErrorMessage = ex.Message.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? ex.Message;
                OnConnectionStatusChanged($"Registrazione Fallita: {shortErrorMessage}");
            }
        }

        private async Task FetchPrinterConfigurationAsync()
        {
            if (string.IsNullOrWhiteSpace(_hubHostAndPort) || !Uri.TryCreate(_hubHostAndPort, UriKind.Absolute, out Uri? baseUri))
            {
                _logger.LogError("URL base dell'hub non valido o non configurato. Impossibile recuperare la configurazione della stampante.");
                OnConnectionStatusChanged("Errore Config: URL Hub invalido");
                return;
            }

            if (string.IsNullOrWhiteSpace(_instanceGuid))
            {
                _logger.LogError("GUID istanza non valido o non configurato. Impossibile recuperare la configurazione della stampante.");
                OnConnectionStatusChanged("Errore Config: GUID invalido");
                return;
            }

            string configUrl = new Uri(baseUri, $"/api/printers/config/{_instanceGuid}").ToString();
            _logger.LogInformation($"Recupero configurazione stampante da: {configUrl}");

            try
            {
                HttpResponseMessage response = await _httpClient.GetAsync(configUrl, _cts?.Token ?? CancellationToken.None);
                if (response.IsSuccessStatusCode)
                {
                    string jsonResponse = await response.Content.ReadAsStringAsync(_cts?.Token ?? CancellationToken.None);
                    var printerConfig = JsonSerializer.Deserialize<PrinterConfigDto>(jsonResponse, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (printerConfig != null)
                    {
                        _currentPrintMode = printerConfig.PrintMode;
                        _configuredWindowsPrinterName = printerConfig.WindowsPrinterName;
                        _logger.LogInformation($"Configurazione stampante caricata: PrintMode={_currentPrintMode}, WindowsPrinterName='{_configuredWindowsPrinterName}'");
                        OnConnectionStatusChanged($"Pronto (Modo: {_currentPrintMode})");
                    }
                    else
                    {
                        _logger.LogError("Deserializzazione della configurazione della stampante fallita (risposta nulla).");
                        OnConnectionStatusChanged("Errore Config: Risposta Invalida");
                    }
                }
                else
                {
                    _logger.LogError($"Errore durante il recupero della configurazione della stampante: {response.StatusCode} - {await response.Content.ReadAsStringAsync()}");
                    OnConnectionStatusChanged($"Errore Config: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Eccezione durante il recupero della configurazione della stampante.");
                OnConnectionStatusChanged("Errore Config: Eccezione");
            }
        }


        private async void HandlePrintJobAsync(string jobId, string windowsPrinterName, byte[] rawData)
        {
            _logger.LogInformation($"Print job received. JobID: {jobId}, Printer (from job): {windowsPrinterName}, Data Length: {rawData?.Length ?? 0}, CurrentMode: {_currentPrintMode}");

            if (rawData != null)
            {
                // Limit hex string logging for brevity if data is very long
                int maxBytesToLog = Math.Min(rawData.Length, 64); 
                string hexString = string.Join(" ", rawData.Take(maxBytesToLog).Select(b => b.ToString("X2")));
                if (rawData.Length > maxBytesToLog) hexString += " ... (truncated)";
                _logger.LogInformation("Received Print Job Data (HEX) for JobID {JobId}: {HexString}", jobId, hexString);
            }

            if (rawData == null || rawData.Length == 0)
            {
                _logger.LogWarning("Print job received with empty ESC/POS data. JobID: {JobId}, Printer: {PrinterName}", jobId, windowsPrinterName);
                return;
            }

            if (_currentPrintMode == PrintMode.OnDemandWindows)
            {
                var printJobItem = new PrintJobItem(jobId, rawData);
                _onDemandPrintQueue.Enqueue(printJobItem);
                _logger.LogInformation($"Job {jobId} accodato per la stampa on-demand. Dimensione coda: {_onDemandPrintQueue.Count}");
                OnDemandQueueCountChanged?.Invoke(this, _onDemandPrintQueue.Count);
            }
            else // Immediate mode or if mode not determined (defaults to Immediate)
            {
                if (string.IsNullOrWhiteSpace(windowsPrinterName))
                {
                    _logger.LogWarning("Print job (Immediate Mode) received with no target printer name specified. JobID: {JobId}. Using configured name: '{ConfiguredName}'", jobId, _configuredWindowsPrinterName);
                    if (string.IsNullOrWhiteSpace(_configuredWindowsPrinterName))
                    {
                        _logger.LogError("Nessun nome stampante specificato nel job e nessun nome stampante configurato. Impossibile stampare JobID: {JobId}", jobId);
                        return;
                    }
                    windowsPrinterName = _configuredWindowsPrinterName; // Fallback to configured name
                }
                
                _logger.LogInformation($"Invio immediato del job {jobId} alla stampante {windowsPrinterName}.");
                try
                {
                    bool success = await _rawPrinter.PrintRawAsync(windowsPrinterName, rawData);
                    if (success)
                    {
                        _logger.LogInformation($"Print job {jobId} sent successfully to printer {windowsPrinterName}.");
                    }
                    else
                    {
                        _logger.LogError($"Failed to send print job {jobId} to printer {windowsPrinterName}. Check RawPrinter logs.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Exception while processing print job {jobId} for printer {windowsPrinterName}.");
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
            if (lowerStatus.Contains("riconnessione") || lowerStatus.Contains("connessione in corso") || lowerStatus.Contains("inizializzazione") || lowerStatus.Contains("caricamento configurazione"))
                return Color.Orange;
            if (lowerStatus.Contains("connesso") || lowerStatus.Contains("registrazione") || lowerStatus.Contains("registrato e pronto") || lowerStatus.Contains("pronto (modo:"))
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
                // For test print, we assume it's raw bytes. If testData is string, convert it.
                // For simplicity, assuming testData is already byte[] or can be converted.
                // If testData is a string, you'd need: byte[] bytes = System.Text.Encoding.Default.GetBytes(testData);
                // For now, assuming testData is a string and needs encoding.
                // This might need adjustment based on how testData is actually passed.
                // For raw ESC/POS, it should be byte[].
                // If the testData from SettingsForm is plain text, we need to encode it.
                // Let's assume testData is string and use default encoding for the test.
                byte[] testBytes = System.Text.Encoding.GetEncoding("ibm858").GetBytes(testData);


                bool success = await _rawPrinter.PrintRawAsync(printerName, testBytes);
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

        // --- Methods for On-Demand Printing ---

        public PrintJobItem? DequeueNextPrintJob()
        {
            if (_onDemandPrintQueue.TryDequeue(out PrintJobItem? jobItem))
            {
                _logger.LogInformation($"Dequeued job {jobItem.JobId}. Remaining in queue: {_onDemandPrintQueue.Count}");
                OnDemandQueueCountChanged?.Invoke(this, _onDemandPrintQueue.Count);
                return jobItem;
            }
            _logger.LogInformation("On-demand print queue is empty.");
            return null;
        }

        public int GetOnDemandQueueCount()
        {
            return _onDemandPrintQueue.Count;
        }

        public string? GetConfiguredWindowsPrinterName()
        {
            return _configuredWindowsPrinterName;
        }
    }
}
