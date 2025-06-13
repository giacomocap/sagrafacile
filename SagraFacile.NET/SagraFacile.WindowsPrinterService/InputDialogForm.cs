using System;
using System.Windows.Forms;
using System.ComponentModel;

namespace SagraFacile.WindowsPrinterService
{
    public partial class InputDialogForm : Form
    {
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string UserInput { get; private set; }

        public InputDialogForm(string title, string prompt)
        {
            InitializeComponent();
            this.Text = title;
            lblPrompt.Text = prompt;
            UserInput = string.Empty;
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            UserInput = txtInput.Text;
            if (string.IsNullOrWhiteSpace(UserInput))
            {
                MessageBox.Show("Il valore inserito non può essere vuoto.", "Input Richiesto", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtInput.Focus();
                return;
            }
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }
    }
}
