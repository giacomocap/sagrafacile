using SagraFacile.WindowsPrinterService.Models;
using SagraFacile.WindowsPrinterService.Services;
using System;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Extensions.Logging; // Re-introduce ILogger

namespace SagraFacile.WindowsPrinterService
{
    public partial class PrintStationForm : Form
    {
        private readonly SignalRService _signalRService;
        private readonly ILogger<PrintStationForm> _logger; // Re-introduce Logger

        public PrintStationForm(SignalRService signalRService, ILogger<PrintStationForm> logger)
        {
            InitializeComponent();
            _signalRService = signalRService ?? throw new ArgumentNullException(nameof(signalRService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger)); // Initialize Logger

            // Subscribe to events
            _signalRService.OnDemandQueueCountChanged += SignalRService_OnDemandQueueCountChanged;
            _signalRService.ConnectionStatusChanged += SignalRService_ConnectionStatusChanged;

            // Set initial state - will be more robustly set in Load event
        }

        private void PrintStationForm_Load(object sender, EventArgs e)
        {
            // Set initial state when form is loaded and handle is created
            UpdateProfileAndPrinterLabels(); // New method to update profile and printer name
            UpdateQueueCountLabel();
            UpdateConnectionStatusLabel();
            LogActivity("Finestra Stazione di Stampa aperta.");
            this.AcceptButton = btnPrintNext; // Ensure Enter key triggers the button
        }

        private void UpdateProfileAndPrinterLabels()
        {
            if (!IsHandleCreated) return;

            lblProfileName.Text = $"Profilo: {_signalRService.CurrentProfileName ?? "N/D"}";
            lblPrinterName.Text = $"Stampante: {_signalRService.ActiveProfileSettings?.SelectedPrinter ?? "N/D"}";
        }

        private void SignalRService_ConnectionStatusChanged(object? sender, string status)
        {
            if (!IsHandleCreated) return;

            if (lblConnectionStatus.InvokeRequired)
            {
                lblConnectionStatus.Invoke(new Action(() => UpdateConnectionStatusLabel(status)));
            }
            else
            {
                UpdateConnectionStatusLabel(status);
            }
        }
        
        private void UpdateConnectionStatusLabel(string? status = null)
        {
            if (!IsHandleCreated) return;

            var currentStatus = _signalRService.GetCurrentStatus();
            lblConnectionStatus.Text = $"Stato Connessione: {currentStatus.LastStatusMessage}";
            lblConnectionStatus.ForeColor = currentStatus.LastStatusColor;
        }

        private void SignalRService_OnDemandQueueCountChanged(object? sender, int count)
        {
            if (!IsHandleCreated) return;

            if (lblPendingCount.InvokeRequired)
            {
                lblPendingCount.Invoke(new Action(() => UpdateQueueCountLabel(count)));
            }
            else
            {
                UpdateQueueCountLabel(count);
            }
        }

        private void UpdateQueueCountLabel(int? count = null)
        {
            if (!IsHandleCreated) return;

            int currentCount = count ?? _signalRService.GetOnDemandQueueCount();
            lblPendingCount.Text = $"Comande in Attesa: {currentCount}";
            btnPrintNext.Enabled = currentCount > 0;
        }

        private async void btnPrintNext_Click(object sender, EventArgs e)
        {
            _logger.LogInformation("Tentativo di stampa prossima comanda...");
            LogActivity("Tentativo di stampa prossima comanda...");
            btnPrintNext.Enabled = false; 

            PrintJobItem? jobToPrint = _signalRService.DequeuePrintJob();

            if (jobToPrint != null)
            {
                _logger.LogInformation($"Stampa Job ID: {jobToPrint.JobId} per stampante");
                LogActivity($"Stampa Job ID: {jobToPrint.JobId} per stampante");
                bool success = await _signalRService.PrintQueuedJobAsync(jobToPrint);
                if (success)
                {
                    _logger.LogInformation($"Comanda Job ID: {jobToPrint.JobId} stampata con successo.");
                    LogActivity($"Comanda Job ID: {jobToPrint.JobId} stampata con successo.");
                }
                else
                {
                    _logger.LogError($"Errore durante la stampa della comanda Job ID: {jobToPrint.JobId}. Controllare i log del servizio.");
                    LogActivity($"Errore durante la stampa della comanda Job ID: {jobToPrint.JobId}. Controllare i log del servizio.");
                }
            }
            else
            {
                _logger.LogInformation("Nessuna comanda trovata in coda.");
                LogActivity("Nessuna comanda trovata in coda.");
            }
            
            if (IsHandleCreated)
            {
               UpdateQueueCountLabel();
            }
        }

        private void LogActivity(string message)
        {
            // _logger.LogInformation($"[PrintStationForm Activity] {message}"); // Use ILogger for structured logging
            
            if (!IsHandleCreated) return;

            if (txtActivityLog.InvokeRequired)
            {
                txtActivityLog.Invoke(new Action(() => PrependLogMessage(message)));
            }
            else
            {
                PrependLogMessage(message);
            }
        }

        private void PrependLogMessage(string message)
        {
            if (!IsHandleCreated) return;

            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            txtActivityLog.Text = $"[{timestamp}] {message}{Environment.NewLine}{txtActivityLog.Text}";
            if (txtActivityLog.Text.Length > 4000)
            {
                txtActivityLog.Text = txtActivityLog.Text.Substring(0, 4000);
            }
        }

        // Override Dispose to unsubscribe from events
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_signalRService != null)
                {
                    _signalRService.OnDemandQueueCountChanged -= SignalRService_OnDemandQueueCountChanged;
                    _signalRService.ConnectionStatusChanged -= SignalRService_ConnectionStatusChanged;
                }
                if (components != null)
                {
                    components.Dispose();
                }
            }
            base.Dispose(disposing);
        }
    }
}
