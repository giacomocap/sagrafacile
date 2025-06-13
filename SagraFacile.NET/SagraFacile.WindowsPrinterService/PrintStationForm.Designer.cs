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
            this.lblPendingCount = new System.Windows.Forms.Label();
            this.btnPrintNext = new System.Windows.Forms.Button();
            this.txtActivityLog = new System.Windows.Forms.TextBox();
            this.lblConnectionStatus = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // lblPendingCount
            // 
            this.lblPendingCount.AutoSize = true;
            this.lblPendingCount.Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.lblPendingCount.Location = new System.Drawing.Point(12, 9);
            this.lblPendingCount.Name = "lblPendingCount";
            this.lblPendingCount.Size = new System.Drawing.Size(198, 21);
            this.lblPendingCount.TabIndex = 0;
            this.lblPendingCount.Text = "Comande in Attesa: 0";
            // 
            // btnPrintNext
            // 
            this.btnPrintNext.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.btnPrintNext.Font = new System.Drawing.Font("Segoe UI", 14F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.btnPrintNext.Location = new System.Drawing.Point(12, 40);
            this.btnPrintNext.Name = "btnPrintNext";
            this.btnPrintNext.Size = new System.Drawing.Size(360, 60);
            this.btnPrintNext.TabIndex = 1;
            this.btnPrintNext.Text = "STAMPA PROSSIMA COMANDA";
            this.btnPrintNext.UseVisualStyleBackColor = true;
            this.btnPrintNext.Click += new System.EventHandler(this.btnPrintNext_Click);
            // 
            // txtActivityLog
            // 
            this.txtActivityLog.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtActivityLog.Location = new System.Drawing.Point(12, 106);
            this.txtActivityLog.Multiline = true;
            this.txtActivityLog.Name = "txtActivityLog";
            this.txtActivityLog.ReadOnly = true;
            this.txtActivityLog.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtActivityLog.Size = new System.Drawing.Size(360, 193);
            this.txtActivityLog.TabIndex = 2;
            // 
            // lblConnectionStatus
            // 
            this.lblConnectionStatus.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.lblConnectionStatus.Location = new System.Drawing.Point(12, 302);
            this.lblConnectionStatus.Name = "lblConnectionStatus";
            this.lblConnectionStatus.Size = new System.Drawing.Size(360, 15);
            this.lblConnectionStatus.TabIndex = 3;
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
            this.MinimumSize = new System.Drawing.Size(400, 365);
            this.Name = "PrintStationForm";
            this.Text = "Stazione Stampa Comande";
            this.Load += new System.EventHandler(this.PrintStationForm_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label lblPendingCount;
        private System.Windows.Forms.Button btnPrintNext;
        private System.Windows.Forms.TextBox txtActivityLog;
        private System.Windows.Forms.Label lblConnectionStatus;
    }
}
