using SlipManagement2;
using System;
namespace SlipManagement2
{
    partial class Main
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
            this.btnCreate = new System.Windows.Forms.Button();
            this.lblMain = new System.Windows.Forms.Label();
            this.flpSlips = new System.Windows.Forms.FlowLayoutPanel();
            this.lblSlipsPanel = new System.Windows.Forms.Label();
            this.btnOpenSettings = new System.Windows.Forms.Button();
            this.button1 = new System.Windows.Forms.Button();
            this.btnExit = new System.Windows.Forms.Button();
            this.flpSlips.SuspendLayout();
            this.SuspendLayout();
            // 
            // btnCreate
            // 
            this.btnCreate.BackColor = System.Drawing.Color.PaleGreen;
            this.btnCreate.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnCreate.Location = new System.Drawing.Point(12, 525);
            this.btnCreate.Name = "btnCreate";
            this.btnCreate.Size = new System.Drawing.Size(113, 45);
            this.btnCreate.TabIndex = 0;
            this.btnCreate.Text = "Create";
            this.btnCreate.UseVisualStyleBackColor = false;
            this.btnCreate.Click += new System.EventHandler(this.btnCreate_Click);
            // 
            // lblMain
            // 
            this.lblMain.AutoSize = true;
            this.lblMain.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblMain.Location = new System.Drawing.Point(29, 22);
            this.lblMain.Name = "lblMain";
            this.lblMain.Size = new System.Drawing.Size(59, 25);
            this.lblMain.TabIndex = 1;
            this.lblMain.Text = "Main";
            // 
            // flpSlips
            // 
            this.flpSlips.Controls.Add(this.lblSlipsPanel);
            this.flpSlips.Location = new System.Drawing.Point(12, 78);
            this.flpSlips.Name = "flpSlips";
            this.flpSlips.Size = new System.Drawing.Size(1141, 421);
            this.flpSlips.TabIndex = 2;
            // 
            // lblSlipsPanel
            // 
            this.lblSlipsPanel.AutoSize = true;
            this.lblSlipsPanel.Location = new System.Drawing.Point(3, 0);
            this.lblSlipsPanel.Name = "lblSlipsPanel";
            this.lblSlipsPanel.Size = new System.Drawing.Size(154, 16);
            this.lblSlipsPanel.TabIndex = 0;
            this.lblSlipsPanel.Text = "This is your current Slips:";
            // 
            // btnOpenSettings
            // 
            this.btnOpenSettings.BackColor = System.Drawing.Color.Gainsboro;
            this.btnOpenSettings.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnOpenSettings.Location = new System.Drawing.Point(1040, 525);
            this.btnOpenSettings.Name = "btnOpenSettings";
            this.btnOpenSettings.Size = new System.Drawing.Size(113, 45);
            this.btnOpenSettings.TabIndex = 1;
            this.btnOpenSettings.Text = "⚙️ Printer Settings";
            this.btnOpenSettings.UseVisualStyleBackColor = false;
            this.btnOpenSettings.Click += new System.EventHandler(this.btnOpenSettings_Click);
            // 
            // button1
            // 
            this.button1.BackColor = System.Drawing.Color.Gainsboro;
            this.button1.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.button1.Location = new System.Drawing.Point(921, 525);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(113, 45);
            this.button1.TabIndex = 3;
            this.button1.Text = "Slip History";
            this.button1.UseVisualStyleBackColor = false;
            // 
            // btnExit
            // 
            this.btnExit.BackColor = System.Drawing.Color.RosyBrown;
            this.btnExit.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnExit.Location = new System.Drawing.Point(1040, 15);
            this.btnExit.Name = "btnExit";
            this.btnExit.Size = new System.Drawing.Size(113, 45);
            this.btnExit.TabIndex = 1;
            this.btnExit.Text = "Exit";
            this.btnExit.UseVisualStyleBackColor = false;
            // 
            // Main
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(120F, 120F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.ClientSize = new System.Drawing.Size(1220, 634);
            this.Controls.Add(this.button1);
            this.Controls.Add(this.btnExit);
            this.Controls.Add(this.flpSlips);
            this.Controls.Add(this.btnOpenSettings);
            this.Controls.Add(this.lblMain);
            this.Controls.Add(this.btnCreate);
            this.Name = "Main";
            this.Text = "Slip Management System";
            this.Load += new System.EventHandler(this.Main_Load);
            this.flpSlips.ResumeLayout(false);
            this.flpSlips.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnCreate;
        private System.Windows.Forms.Label lblMain;
        public System.Windows.Forms.FlowLayoutPanel flpSlips;
        private System.Windows.Forms.Label lblSlipsPanel;
        private System.Windows.Forms.Button btnOpenSettings;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.Button btnExit;
    }
}

