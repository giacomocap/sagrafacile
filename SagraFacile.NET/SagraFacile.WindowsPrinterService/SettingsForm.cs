using SagraFacile.WindowsPrinterService.Models;
using System;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;
using System.Linq;
using System.Threading;
using System.ComponentModel;
using SagraFacile.WindowsPrinterService.Services;
using SagraFacile.WindowsPrinterService.Utils; // For StartupManager

namespace SagraFacile.WindowsPrinterService
{
    public partial class SettingsForm : Form
    {
        private readonly string _profilesDirectory;
        private string? _currentProfileName; 
        private ProfileSettings _currentSettings;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public SignalRService? SignalRServiceInstance { get; set; }

        public SettingsForm(string? profileName, string profilesDirectory)
        {
            InitializeComponent();
            _profilesDirectory = profilesDirectory ?? throw new ArgumentNullException(nameof(profilesDirectory));
            _currentProfileName = profileName;

            // Ensure profiles directory exists (it should, but good practice)
            Directory.CreateDirectory(_profilesDirectory);

            PopulatePrinters();
            PopulatePrinterTypeComboBox();
            _currentSettings = LoadProfileSettings(_currentProfileName);
            PopulateControlsFromSettings();

            if (string.IsNullOrEmpty(profileName))
            {
                this.Text = "Crea Nuovo Profilo Stampante";
            }
            else
            {
                this.Text = $"Modifica Profilo: {profileName}";
            }

            buttonSave.Click += ButtonSave_Click;
            buttonCancel.Click += ButtonCancel_Click;

            if (this.Controls.Find("btnTestPrinter", true).FirstOrDefault() is Button btnTest)
            {
                btnTest.Click += BtnTestPrinter_Click;
            }
            this.Load += SettingsForm_Load;
            this.comboBoxPrinters.SelectedIndexChanged += new System.EventHandler(this.comboBoxPrinters_SelectedIndexChanged);
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

        private void PopulatePrinterTypeComboBox()
        {
            cboPrinterType.DataSource = Enum.GetValues(typeof(LocalPrinterType))
                .Cast<LocalPrinterType>()
                .Select(p => new { Name = p.ToString(), Value = p })
                .ToList();
            cboPrinterType.DisplayMember = "Name";
            cboPrinterType.ValueMember = "Value";
        }

        private void comboBoxPrinters_SelectedIndexChanged(object? sender, EventArgs e)
        {
            PopulatePaperSizes();
        }

        private void PopulatePaperSizes()
        {
            string? selectedPrinterName = comboBoxPrinters.SelectedItem?.ToString();
            var cboPaperSize = this.Controls.Find("cboPaperSize", true).FirstOrDefault() as ComboBox;

            if (string.IsNullOrEmpty(selectedPrinterName) || cboPaperSize == null)
            {
                cboPaperSize?.Items.Clear();
                return;
            }

            string? currentSelection = cboPaperSize.SelectedItem?.ToString();
            cboPaperSize.Items.Clear();
            try
            {
                PrintDocument pd = new PrintDocument();
                pd.PrinterSettings.PrinterName = selectedPrinterName;
                foreach (PaperSize ps in pd.PrinterSettings.PaperSizes)
                {
                    cboPaperSize.Items.Add(ps.PaperName);
                }

                if (cboPaperSize.Items.Contains(currentSelection))
                {
                    cboPaperSize.SelectedItem = currentSelection;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not get paper sizes for printer '{selectedPrinterName}': {ex.Message}");
            }
        }

        private void PopulateControlsFromSettings()
        {
            // Populate profile name
            txtProfileName.Text = _currentSettings.ProfileName ?? string.Empty;

            if (!string.IsNullOrEmpty(_currentSettings.SelectedPrinter) && comboBoxPrinters.Items.Contains(_currentSettings.SelectedPrinter))
            {
                comboBoxPrinters.SelectedItem = _currentSettings.SelectedPrinter;
            }
            else if (comboBoxPrinters.Items.Count > 0)
            {
                var firstRealPrinter = comboBoxPrinters.Items.Cast<string>().FirstOrDefault(p => p != "Microsoft Print to PDF");
                if (firstRealPrinter != null)
                {
                    comboBoxPrinters.SelectedItem = firstRealPrinter;
                }
                else
                {
                    comboBoxPrinters.SelectedIndex = 0;
                }
            }

            if (this.Controls.Find("txtHubUrl", true).FirstOrDefault() is TextBox txtHubHost) txtHubHost.Text = _currentSettings.HubHostAndPort ?? string.Empty;
            if (this.Controls.Find("txtInstanceGuid", true).FirstOrDefault() is TextBox txtInstanceGuid) txtInstanceGuid.Text = _currentSettings.InstanceGuid ?? string.Empty;
            
            // Assuming chkAutoStart is the name of the CheckBox added in the designer
            if (this.Controls.Find("chkAutoStart", true).FirstOrDefault() is CheckBox chkAutoStartCtrl)
            {
                chkAutoStartCtrl.Checked = _currentSettings.AutoStartEnabled;
            }

            // Populate printer type
            cboPrinterType.SelectedValue = _currentSettings.PrinterType;

            // Populate paper sizes based on selected printer and set saved value
            PopulatePaperSizes();
            var cboPaperSize = this.Controls.Find("cboPaperSize", true).FirstOrDefault() as ComboBox;
            if (cboPaperSize != null && !string.IsNullOrEmpty(_currentSettings.PaperSize) && cboPaperSize.Items.Contains(_currentSettings.PaperSize))
            {
                cboPaperSize.SelectedItem = _currentSettings.PaperSize;
            }
            else if (cboPaperSize != null && cboPaperSize.Items.Count > 0)
            {
                cboPaperSize.SelectedIndex = 0; // Select the first item if no saved size or saved size not found
            }
        }

        private ProfileSettings LoadProfileSettings(string? profileName)
        {
            if (string.IsNullOrEmpty(profileName))
            {
                return new ProfileSettings(); // Return new settings for a new profile
            }

            string profilePath = Path.Combine(_profilesDirectory, $"{profileName}.json");
            try
            {
                if (File.Exists(profilePath))
                {
                    string json = File.ReadAllText(profilePath);
                    var settings = JsonSerializer.Deserialize<ProfileSettings>(json);
                    if (settings != null)
                    {
                        settings.ProfileName = profileName; // Ensure ProfileName is consistent
                        return settings;
                    }
                     MessageBox.Show($"Impossibile deserializzare il profilo '{profileName}'. Verranno usati valori di default.", "Errore Profilo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading profile '{profileName}': {ex.Message}");
                MessageBox.Show($"Errore durante il caricamento del profilo '{profileName}': {ex.Message}\nVerranno usati valori di default.", "Errore Profilo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            // If file doesn't exist or error, return new settings for the named profile
            return new ProfileSettings { ProfileName = profileName };
        }

        private bool SaveProfileSettings()
        {
            // Get profile name from the text field
            string? profileNameToSave = txtProfileName.Text?.Trim();

            if (string.IsNullOrWhiteSpace(profileNameToSave))
            {
                MessageBox.Show("Il nome del profilo non può essere vuoto.", "Nome Profilo Richiesto", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtProfileName.Focus();
                return false;
            }

            // Clean the profile name for use as filename
            profileNameToSave = string.Join("_", profileNameToSave.Split(Path.GetInvalidFileNameChars()));

            // Check if we're creating a new profile or editing an existing one
            bool isNewProfile = string.IsNullOrEmpty(_currentProfileName);
            bool isRenamingProfile = !isNewProfile && !string.Equals(_currentProfileName, profileNameToSave, StringComparison.OrdinalIgnoreCase);

            // Check for duplicate profile names (only if creating new or renaming)
            if (isNewProfile || isRenamingProfile)
            {
                string existingProfilePath = Path.Combine(_profilesDirectory, $"{profileNameToSave}.json");
                if (File.Exists(existingProfilePath))
                {
                    MessageBox.Show($"Un profilo con nome '{profileNameToSave}' esiste già. Scegli un nome diverso.", "Nome Profilo Duplicato", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    txtProfileName.Focus();
                    return false;
                }
            }

            string? hubHostAndPort = null, instanceGuid = null;
            if (this.Controls.Find("txtHubUrl", true).FirstOrDefault() is TextBox txtHubHostCtrl) hubHostAndPort = txtHubHostCtrl.Text;
            if (this.Controls.Find("txtInstanceGuid", true).FirstOrDefault() is TextBox txtInstanceGuidCtrl) instanceGuid = txtInstanceGuidCtrl.Text;

            var settingsToSave = new ProfileSettings
            {
                ProfileName = profileNameToSave,
                SelectedPrinter = comboBoxPrinters.SelectedItem?.ToString(),
                HubHostAndPort = hubHostAndPort,
                InstanceGuid = instanceGuid,
                // Assuming chkAutoStart is the name of the CheckBox added in the designer
                AutoStartEnabled = (this.Controls.Find("chkAutoStart", true).FirstOrDefault() is CheckBox chkAutoStartCtrl) && chkAutoStartCtrl.Checked,
                PrinterType = (LocalPrinterType)(cboPrinterType.SelectedValue ?? LocalPrinterType.Standard),
                PaperSize = (this.Controls.Find("cboPaperSize", true).FirstOrDefault() as ComboBox)?.SelectedItem?.ToString()
            };

            string profilePath = Path.Combine(_profilesDirectory, $"{profileNameToSave}.json");
            try
            {
                // If we're renaming a profile, delete the old file
                if (isRenamingProfile && !string.IsNullOrEmpty(_currentProfileName))
                {
                    string oldProfilePath = Path.Combine(_profilesDirectory, $"{_currentProfileName}.json");
                    if (File.Exists(oldProfilePath))
                    {
                        File.Delete(oldProfilePath);
                    }
                }

                string json = JsonSerializer.Serialize(settingsToSave, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(profilePath, json);
                _currentSettings = settingsToSave; 
                _currentProfileName = profileNameToSave; 
                this.Text = $"Modifica Profilo: {_currentProfileName}"; // Update title if it was a new profile

                // Manage Windows Startup entry
                if (!string.IsNullOrEmpty(settingsToSave.ProfileName) && !string.IsNullOrEmpty(settingsToSave.InstanceGuid))
                {
                    StartupManager.SetAutoStart(settingsToSave, settingsToSave.AutoStartEnabled);
                }
                else
                {
                    // Log or handle error: ProfileName or InstanceGuid is missing, cannot set autostart
                    Console.WriteLine("Warning: ProfileName or InstanceGuid is missing. Cannot manage autostart entry.");
                }
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Errore durante il salvataggio del profilo '{profileNameToSave}': {ex.Message}", "Errore Salvataggio", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        private async void ButtonSave_Click(object? sender, EventArgs e)
        {
            // Validate profile name
            if (string.IsNullOrWhiteSpace(txtProfileName.Text))
            {
                MessageBox.Show("Il nome del profilo non può essere vuoto.", "Errore Validazione", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtProfileName.Focus();
                return;
            }

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

            if (!SaveProfileSettings())
            {
                return; 
            }

            if (SignalRServiceInstance != null && SignalRServiceInstance.CurrentProfileName == _currentProfileName)
            {
                MessageBox.Show($"Impostazioni per il profilo '{_currentProfileName}' salvate. Riavvio del servizio di connessione in corso...", "Salvataggio Profilo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                await SignalRServiceInstance.RestartAsync(CancellationToken.None);
            }
            else
            {
                 MessageBox.Show($"Impostazioni per il profilo '{_currentProfileName}' salvate.", "Salvataggio Profilo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void ButtonCancel_Click(object? sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
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

            string testData = $"Test di stampa Sagrafacile!\r\nProfilo: {_currentProfileName}\r\nStampante: {selectedPrinter}\r\nOra: {DateTime.Now}\r\n\r\n------------------------\r\n";
            testData += "Riga 1\r\n";
            testData += "Riga 2\r\n";
            testData += "Riga 3 - Caratteri speciali: àèìòù €!?\r\n";
            testData += "------------------------\r\n\r\n\r\n\r\n"; 
            testData += (char)0x1D;
            testData += 'V';
            testData += (char)0;

            try
            {
                var printerType = (LocalPrinterType)(cboPrinterType.SelectedValue ?? LocalPrinterType.Standard);
                var cboPaperSize = this.Controls.Find("cboPaperSize", true).FirstOrDefault() as ComboBox;
                string? paperSize = cboPaperSize?.SelectedItem?.ToString();
                bool success = await SignalRServiceInstance.TestPrintAsync(selectedPrinter, testData, printerType, paperSize);
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
            if (this.IsDisposed || !this.IsHandleCreated)
            {
                return;
            }

            if (lblStatus == null || lblStatus.IsDisposed)
            {
                return;
            }

            if (lblStatus.InvokeRequired)
            {
                try
                {
                    this.BeginInvoke(new Action(() =>
                    {
                        if (!this.IsDisposed && this.IsHandleCreated && lblStatus != null && !lblStatus.IsDisposed && lblStatus.IsHandleCreated)
                        {
                            lblStatus.Text = $"Stato: {statusMessage}";
                            lblStatus.ForeColor = statusColor;
                        }
                    }));
                }
                catch (Exception ex) when (ex is ObjectDisposedException || ex is InvalidOperationException)
                {
                    // Console.WriteLine($"Exception during BeginInvoke in UpdateConnectionStatus: {ex.Message}");
                }
            }
            else 
            {
                if (!lblStatus.IsDisposed && lblStatus.IsHandleCreated)
                {
                    lblStatus.Text = $"Stato: {statusMessage}";
                    lblStatus.ForeColor = statusColor;
                }
            }
        }
    }
}
