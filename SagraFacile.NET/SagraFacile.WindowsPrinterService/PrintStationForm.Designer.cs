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
            this.lblQueueCount = new System.Windows.Forms.Label();
            this.btnPrintNext = new System.Windows.Forms.Button();
            this.txtActivityLog = new System.Windows.Forms.TextBox();
            this.SuspendLayout();
            // 
            // lblQueueCount
            // 
            this.lblQueueCount.AutoSize = true;
            this.lblQueueCount.Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.lblQueueCount.Location = new System.Drawing.Point(12, 20);
            this.lblQueueCount.Name = "lblQueueCount";
            this.lblQueueCount.Size = new System.Drawing.Size(198, 28);
            this.lblQueueCount.TabIndex = 0;
            this.lblQueueCount.Text = "Comande in Attesa: 0";
            // 
            // btnPrintNext
            // 
            this.btnPrintNext.Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.btnPrintNext.Location = new System.Drawing.Point(12, 60);
            this.btnPrintNext.Name = "btnPrintNext";
            this.btnPrintNext.Size = new System.Drawing.Size(360, 50);
            this.btnPrintNext.TabIndex = 1;
            this.btnPrintNext.Text = "STAMPA PROSSIMA COMANDA";
            this.btnPrintNext.UseVisualStyleBackColor = true;
            this.btnPrintNext.Click += new System.EventHandler(this.BtnPrintNext_Click);
            // 
            // txtActivityLog
            // 
            this.txtActivityLog.Location = new System.Drawing.Point(12, 130);
            this.txtActivityLog.Multiline = true;
            this.txtActivityLog.Name = "txtActivityLog";
            this.txtActivityLog.ReadOnly = true;
            this.txtActivityLog.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtActivityLog.Size = new System.Drawing.Size(360, 180);
            this.txtActivityLog.TabIndex = 2;
            // 
            // PrintStationForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 20F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(384, 321);
            this.Controls.Add(this.txtActivityLog);
            this.Controls.Add(this.btnPrintNext);
            this.Controls.Add(this.lblQueueCount);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = true; // Allow minimizing
            this.Name = "PrintStationForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Stazione Stampa Comande";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label lblQueueCount;
        private System.Windows.Forms.Button btnPrintNext;
        private System.Windows.Forms.TextBox txtActivityLog;
    }
}
