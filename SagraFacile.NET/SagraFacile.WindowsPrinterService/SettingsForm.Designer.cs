namespace SagraFacile.WindowsPrinterService
{
    partial class SettingsForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            lblProfileName = new Label();
            txtProfileName = new TextBox();
            label1 = new Label();
            comboBoxPrinters = new ComboBox();
            buttonSave = new Button();
            buttonCancel = new Button();
            txtHubUrl = new TextBox();
            txtInstanceGuid = new TextBox();
            label2 = new Label();
            label3 = new Label();
            groupBox1 = new GroupBox();
            lblStatus = new Label();
            btnTestPrinter = new Button();
            chkAutoStart = new CheckBox();
            lblPrinterType = new Label();
            cboPrinterType = new ComboBox();
            lblPaperSize = new Label();
            cboPaperSize = new ComboBox();
            groupBox1.SuspendLayout();
            SuspendLayout();
            // 
            // lblProfileName
            // 
            lblProfileName.AutoSize = true;
            lblProfileName.Location = new Point(22, 15);
            lblProfileName.Name = "lblProfileName";
            lblProfileName.Size = new Size(89, 15);
            lblProfileName.TabIndex = 15;
            lblProfileName.Text = "Nome Profilo";
            // 
            // txtProfileName
            // 
            txtProfileName.Location = new Point(22, 33);
            txtProfileName.Name = "txtProfileName";
            txtProfileName.Size = new Size(480, 23);
            txtProfileName.TabIndex = 0;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(22, 65);
            label1.Name = "label1";
            label1.Size = new Size(116, 15);
            label1.TabIndex = 1;
            label1.Text = "Seleziona Stampante";
            // 
            // comboBoxPrinters
            // 
            comboBoxPrinters.FormattingEnabled = true;
            comboBoxPrinters.Location = new Point(22, 83);
            comboBoxPrinters.Name = "comboBoxPrinters";
            comboBoxPrinters.Size = new Size(279, 23);
            comboBoxPrinters.TabIndex = 2;
            // 
            // buttonSave
            // 
            buttonSave.Location = new Point(22, 318);
            buttonSave.Name = "buttonSave";
            buttonSave.Size = new Size(75, 23);
            buttonSave.TabIndex = 7;
            buttonSave.Text = "Salva";
            buttonSave.UseVisualStyleBackColor = true;
            buttonSave.Click += ButtonSave_Click;
            // 
            // buttonCancel
            // 
            buttonCancel.Location = new Point(427, 318);
            buttonCancel.Name = "buttonCancel";
            buttonCancel.Size = new Size(75, 23);
            buttonCancel.TabIndex = 8;
            buttonCancel.Text = "Annulla";
            buttonCancel.UseVisualStyleBackColor = true;
            // 
            // txtHubUrl
            // 
            txtHubUrl.Location = new Point(22, 187);
            txtHubUrl.Name = "txtHubUrl";
            txtHubUrl.Size = new Size(480, 23);
            txtHubUrl.TabIndex = 3;
            txtHubUrl.Text = "es: https://tuoserver.com:7075 o http://localhost:5000";
            // 
            // txtInstanceGuid
            // 
            txtInstanceGuid.Location = new Point(22, 238);
            txtInstanceGuid.Name = "txtInstanceGuid";
            txtInstanceGuid.Size = new Size(429, 23);
            txtInstanceGuid.TabIndex = 4;
            txtInstanceGuid.Text = "GUID (copialo dall'Admin SagraFacile)";
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(23, 169);
            label2.Name = "label2";
            label2.Size = new Size(90, 15);
            label2.TabIndex = 9;
            label2.Text = "URL Base Server";
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new Point(23, 220);
            label3.Name = "label3";
            label3.Size = new Size(73, 15);
            label3.TabIndex = 10;
            label3.Text = "GUID Istanza";
            // 
            // groupBox1
            // 
            groupBox1.Controls.Add(lblStatus);
            groupBox1.Location = new Point(22, 357);
            groupBox1.Name = "groupBox1";
            groupBox1.Size = new Size(480, 70);
            groupBox1.TabIndex = 11;
            groupBox1.TabStop = false;
            groupBox1.Text = "Stato Connessione al Server";
            // 
            // lblStatus
            // 
            lblStatus.AutoSize = true;
            lblStatus.Location = new Point(15, 30);
            lblStatus.Name = "lblStatus";
            lblStatus.Size = new Size(96, 15);
            lblStatus.TabIndex = 0;
            lblStatus.Text = "Stato: Sconnesso";
            // 
            // btnTestPrinter
            // 
            btnTestPrinter.Location = new Point(307, 82);
            btnTestPrinter.Name = "btnTestPrinter";
            btnTestPrinter.Size = new Size(195, 24);
            btnTestPrinter.TabIndex = 13;
            btnTestPrinter.Text = "Test Stampante";
            btnTestPrinter.UseVisualStyleBackColor = true;
            // 
            // chkAutoStart
            // 
            chkAutoStart.AutoSize = true;
            chkAutoStart.Location = new Point(22, 281);
            chkAutoStart.Name = "chkAutoStart";
            chkAutoStart.Size = new Size(243, 19);
            chkAutoStart.TabIndex = 6;
            chkAutoStart.Text = "Avvia questo profilo all'avvio di Windows";
            chkAutoStart.UseVisualStyleBackColor = true;
            // 
            // lblPrinterType
            // 
            lblPrinterType.AutoSize = true;
            lblPrinterType.Location = new Point(23, 118);
            lblPrinterType.Name = "lblPrinterType";
            lblPrinterType.Size = new Size(92, 15);
            lblPrinterType.TabIndex = 16;
            lblPrinterType.Text = "Tipo Stampante";
            // 
            // cboPrinterType
            // 
            cboPrinterType.DropDownStyle = ComboBoxStyle.DropDownList;
            cboPrinterType.FormattingEnabled = true;
            cboPrinterType.Location = new Point(22, 136);
            cboPrinterType.Name = "cboPrinterType";
            cboPrinterType.Size = new Size(279, 23);
            cboPrinterType.TabIndex = 17;
            // 
            // lblPaperSize
            // 
            lblPaperSize.AutoSize = true;
            lblPaperSize.Location = new Point(307, 118);
            lblPaperSize.Name = "lblPaperSize";
            lblPaperSize.Size = new Size(79, 15);
            lblPaperSize.TabIndex = 18;
            lblPaperSize.Text = "Formato Carta";
            // 
            // cboPaperSize
            // 
            cboPaperSize.DropDownStyle = ComboBoxStyle.DropDownList;
            cboPaperSize.FormattingEnabled = true;
            cboPaperSize.Location = new Point(307, 136);
            cboPaperSize.Name = "cboPaperSize";
            cboPaperSize.Size = new Size(195, 23);
            cboPaperSize.TabIndex = 19;
            // 
            // SettingsForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(531, 439);
            Controls.Add(cboPaperSize);
            Controls.Add(lblPaperSize);
            Controls.Add(cboPrinterType);
            Controls.Add(lblPrinterType);
            Controls.Add(lblProfileName);
            Controls.Add(txtProfileName);
            Controls.Add(chkAutoStart);
            Controls.Add(btnTestPrinter);
            Controls.Add(groupBox1);
            Controls.Add(label3);
            Controls.Add(label2);
            Controls.Add(txtInstanceGuid);
            Controls.Add(txtHubUrl);
            Controls.Add(buttonCancel);
            Controls.Add(buttonSave);
            Controls.Add(comboBoxPrinters);
            Controls.Add(label1);
            Name = "SettingsForm";
            Text = "Sagrafacile App di Stampa - Impostazioni e Stato";
            groupBox1.ResumeLayout(false);
            groupBox1.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Label lblProfileName;
        private TextBox txtProfileName;
        private Label label1;
        private ComboBox comboBoxPrinters;
        private Button buttonSave;
        private Button buttonCancel;
        private TextBox txtHubUrl;
        private TextBox txtInstanceGuid;
        private Label label2;
        private Label label3;
        private GroupBox groupBox1;
        private Label lblStatus;
        private Button btnTestPrinter;
        private CheckBox chkAutoStart;
        private Label lblPrinterType;
        private ComboBox cboPrinterType;
        private Label lblPaperSize;
        private ComboBox cboPaperSize;
    }
}
