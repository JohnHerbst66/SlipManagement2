namespace SlipManagement2
{
    partial class CustomizeSlipsForm
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
            this.tbcCustomize = new System.Windows.Forms.TabControl();
            this.tabFieldSetup = new System.Windows.Forms.TabPage();
            this.dgvFieldSetup = new System.Windows.Forms.DataGridView();
            this.tabSlipDesign = new System.Windows.Forms.TabPage();
            this.btnSave = new System.Windows.Forms.Button();
            this.btnExit = new System.Windows.Forms.Button();
            this.tbcCustomize.SuspendLayout();
            this.tabFieldSetup.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvFieldSetup)).BeginInit();
            this.SuspendLayout();
            // 
            // tbcCustomize
            // 
            this.tbcCustomize.Controls.Add(this.tabFieldSetup);
            this.tbcCustomize.Controls.Add(this.tabSlipDesign);
            this.tbcCustomize.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.tbcCustomize.Location = new System.Drawing.Point(23, 54);
            this.tbcCustomize.Name = "tbcCustomize";
            this.tbcCustomize.SelectedIndex = 0;
            this.tbcCustomize.Size = new System.Drawing.Size(1152, 574);
            this.tbcCustomize.TabIndex = 0;
            // 
            // tabFieldSetup
            // 
            this.tabFieldSetup.Controls.Add(this.dgvFieldSetup);
            this.tabFieldSetup.Location = new System.Drawing.Point(4, 25);
            this.tabFieldSetup.Name = "tabFieldSetup";
            this.tabFieldSetup.Padding = new System.Windows.Forms.Padding(3);
            this.tabFieldSetup.Size = new System.Drawing.Size(1144, 545);
            this.tabFieldSetup.TabIndex = 0;
            this.tabFieldSetup.Text = "📑 Field Setup";
            this.tabFieldSetup.UseVisualStyleBackColor = true;
            // 
            // dgvFieldSetup
            // 
            this.dgvFieldSetup.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.dgvFieldSetup.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvFieldSetup.Location = new System.Drawing.Point(15, 6);
            this.dgvFieldSetup.Name = "dgvFieldSetup";
            this.dgvFieldSetup.RowHeadersWidth = 51;
            this.dgvFieldSetup.RowTemplate.Height = 24;
            this.dgvFieldSetup.Size = new System.Drawing.Size(1123, 400);
            this.dgvFieldSetup.TabIndex = 0;
            // 
            // tabSlipDesign
            // 
            this.tabSlipDesign.Location = new System.Drawing.Point(4, 25);
            this.tabSlipDesign.Name = "tabSlipDesign";
            this.tabSlipDesign.Padding = new System.Windows.Forms.Padding(3);
            this.tabSlipDesign.Size = new System.Drawing.Size(1144, 545);
            this.tabSlipDesign.TabIndex = 1;
            this.tabSlipDesign.Text = "🖨️ Slip Design";
            this.tabSlipDesign.UseVisualStyleBackColor = true;
            // 
            // btnSave
            // 
            this.btnSave.BackColor = System.Drawing.Color.PaleGreen;
            this.btnSave.Location = new System.Drawing.Point(27, 12);
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(137, 36);
            this.btnSave.TabIndex = 21;
            this.btnSave.Text = "SAVE";
            this.btnSave.UseVisualStyleBackColor = false;
            this.btnSave.Click += new System.EventHandler(this.btnSave_Click);
            // 
            // btnExit
            // 
            this.btnExit.BackColor = System.Drawing.Color.Tomato;
            this.btnExit.Location = new System.Drawing.Point(170, 12);
            this.btnExit.Name = "btnExit";
            this.btnExit.Size = new System.Drawing.Size(137, 36);
            this.btnExit.TabIndex = 22;
            this.btnExit.Text = "Exit";
            this.btnExit.UseVisualStyleBackColor = false;
            this.btnExit.Click += new System.EventHandler(this.btnExit_Click);
            // 
            // CustomizeSlipsForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1211, 640);
            this.WindowState = System.Windows.Forms.FormWindowState.Maximized;
            this.Controls.Add(this.btnExit);
            this.Controls.Add(this.btnSave);
            this.Controls.Add(this.tbcCustomize);
            this.Name = "CustomizeSlipsForm";
            this.Text = "CostomizeSlipsForm";
            this.tbcCustomize.ResumeLayout(false);
            this.tabFieldSetup.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dgvFieldSetup)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TabControl tbcCustomize;
        private System.Windows.Forms.TabPage tabFieldSetup;
        private System.Windows.Forms.TabPage tabSlipDesign;
        private System.Windows.Forms.Button btnSave;
        private System.Windows.Forms.Button btnExit;
        private System.Windows.Forms.DataGridView dgvFieldSetup;
    }
}