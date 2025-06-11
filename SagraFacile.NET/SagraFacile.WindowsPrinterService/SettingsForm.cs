using System;
using System.Drawing; // Required for Color
using System.Drawing.Printing; // Required for PrinterSettings
using System.IO; // Required for File operations
using System.Text.Json; // Required for JSON serialization
using System.Windows.Forms;
using System.Linq;
using System.Threading; // Required for CancellationToken
using System.ComponentModel; // Required for Browsable and DesignerSerializationVisibility attributes
using SagraFacile.WindowsPrinterService.Services; // Added for SignalRService

namespace SagraFacile.WindowsPrinterService
{
    public partial class SettingsForm : Form
    {
        private class AppSettings
        {
            public string? SelectedPrinter { get; set; }
            public string? HubHostAndPort { get; set; }
            public string? InstanceGuid { get; set; }
        }

        private readonly string _settingsFilePath;
        private AppSettings _currentSettings;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public SignalRService? SignalRServiceInstance { get; set; }


        public SettingsForm()
        {
            InitializeComponent();

            string appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SagraFacilePrinterService");
            Directory.CreateDirectory(appDataFolder);
            _settingsFilePath = Path.Combine(appDataFolder, "settings.json");

            _currentSettings = LoadSettingsFromFile();
            PopulatePrinters();
            PopulateControlsFromSettings();

            // Ensure btnGenerateGuid click handler is assigned if the control exists
            if (this.Controls.Find("btnGenerateGuid", true).FirstOrDefault() is Button btnGenGuid)
            {
                btnGenGuid.Click += BtnGenerateGuid_Click;
            }

            buttonSave.Click += ButtonSave_Click;
            buttonCancel.Click += ButtonCancel_Click;

            // Wire up Test Printer button if it exists
            if (this.Controls.Find("btnTestPrinter", true).FirstOrDefault() is Button btnTest)
            {
                btnTest.Click += BtnTestPrinter_Click;
            }

            // Wire up the Load event
            this.Load += SettingsForm_Load;
        }

        private void SettingsForm_Load(object? sender, EventArgs e)
        {
            if (SignalRServiceInstance != null)
            {
                var (statusMessage, statusColor) = SignalRServiceInstance.GetCurrentStatus();
                UpdateConnectionStatus(statusMessage, statusColor);
            }
        }

        private void PopulatePrinters()
        {
            comboBoxPrinters.Items.Clear();
            comboBoxPrinters.Items.Add("Microsoft Print to PDF");
            foreach (string printer in PrinterSettings.InstalledPrinters)
            {
                comboBoxPrinters.Items.Add(printer);
            }
        }

        private void PopulateControlsFromSettings()
        {
            if (!string.IsNullOrEmpty(_currentSettings.SelectedPrinter) && comboBoxPrinters.Items.Contains(_currentSettings.SelectedPrinter))
            {
                comboBoxPrinters.SelectedItem = _currentSettings.SelectedPrinter;
            }
            else if (comboBoxPrinters.Items.Count > 0)
            {
                comboBoxPrinters.SelectedIndex = 0;
            }

            if (this.Controls.Find("txtHubUrl", true).FirstOrDefault() is TextBox txtHubHost) txtHubHost.Text = _currentSettings.HubHostAndPort ?? "localhost:7055";
            if (this.Controls.Find("txtInstanceGuid", true).FirstOrDefault() is TextBox txtInstanceGuid) txtInstanceGuid.Text = _currentSettings.InstanceGuid ?? string.Empty;
        }

