using SagraFacile.WindowsPrinterService.Models;
using SagraFacile.WindowsPrinterService.Printing;
using SagraFacile.WindowsPrinterService.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Text;
using System.Windows.Forms;
using System.Threading.Tasks;

namespace SagraFacile.WindowsPrinterService
{
    public partial class PrintStationForm : Form
    {
        private readonly SignalRService _signalRService;
        private readonly IRawPrinter _rawPrinter;
        private readonly ILogger<PrintStationForm> _logger;

        public PrintStationForm(SignalRService signalRService, IRawPrinter rawPrinter, ILogger<PrintStationForm> logger)
        {
            InitializeComponent(); // This will be defined in Designer.cs
            _signalRService = signalRService;
            _rawPrinter = rawPrinter;
            _logger = logger;

            // Subscribe to queue count changes
            _signalRService.OnDemandQueueCountChanged += SignalRService_OnDemandQueueCountChanged;

            // Set initial state
            UpdateQueueCountDisplay(_signalRService.GetOnDemandQueueCount());
            this.Load += PrintStationForm_Load;
        }

        private void PrintStationForm_Load(object? sender, EventArgs e)
        {
            // Ensure the display is up-to-date when the form loads
            UpdateQueueCountDisplay(_signalRService.GetOnDemandQueueCount());
            AddLog("Modulo Stampa Comande Avviato.");
        }

        private void SignalRService_OnDemandQueueCountChanged(object? sender, int newCount)
        {
            // Ensure UI updates are on the UI thread
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => UpdateQueueCountDisplay(newCount)));
            }
            else
            {
                UpdateQueueCountDisplay(newCount);
            }
        }

        private void UpdateQueueCountDisplay(int count)
        {
            // Assuming a label named lblQueueCount exists on the form
            if (Controls.Find("lblQueueCount", true).FirstOrDefault() is Label lblQueueCount)
            {
                lblQueueCount.Text = $"Comande in Attesa: {count}";
            }
             // Assuming a button named btnPrintNext exists
            if (Controls.Find("btnPrintNext", true).FirstOrDefault() is Button btnPrintNext)
            {
                btnPrintNext.Enabled = count > 0;
            }
        }

        private async void BtnPrintNext_Click(object? sender, EventArgs e)
        {
            Button? btn = sender as Button;
            if (btn != null)
            {
                btn.Enabled = false; // Disable button to prevent double-clicks
            }

            PrintJobItem? jobToPrint = _signalRService.DequeueNextPrintJob();

            if (jobToPrint != null)
            {
                string? printerName = _signalRService.GetConfiguredWindowsPrinterName();
                if (string.IsNullOrWhiteSpace(printerName))
                {
                    MessageBox.Show("Nome stampante non configurato nel servizio SignalR. Impossibile stampare.", "Errore Stampa", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    AddLog($"ERRORE: Tentativo di stampa job {jobToPrint.JobId} fallito. Nome stampante non configurato.");
                    if (btn != null) 
                    {
                        btn.Enabled = _signalRService.GetOnDemandQueueCount() > 0; // Re-enable if queue still has items
                    }
                    return;
                }

                AddLog($"Stampa Job ID: {jobToPrint.JobId} su stampante: {printerName}...");
                try
                {
                    // The rawData is already byte[]
                    bool success = await _rawPrinter.PrintRawAsync(printerName, jobToPrint.RawData);
                    if (success)
                    {
                        AddLog($"Job ID: {jobToPrint.JobId} stampato con successo.");
                    }
                    else
                    {
                        AddLog($"ERRORE: Stampa Job ID: {jobToPrint.JobId} fallita. Controllare i log della stampante.");
                        MessageBox.Show($"Stampa del Job ID {jobToPrint.JobId} fallita. Controllare la stampante e i log.", "Errore Stampa", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Eccezione durante la stampa del job {jobToPrint.JobId}");
                    AddLog($"ERRORE CRITICO: Eccezione durante la stampa del Job ID {jobToPrint.JobId}: {ex.Message}");
                    MessageBox.Show($"Errore critico durante la stampa del Job ID {jobToPrint.JobId}: {ex.Message}", "Errore Stampa Critico", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
                AddLog("Nessuna comanda in attesa.");
                MessageBox.Show("Nessuna comanda in attesa da stampare.", "Coda Vuota", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            
            if (btn != null) 
            {
                btn.Enabled = _signalRService.GetOnDemandQueueCount() > 0; // Re-enable button based on new queue count
            }
        }

        private void AddLog(string message)
        {
            // Assuming a TextBox named txtActivityLog (multiline) exists
            if (Controls.Find("txtActivityLog", true).FirstOrDefault() is TextBox txtActivityLog)
            {
                if (txtActivityLog.InvokeRequired)
                {
                    txtActivityLog.Invoke(new Action(() => PrependLogMessage(txtActivityLog, message)));
                }
                else
                {
                    PrependLogMessage(txtActivityLog, message);
                }
            }
            _logger.LogInformation(message); // Also log to the main logger
        }

        private void PrependLogMessage(TextBox textBox, string message)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            textBox.Text = $"{timestamp}: {message}{Environment.NewLine}{textBox.Text}";
            if (textBox.Text.Length > 10000) // Keep log from growing too large
            {
                textBox.Text = textBox.Text.Substring(0, 10000);
            }
        }

        // It's good practice to unsubscribe from events when the form is disposed
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _signalRService.OnDemandQueueCountChanged -= SignalRService_OnDemandQueueCountChanged;
                if (components != null)
                {
                    components.Dispose();
                }
            }
            base.Dispose(disposing);
        }
    }
}
