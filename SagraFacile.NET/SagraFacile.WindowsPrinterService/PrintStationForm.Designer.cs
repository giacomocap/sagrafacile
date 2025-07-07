namespace SagraFacile.WindowsPrinterService
{
    partial class PrintStationForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.lblProfileName = new System.Windows.Forms.Label();
            this.lblPrinterName = new System.Windows.Forms.Label();
            this.lblPendingCount = new System.Windows.Forms.Label();
            this.btnPrintNext = new System.Windows.Forms.Button();
            this.txtActivityLog = new System.Windows.Forms.TextBox();
            this.lblConnectionStatus = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // lblProfileName
            // 
            this.lblProfileName.AutoSize = true;
            this.lblProfileName.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.lblProfileName.Location = new System.Drawing.Point(12, 9);
            this.lblProfileName.Name = "lblProfileName";
            this.lblProfileName.Size = new System.Drawing.Size(80, 15);
            this.lblProfileName.TabIndex = 0;
            this.lblProfileName.Text = "Profilo: N/D";
            // 
            // lblPrinterName
            // 
            this.lblPrinterName.AutoSize = true;
            this.lblPrinterName.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.lblPrinterName.Location = new System.Drawing.Point(12, 30);
            this.lblPrinterName.Name = "lblPrinterName";
            this.lblPrinterName.Size = new System.Drawing.Size(95, 15);
            this.lblPrinterName.TabIndex = 1;
            this.lblPrinterName.Text = "Stampante: N/D";
            // 
            // lblPendingCount
            // 
            this.lblPendingCount.AutoSize = true;
            this.lblPendingCount.Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.lblPendingCount.Location = new System.Drawing.Point(12, 55);
            this.lblPendingCount.Name = "lblPendingCount";
            this.lblPendingCount.Size = new System.Drawing.Size(198, 21);
            this.lblPendingCount.TabIndex = 2;
            this.lblPendingCount.Text = "Comande in Attesa: 0";
            // 
            // btnPrintNext
            // 
            this.btnPrintNext.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.btnPrintNext.Font = new System.Drawing.Font("Segoe UI", 14F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.btnPrintNext.Location = new System.Drawing.Point(12, 85);
            this.btnPrintNext.Name = "btnPrintNext";
            this.btnPrintNext.Size = new System.Drawing.Size(360, 60);
            this.btnPrintNext.TabIndex = 3;
            this.btnPrintNext.Text = "STAMPA PROSSIMA COMANDA";
            this.btnPrintNext.UseVisualStyleBackColor = true;
            this.btnPrintNext.Click += new System.EventHandler(this.btnPrintNext_Click);
            // 
            // txtActivityLog
            // 
            this.txtActivityLog.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtActivityLog.Location = new System.Drawing.Point(12, 155);
            this.txtActivityLog.Multiline = true;
            this.txtActivityLog.Name = "txtActivityLog";
            this.txtActivityLog.ReadOnly = true;
            this.txtActivityLog.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtActivityLog.Size = new System.Drawing.Size(360, 144);
            this.txtActivityLog.TabIndex = 4;
            // 
            // lblConnectionStatus
            // 
            this.lblConnectionStatus.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.lblConnectionStatus.Location = new System.Drawing.Point(12, 302);
            this.lblConnectionStatus.Name = "lblConnectionStatus";
            this.lblConnectionStatus.Size = new System.Drawing.Size(360, 15);
            this.lblConnectionStatus.TabIndex = 5;
            this.lblConnectionStatus.Text = "Stato Connessione: Inizializzazione...";
            this.lblConnectionStatus.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // PrintStationForm
            // 
            this.AcceptButton = this.btnPrintNext;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(384, 326);
            this.Controls.Add(this.lblConnectionStatus);
            this.Controls.Add(this.txtActivityLog);
            this.Controls.Add(this.btnPrintNext);
            this.Controls.Add(this.lblPendingCount);
            this.Controls.Add(this.lblPrinterName);
            this.Controls.Add(this.lblProfileName);
            this.MinimumSize = new System.Drawing.Size(400, 365);
            this.Name = "PrintStationForm";
            this.Text = "Stazione Stampa Comande";
            this.Load += new System.EventHandler(this.PrintStationForm_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label lblProfileName;
        private System.Windows.Forms.Label lblPrinterName;
        private System.Windows.Forms.Label lblPendingCount;
        private System.Windows.Forms.Button btnPrintNext;
        private System.Windows.Forms.TextBox txtActivityLog;
        private System.Windows.Forms.Label lblConnectionStatus;
    }
}