        private AppSettings LoadSettingsFromFile()
        {
            try
            {
                if (File.Exists(_settingsFilePath))
                {
                    string json = File.ReadAllText(_settingsFilePath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json);
                    return settings ?? new AppSettings();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading settings: {ex.Message}");
                MessageBox.Show($"Error loading settings: {ex.Message}\nWill use default values.", "Settings Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            return new AppSettings();
        }

        private void SaveSettingsToFile()
        {
            string? hubHostAndPort = null, instanceGuid = null;
            if (this.Controls.Find("txtHubUrl", true).FirstOrDefault() is TextBox txtHubHostCtrl) hubHostAndPort = txtHubHostCtrl.Text;
            if (this.Controls.Find("txtInstanceGuid", true).FirstOrDefault() is TextBox txtInstanceGuidCtrl) instanceGuid = txtInstanceGuidCtrl.Text;

            var settingsToSave = new AppSettings
            {
                SelectedPrinter = comboBoxPrinters.SelectedItem?.ToString(),
                HubHostAndPort = hubHostAndPort,
                InstanceGuid = instanceGuid
            };

            try
            {
                string json = JsonSerializer.Serialize(settingsToSave, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_settingsFilePath, json);
                _currentSettings = settingsToSave;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving settings: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void ButtonSave_Click(object? sender, EventArgs e)
        {
            if (this.Controls.Find("txtHubUrl", true).FirstOrDefault() is TextBox txtHubHostCtrl && string.IsNullOrWhiteSpace(txtHubHostCtrl.Text))
            {
                MessageBox.Show("L'URL Base del Server non può essere vuoto.", "Errore Validazione", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtHubHostCtrl.Focus();
                return;
            }
            if (this.Controls.Find("txtInstanceGuid", true).FirstOrDefault() is TextBox txtInstanceGuidCtrl && (!Guid.TryParse(txtInstanceGuidCtrl.Text, out _) || string.IsNullOrWhiteSpace(txtInstanceGuidCtrl.Text)))
            {
                MessageBox.Show("Il GUID Istanza deve essere un GUID valido e non può essere vuoto.", "Errore Validazione", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtInstanceGuidCtrl.Focus();
                return;
            }

            SaveSettingsToFile();

            if (SignalRServiceInstance != null)
            {
                // Consider providing a proper CancellationToken if available from the application's context
                // For now, CancellationToken.None is used for simplicity.
                // This will stop the current SignalR connection and start a new one with the new settings.
                MessageBox.Show("Impostazioni salvate. Riavvio del servizio di connessione in corso...", "Salvataggio", MessageBoxButtons.OK, MessageBoxIcon.Information);
                await SignalRServiceInstance.RestartAsync(CancellationToken.None);
            }
            else
            {
                MessageBox.Show("Impostazioni salvate. Riavviare manualmente il servizio per applicare le modifiche alla connessione.", "Salvataggio", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void ButtonCancel_Click(object? sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

        private void BtnGenerateGuid_Click(object? sender, EventArgs e)
        {
            if (this.Controls.Find("txtInstanceGuid", true).FirstOrDefault() is TextBox txtInstanceGuid)
            {
                txtInstanceGuid.Text = Guid.NewGuid().ToString();
            }
        }

        public static (string HubHostAndPort, string InstanceGuid) GetSignalRConfig()
        {
            string appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SagraFacilePrinterService");
            string settingsFilePath = Path.Combine(appDataFolder, "settings.json");
            AppSettings settings = new AppSettings();

            try
            {
                if (File.Exists(settingsFilePath))
                {
                    string json = File.ReadAllText(settingsFilePath);
                    var loadedSettings = JsonSerializer.Deserialize<AppSettings>(json);
                    if (loadedSettings != null) settings = loadedSettings;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading settings for SignalRService: {ex.Message}");
            }

            return (
                settings.HubHostAndPort ?? "localhost:7055",
                settings.InstanceGuid ?? string.Empty
            );
        }

        private async void BtnTestPrinter_Click(object? sender, EventArgs e)
        {
            string? selectedPrinter = comboBoxPrinters.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(selectedPrinter))
            {
                MessageBox.Show("Selezionare una stampante dalla lista.", "Test Stampa", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (SignalRServiceInstance == null)
            {
                MessageBox.Show("Servizio SignalR non disponibile. Impossibile eseguire test di stampa.", "Test Stampa", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string testData = $"Test di stampa Sagrafacile!\r\nStampante: {selectedPrinter}\r\nOra: {DateTime.Now}\r\n\r\n------------------------\r\n";
            // Add a few more lines for a better test
            testData += "Riga 1\r\n";
            testData += "Riga 2\r\n";
            testData += "Riga 3 - Caratteri speciali: àèìòù €!?\r\n";
            testData += "------------------------\r\n\r\n\r\n\r\n"; // Extra newlines for paper feed/cut
            testData += (char)0x1D;
            testData += 'V';
            testData += (char)0;

            try
            {
                bool success = await SignalRServiceInstance.TestPrintAsync(selectedPrinter, testData);
                if (success)
                {
                    MessageBox.Show($"Test di stampa inviato con successo a '{selectedPrinter}'.", "Test Stampa", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show($"Fallimento invio test di stampa a '{selectedPrinter}'. Controllare i log.", "Test Stampa", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Errore durante il test di stampa: {ex.Message}", "Test Stampa Errore", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void UpdateConnectionStatus(string statusMessage, Color statusColor)
        {
            // If the form is disposed, or its handle isn't created, it's unsafe to proceed.
            if (this.IsDisposed || !this.IsHandleCreated)
            {
                return;
            }

            // Check lblStatus specifically. If it's null or disposed, we can't update it.
            if (lblStatus == null || lblStatus.IsDisposed)
            {
                return;
            }

            if (lblStatus.InvokeRequired)
            {
                try
                {
                    // Use BeginInvoke for asynchronous dispatch.
                    // The delegate will execute on the UI thread.
                    this.BeginInvoke(new Action(() =>
                    {
                        // Re-check conditions on the UI thread before accessing lblStatus.
                        if (!this.IsDisposed && this.IsHandleCreated && lblStatus != null && !lblStatus.IsDisposed && lblStatus.IsHandleCreated)
                        {
                            lblStatus.Text = $"Stato: {statusMessage}";
                            lblStatus.ForeColor = statusColor;
                        }
                    }));
                }
                catch (Exception ex) when (ex is ObjectDisposedException || ex is InvalidOperationException)
                {
                    // Catch exceptions if BeginInvoke itself fails (e.g., form handle is being destroyed).
                    // Logging here can be helpful for diagnostics if issues persist.
                    // Console.WriteLine($"Exception during BeginInvoke in UpdateConnectionStatus: {ex.Message}");
                }
            }
            else // Already on the UI thread
            {
                // Direct update, but still check if lblStatus is valid and its handle created.
                if (!lblStatus.IsDisposed && lblStatus.IsHandleCreated)
                {
                    lblStatus.Text = $"Stato: {statusMessage}";
                    lblStatus.ForeColor = statusColor;
                }
            }
        }
    }
}
