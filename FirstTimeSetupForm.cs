using System;
using System.Drawing;
using System.Drawing.Printing;
using System.Windows.Forms;

namespace SlipManagement2
{
    public class FirstTimeSetupForm : Form
    {
        private TextBox  txtCompanyName;
        private ComboBox cmbPrinter;

        public FirstTimeSetupForm()
        {
            Text            = "Welcome — First Time Setup";
            ClientSize      = new Size(480, 350);
            StartPosition   = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox     = false;
            MinimizeBox     = false;
            BackColor       = Color.White;

            BuildLayout();
        }

        private void BuildLayout()
        {
            Controls.Add(new Label
            {
                Text     = "Welcome to Slip Management",
                Font     = new Font("Arial", 15, FontStyle.Bold),
                Location = new Point(20, 22),
                AutoSize = true
            });

            Controls.Add(new Label
            {
                Text      = "Set up a few details before you get started. You can change these later in Settings.",
                Font      = new Font("Arial", 9),
                ForeColor = Color.Gray,
                Location  = new Point(20, 58),
                Size      = new Size(440, 34),
                AutoSize  = false
            });

            Controls.Add(new Panel
            {
                Location  = new Point(20, 98),
                Size      = new Size(440, 1),
                BackColor = Color.LightGray
            });

            Controls.Add(new Label { Text = "Company Name:", Location = new Point(20, 112), AutoSize = true });
            txtCompanyName = new TextBox
            {
                Location  = new Point(20, 132),
                Size      = new Size(440, 22),
                Text      = "UITVAL GRONDE PTY (LTD)",
                MaxLength = 60
            };
            Controls.Add(txtCompanyName);

            Controls.Add(new Label { Text = "Default Printer:", Location = new Point(20, 172), AutoSize = true });
            cmbPrinter = new ComboBox
            {
                Location      = new Point(20, 192),
                Size          = new Size(440, 22),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            foreach (string p in PrinterSettings.InstalledPrinters)
                cmbPrinter.Items.Add(p);
            if (cmbPrinter.Items.Count > 0)
                cmbPrinter.SelectedIndex = 0;
            Controls.Add(cmbPrinter);

            var btnStart = new Button
            {
                Text      = "Get Started",
                Size      = new Size(160, 42),
                Location  = new Point((ClientSize.Width - 160) / 2, 256),
                BackColor = Color.PaleGreen,
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Arial", 11, FontStyle.Bold)
            };
            btnStart.Click += BtnStart_Click;
            Controls.Add(btnStart);

            Controls.Add(new Label
            {
                Text      = "Settings can be changed later via the Settings button on the main screen.",
                Font      = new Font("Arial", 8),
                ForeColor = Color.Gray,
                Location  = new Point(20, 315),
                Size      = new Size(440, 18),
                AutoSize  = false,
                TextAlign = ContentAlignment.MiddleCenter
            });
        }

        private void BtnStart_Click(object sender, EventArgs e)
        {
            string companyName = txtCompanyName.Text.Trim();
            if (string.IsNullOrEmpty(companyName))
            {
                MessageBox.Show("Please enter a company name.", "Required",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtCompanyName.Focus();
                return;
            }

            DatabaseManager.InitializeDatabase();
            DatabaseManager.SaveGlobalSetting("HeaderTitle", companyName);

            string printer = cmbPrinter.SelectedItem?.ToString();
            if (!string.IsNullOrEmpty(printer))
                DatabaseManager.SaveGlobalSetting("SelectedPrinter", printer);

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
