using SagraFacile.WindowsPrinterService.Models;
using SagraFacile.WindowsPrinterService.Services;
using System;
using System.Drawing;
using System.Windows.Forms;
// using Microsoft.Extensions.Logging; // ILogger removed for simplicity for now

namespace SagraFacile.WindowsPrinterService
{
    public partial class PrintStationForm : Form
    {
        private readonly SignalRService _signalRService;
        // private readonly ILogger<PrintStationForm> _logger; // Logger removed for now

        public PrintStationForm(SignalRService signalRService /*, ILogger<PrintStationForm> logger */)
        {
            InitializeComponent();
            _signalRService = signalRService ?? throw new ArgumentNullException(nameof(signalRService));
            // _logger = logger ?? throw new ArgumentNullException(nameof(logger)); // Logger removed

            // Subscribe to events
            _signalRService.OnDemandQueueCountChanged += SignalRService_OnDemandQueueCountChanged;
            _signalRService.ConnectionStatusChanged += SignalRService_ConnectionStatusChanged;

            // Set initial state - will be more robustly set in Load event
        }

        private void PrintStationForm_Load(object sender, EventArgs e)
        {
            // Set initial state when form is loaded and handle is created
            UpdateQueueCountLabel();
            UpdateConnectionStatusLabel();
            LogActivity("Finestra Stazione di Stampa aperta.");
            this.AcceptButton = btnPrintNext; // Ensure Enter key triggers the button
        }

        private void SignalRService_ConnectionStatusChanged(object? sender, string status)
        {
            if (!IsHandleCreated) return; // Check if handle created

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
            if (!IsHandleCreated) return; // Check if handle created

            if (status == null)
            {
                var currentStatus = _signalRService.GetCurrentStatus();
                lblConnectionStatus.Text = $"Stato Connessione: {currentStatus.LastStatusMessage}";
                lblConnectionStatus.ForeColor = currentStatus.LastStatusColor;
            }
            else
            {
                lblConnectionStatus.Text = $"Stato Connessione: {status}";
                string lowerStatus = status.ToLowerInvariant();
                if (lowerStatus.Contains("errore") || lowerStatus.Contains("fallita")) lblConnectionStatus.ForeColor = Color.Red;
                else if (lowerStatus.Contains("riconnessione") || lowerStatus.Contains("connessione in corso")) lblConnectionStatus.ForeColor = Color.Orange;
                else if (lowerStatus.Contains("registrato") || lowerStatus.Contains("connesso")) lblConnectionStatus.ForeColor = Color.Green;
                else lblConnectionStatus.ForeColor = Color.Black;
            }
        }

        private void SignalRService_OnDemandQueueCountChanged(object? sender, int count)
        {
            if (!IsHandleCreated) return; // Check if handle created

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
            if (!IsHandleCreated) return; // Check if handle created

            int currentCount = count ?? _signalRService.GetOnDemandQueueCount();
            lblPendingCount.Text = $"Comande in Attesa: {currentCount}";
            btnPrintNext.Enabled = currentCount > 0;
        }

        private async void btnPrintNext_Click(object sender, EventArgs e)
        {
            LogActivity("Tentativo di stampa prossima comanda...");
            btnPrintNext.Enabled = false; 

            PrintJobItem? jobToPrint = _signalRService.DequeuePrintJob();

            if (jobToPrint != null)
            {
                LogActivity($"Stampa Job ID: {jobToPrint.JobId} per stampante: {jobToPrint.TargetWindowsPrinterName}...");
                bool success = await _signalRService.PrintQueuedJobAsync(jobToPrint);
                if (success)
                {
                    LogActivity($"Comanda Job ID: {jobToPrint.JobId} stampata con successo.");
                }
                else
                {
                    LogActivity($"Errore durante la stampa della comanda Job ID: {jobToPrint.JobId}. Controllare i log del servizio.");
                }
            }
            else
            {
                LogActivity("Nessuna comanda trovata in coda.");
            }
            
            if (IsHandleCreated) // Check handle before updating UI
            {
               UpdateQueueCountLabel(); // This will re-evaluate btnPrintNext.Enabled
            }
        }

        private void LogActivity(string message)
        {
            Console.WriteLine($"[PrintStationForm] {DateTime.Now:HH:mm:ss} - {message}"); // Simple console log for now.
            
            if (!IsHandleCreated) return; // Check if handle created

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
            if (!IsHandleCreated) return; // Check if handle created

            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            txtActivityLog.Text = $"[{timestamp}] {message}{Environment.NewLine}{txtActivityLog.Text}";
            if (txtActivityLog.Text.Length > 4000) // Keep log trimmed (increased size a bit)
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
