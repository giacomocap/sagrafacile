using SagraFacile.WindowsPrinterService.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;
using System.ComponentModel;

namespace SagraFacile.WindowsPrinterService
{
    public partial class ProfileSelectionForm : Form
    {
        private readonly string _profilesDirectory;
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public ProfileSettings? SelectedProfileSettings { get; private set; }

        public ProfileSelectionForm()
        {
            InitializeComponent();
            string appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SagraFacilePrinterService");
            _profilesDirectory = Path.Combine(appDataFolder, "profiles");
            Directory.CreateDirectory(_profilesDirectory); // Ensure profiles directory exists
        }

        private void ProfileSelectionForm_Load(object sender, EventArgs e)
        {
            LoadProfilesToListBox();
            UpdateProfileSpecificButtonStates();
        }

        private void LoadProfilesToListBox()
        {
            lstProfiles.Items.Clear();
            if (Directory.Exists(_profilesDirectory))
            {
                var profileFiles = Directory.GetFiles(_profilesDirectory, "*.json")
                                            .Select(Path.GetFileNameWithoutExtension)
                                            .Where(name => !string.IsNullOrWhiteSpace(name)) // Ensure not null or empty
                                            .OrderBy(name => name);
                
                foreach (var profileName in profileFiles)
                {
                    lstProfiles.Items.Add(profileName!); // Add non-null profile name
                }

                if (lstProfiles.Items.Count > 0)
                {
                    lstProfiles.SelectedIndex = 0;
                }
            }
            UpdateProfileSpecificButtonStates(); // Update button states after loading
        }

        private void UpdateProfileSpecificButtonStates()
        {
            bool profileSelected = lstProfiles.SelectedItem != null;
            btnLoadProfile.Enabled = profileSelected;
            btnEditProfile.Enabled = profileSelected;
            btnDeleteProfile.Enabled = profileSelected;
        }

        private void lstProfiles_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateProfileSpecificButtonStates();
        }

        private void btnLoadProfile_Click(object sender, EventArgs e)
        {
            if (lstProfiles.SelectedItem == null)
            {
                MessageBox.Show("Selezionare un profilo da caricare.", "Nessun Profilo Selezionato", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string profileName = lstProfiles.SelectedItem.ToString()!;
            string profilePath = Path.Combine(_profilesDirectory, $"{profileName}.json");

            try
            {
                if (File.Exists(profilePath))
                {
                    string json = File.ReadAllText(profilePath);
                    SelectedProfileSettings = JsonSerializer.Deserialize<ProfileSettings>(json);
                    if (SelectedProfileSettings != null)
                    {
                        SelectedProfileSettings.ProfileName = profileName; // Ensure ProfileName is set
                        this.DialogResult = DialogResult.OK;
                        this.Close();
                    }
                    else
                    {
                        MessageBox.Show($"Impossibile deserializzare il profilo '{profileName}'. Il file potrebbe essere corrotto.", "Errore Caricamento Profilo", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                else
                {
                    MessageBox.Show($"File del profilo '{profileName}' non trovato.", "Errore Caricamento Profilo", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Errore durante il caricamento del profilo '{profileName}': {ex.Message}", "Errore Caricamento Profilo", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnCreateProfile_Click(object sender, EventArgs e)
        {
            using (var settingsForm = new SettingsForm(null, _profilesDirectory)) // Pass null for new profile
            {
                if (settingsForm.ShowDialog(this) == DialogResult.OK)
                {
                    LoadProfilesToListBox(); // Refresh list
                }
            }
        }

        private void btnEditProfile_Click(object sender, EventArgs e)
        {
            if (lstProfiles.SelectedItem == null)
            {
                MessageBox.Show("Selezionare un profilo da modificare.", "Nessun Profilo Selezionato", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            string profileName = lstProfiles.SelectedItem.ToString()!;
            
            using (var settingsForm = new SettingsForm(profileName, _profilesDirectory))
            {
                if (settingsForm.ShowDialog(this) == DialogResult.OK)
                {
                    LoadProfilesToListBox(); // Refresh list
                }
            }
        }

        private void btnDeleteProfile_Click(object sender, EventArgs e)
        {
            if (lstProfiles.SelectedItem == null)
            {
                MessageBox.Show("Selezionare un profilo da eliminare.", "Nessun Profilo Selezionato", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string profileName = lstProfiles.SelectedItem.ToString()!;
            var confirmResult = MessageBox.Show($"Sei sicuro di voler eliminare il profilo '{profileName}'?",
                                                 "Conferma Eliminazione",
                                                 MessageBoxButtons.YesNo,
                                                 MessageBoxIcon.Warning);

            if (confirmResult == DialogResult.Yes)
            {
                string profilePath = Path.Combine(_profilesDirectory, $"{profileName}.json");
                try
                {
                    if (File.Exists(profilePath))
                    {
                        File.Delete(profilePath);
                        MessageBox.Show($"Profilo '{profileName}' eliminato con successo.", "Eliminazione Riuscita", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        LoadProfilesToListBox(); // Refresh list
                    }
                    else
                    {
                        MessageBox.Show($"File del profilo '{profileName}' non trovato.", "Errore Eliminazione", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Errore durante l'eliminazione del profilo '{profileName}': {ex.Message}", "Errore Eliminazione", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void btnExit_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel; // Or Abort, depending on how Program.cs handles it
            this.Close();
        }
    }
}
