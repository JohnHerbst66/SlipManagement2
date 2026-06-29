namespace SlipManagement2
{
    partial class PrinterSettingsForm
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
            this.label1 = new System.Windows.Forms.Label();
            this.cmbPrinters = new System.Windows.Forms.ComboBox();
            this.label2 = new System.Windows.Forms.Label();
            this.txtSlipLength = new System.Windows.Forms.TextBox();
            this.btnSaveSettings = new System.Windows.Forms.Button();
            this.btnCancelSettings = new System.Windows.Forms.Button();
            this.label3 = new System.Windows.Forms.Label();
            this.cmbPaperSizes = new System.Windows.Forms.ComboBox();
            this.cmbOrientation = new System.Windows.Forms.ComboBox();
            this.numCopies = new System.Windows.Forms.NumericUpDown();
            this.label4 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.numCopies)).BeginInit();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(37, 62);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(89, 16);
            this.label1.TabIndex = 0;
            this.label1.Text = "Select Printer:";
            // 
            // cmbPrinters
            // 
            this.cmbPrinters.FormattingEnabled = true;
            this.cmbPrinters.Location = new System.Drawing.Point(167, 54);
            this.cmbPrinters.Name = "cmbPrinters";
            this.cmbPrinters.Size = new System.Drawing.Size(121, 24);
            this.cmbPrinters.TabIndex = 1;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(37, 106);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(124, 16);
            this.label2.TabIndex = 2;
            this.label2.Text = "Slip Custom Length:";
            // 
            // txtSlipLength
            // 
            this.txtSlipLength.Location = new System.Drawing.Point(167, 106);
            this.txtSlipLength.Name = "txtSlipLength";
            this.txtSlipLength.Size = new System.Drawing.Size(121, 22);
            this.txtSlipLength.TabIndex = 3;
            this.txtSlipLength.Text = "5.5";
            // 
            // btnSaveSettings
            // 
            this.btnSaveSettings.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnSaveSettings.BackColor = System.Drawing.Color.PaleGreen;
            this.btnSaveSettings.Location = new System.Drawing.Point(199, 328);
            this.btnSaveSettings.Name = "btnSaveSettings";
            this.btnSaveSettings.Size = new System.Drawing.Size(113, 51);
            this.btnSaveSettings.TabIndex = 4;
            this.btnSaveSettings.Text = "💾 Save Configuration";
            this.btnSaveSettings.UseVisualStyleBackColor = false;
            this.btnSaveSettings.Click += new System.EventHandler(this.btnSaveSettings_Click);
            // 
            // btnCancelSettings
            // 
            this.btnCancelSettings.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnCancelSettings.BackColor = System.Drawing.Color.IndianRed;
            this.btnCancelSettings.Location = new System.Drawing.Point(374, 328);
            this.btnCancelSettings.Name = "btnCancelSettings";
            this.btnCancelSettings.Size = new System.Drawing.Size(110, 51);
            this.btnCancelSettings.TabIndex = 5;
            this.btnCancelSettings.Text = "❌ Cancel";
            this.btnCancelSettings.UseVisualStyleBackColor = false;
            this.btnCancelSettings.Click += new System.EventHandler(this.btnCancelSettings_Click);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(37, 159);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(117, 16);
            this.label3.TabIndex = 6;
            this.label3.Text = "Paper Profile Size:";
            // 
            // cmbPaperSizes
            // 
            this.cmbPaperSizes.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbPaperSizes.FormattingEnabled = true;
            this.cmbPaperSizes.Location = new System.Drawing.Point(167, 156);
            this.cmbPaperSizes.Name = "cmbPaperSizes";
            this.cmbPaperSizes.Size = new System.Drawing.Size(121, 24);
            this.cmbPaperSizes.TabIndex = 7;
            // 
            // cmbOrientation
            // 
            this.cmbOrientation.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbOrientation.FormattingEnabled = true;
            this.cmbOrientation.Location = new System.Drawing.Point(167, 203);
            this.cmbOrientation.Name = "cmbOrientation";
            this.cmbOrientation.Size = new System.Drawing.Size(121, 24);
            this.cmbOrientation.TabIndex = 8;
            // 
            // numCopies
            // 
            this.numCopies.Location = new System.Drawing.Point(167, 249);
            this.numCopies.Name = "numCopies";
            this.numCopies.Size = new System.Drawing.Size(120, 22);
            this.numCopies.TabIndex = 9;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(37, 206);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(74, 16);
            this.label4.TabIndex = 10;
            this.label4.Text = "Orientation:";
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(26, 251);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(98, 16);
            this.label5.TabIndex = 11;
            this.label5.Text = "Num of Copies:";
            // 
            // PrinterSettingsForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.WindowState = System.Windows.Forms.FormWindowState.Maximized;
            this.Controls.Add(this.label5);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.numCopies);
            this.Controls.Add(this.cmbOrientation);
            this.Controls.Add(this.cmbPaperSizes);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.btnCancelSettings);
            this.Controls.Add(this.btnSaveSettings);
            this.Controls.Add(this.txtSlipLength);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.cmbPrinters);
            this.Controls.Add(this.label1);
            this.Name = "PrinterSettingsForm";
            this.Text = "PrinterSettingsForm";
            ((System.ComponentModel.ISupportInitialize)(this.numCopies)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.ComboBox cmbPrinters;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox txtSlipLength;
        private System.Windows.Forms.Button btnSaveSettings;
        private System.Windows.Forms.Button btnCancelSettings;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.ComboBox cmbPaperSizes;
        private System.Windows.Forms.ComboBox cmbOrientation;
        private System.Windows.Forms.NumericUpDown numCopies;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label label5;
    }
}