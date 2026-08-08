using System;
using System.Drawing;
using System.Drawing.Printing;
using System.Globalization;
using System.Windows.Forms;

namespace SlipManagement2
{
    public class FirstTimeSetupForm : Form
    {
        private TextBox  txtCompanyName;
        private ComboBox cmbPrinter;
        private TextBox  txtLogoPath;
        private ComboBox cmbPaperSize;
        private TextBox  txtSlipLength;
        private Label    lblSlipLength;
        private CheckBox chkSavePreset;
        private TextBox  txtPresetName;

        // "Customize Fields..." has to create the database so CustomizeSlipsForm can load the
        // field list. That must not count as completing setup — see OnFormClosing.
        private bool _dbCreatedDuringSetup;

        public FirstTimeSetupForm()
        {
            Text            = "Welcome — First Time Setup";
            ClientSize      = new Size(520, 475);
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
                Location = new Point(20, 15),
                AutoSize = true,
            });

            Controls.Add(new Label
            {
                Text      = "Set up a few details before you get started. You can change these later in Settings.",
                Font      = new Font("Arial", 9),
                ForeColor = Color.Gray,
                Location  = new Point(20, 50),
                Size      = new Size(480, 30),
                AutoSize  = false,
            });

            Controls.Add(new Panel
            {
                Location  = new Point(20, 84),
                Size      = new Size(480, 1),
                BackColor = Color.LightGray,
            });

            // Company Name
            Controls.Add(new Label { Text = "Company Name:", Location = new Point(20, 98), AutoSize = true });
            txtCompanyName = new TextBox
            {
                Location  = new Point(20, 116),
                Size      = new Size(480, 22),
                Text      = "UITVAL GRONDE PTY (LTD)",
                MaxLength = 60,
            };
            Controls.Add(txtCompanyName);

            // Logo file
            Controls.Add(new Label { Text = "Logo File Path (optional):", Location = new Point(20, 148), AutoSize = true });
            txtLogoPath = new TextBox
            {
                Location = new Point(20, 166),
                Size     = new Size(396, 22),
            };
            Controls.Add(txtLogoPath);
            var btnBrowse = new Button
            {
                Text      = "Browse...",
                Location  = new Point(424, 164),
                Size      = new Size(76, 26),
                FlatStyle = FlatStyle.Flat,
            };
            btnBrowse.Click += BtnBrowseLogo_Click;
            Controls.Add(btnBrowse);

            // Default printer
            Controls.Add(new Label { Text = "Default Printer:", Location = new Point(20, 202), AutoSize = true });
            cmbPrinter = new ComboBox
            {
                Location      = new Point(20, 220),
                Size          = new Size(480, 22),
                DropDownStyle = ComboBoxStyle.DropDownList,
            };
            foreach (string p in PrinterSettings.InstalledPrinters)
                cmbPrinter.Items.Add(p);
            if (cmbPrinter.Items.Count > 0)
                cmbPrinter.SelectedIndex = 0;
            Controls.Add(cmbPrinter);

            // Paper size + slip length (same row; slip length is conditional)
            Controls.Add(new Label { Text = "Paper Size:", Location = new Point(20, 252), AutoSize = true });
            cmbPaperSize = new ComboBox
            {
                Location      = new Point(20, 270),
                Size          = new Size(220, 22),
                DropDownStyle = ComboBoxStyle.DropDownList,
            };
            foreach (var s in PaperSizeHelper.ProfileNames)
                cmbPaperSize.Items.Add(s);
            cmbPaperSize.SelectedItem = PaperSizeHelper.DefaultProfile;
            cmbPaperSize.SelectedIndexChanged += (s, ev) => UpdateSlipLengthVisibility();
            Controls.Add(cmbPaperSize);

            lblSlipLength = new Label { Text = "Slip Length (inches):", Location = new Point(256, 252), AutoSize = true };
            Controls.Add(lblSlipLength);
            txtSlipLength = new TextBox
            {
                Location  = new Point(256, 270),
                Size      = new Size(100, 22),
                Text      = "5.5",
                MaxLength = 6,
            };
            // Reject letters as they are typed rather than only complaining on Get Started.
            // A rejected keystroke is unmistakable; a message box after the fact is easy to miss.
            txtSlipLength.KeyPress += TxtSlipLength_KeyPress;
            Controls.Add(txtSlipLength);

            // Preset save option
            chkSavePreset = new CheckBox
            {
                Text     = "Save as printer preset named:",
                Location = new Point(20, 310),
                Size     = new Size(212, 22),
                Checked  = true,
                Font     = new Font("Arial", 9),
            };
            chkSavePreset.CheckedChanged += (s, ev) => txtPresetName.Enabled = chkSavePreset.Checked;
            Controls.Add(chkSavePreset);

            txtPresetName = new TextBox
            {
                Location  = new Point(236, 308),
                Size      = new Size(150, 22),
                Text      = "Default",
                MaxLength = 50,
            };
            Controls.Add(txtPresetName);

            // Action buttons
            var btnCustomize = new Button
            {
                Text      = "Customize Fields...",
                Size      = new Size(160, 38),
                Location  = new Point(20, 350),
                BackColor = Color.Gainsboro,
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Arial", 9),
            };
            btnCustomize.Click += BtnCustomizeFields_Click;
            Controls.Add(btnCustomize);

            var btnStart = new Button
            {
                Text      = "Get Started",
                Size      = new Size(160, 38),
                Location  = new Point(340, 350),
                BackColor = Color.PaleGreen,
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Arial", 11, FontStyle.Bold),
            };
            btnStart.Click += BtnStart_Click;
            Controls.Add(btnStart);

            // Footer note
            Controls.Add(new Label
            {
                Text      = "Settings can be changed later via the Settings button on the main screen.",
                Font      = new Font("Arial", 8),
                ForeColor = Color.Gray,
                Location  = new Point(20, 410),
                Size      = new Size(480, 18),
                AutoSize  = false,
                TextAlign = ContentAlignment.MiddleCenter,
            });

            UpdateSlipLengthVisibility();
        }

        // Slip length is a measurement in inches: digits plus at most one decimal separator.
        // Mirrors the filtering already applied to the Tons field on CreateSlip.
        private void TxtSlipLength_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (char.IsControl(e.KeyChar)) return;

            if (!char.IsDigit(e.KeyChar) && e.KeyChar != '.' && e.KeyChar != ',')
            {
                e.Handled = true;
                return;
            }

            if (e.KeyChar == '.' || e.KeyChar == ',')
            {
                // Build what the text WOULD become, so replacing a selected separator still works
                string simulated = txtSlipLength.Text
                    .Remove(txtSlipLength.SelectionStart, txtSlipLength.SelectionLength)
                    .Insert(txtSlipLength.SelectionStart, e.KeyChar.ToString());

                if (simulated.Split('.', ',').Length - 1 > 1)
                    e.Handled = true;
            }
        }

        private void UpdateSlipLengthVisibility()
        {
            bool isCustom = PaperSizeHelper.UsesCustomLength(cmbPaperSize.SelectedItem?.ToString());
            lblSlipLength.Visible = isCustom;
            txtSlipLength.Visible = isCustom;
        }

        private void BtnBrowseLogo_Click(object sender, EventArgs e)
        {
            using (var ofd = new OpenFileDialog { Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp" })
            {
                if (ofd.ShowDialog() == DialogResult.OK)
                    txtLogoPath.Text = ofd.FileName;
            }
        }

        private void BtnCustomizeFields_Click(object sender, EventArgs e)
        {
            // Database must exist before CustomizeSlipsForm can load FieldConfig
            DatabaseManager.InitializeDatabase();
            _dbCreatedDuringSetup = true;
            using (var customize = new CustomizeSlipsForm())
                customize.ShowDialog(this);
        }

        // "Get Started" is the only action that commits a setup. If the operator abandons this
        // screen after Customize Fields created the database, remove it again — otherwise the
        // next launch sees a database, skips setup entirely, and lands on a half-configured
        // system holding the seeded defaults instead of the operator's choices, with no way
        // back to this screen short of deleting the file by hand.
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);

            if (DialogResult != DialogResult.OK && _dbCreatedDuringSetup)
                DatabaseManager.DeleteDatabaseFile();
        }

        private void BtnStart_Click(object sender, EventArgs e)
        {
            // 1. Validate company name
            string companyName = txtCompanyName.Text.Trim();
            if (string.IsNullOrEmpty(companyName))
            {
                MessageBox.Show("Please enter a company name.", "Required",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtCompanyName.Focus();
                return;
            }

            // 2. Validate slip length when Small240x102 is selected
            string slipLenRaw = txtSlipLength.Text.Trim().Replace(',', '.');
            double slipLenIn  = 5.5;
            if (txtSlipLength.Visible)
            {
                if (!double.TryParse(slipLenRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out slipLenIn) || slipLenIn <= 0)
                {
                    MessageBox.Show("Slip Length must be a positive number (inches).", "Validation",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    txtSlipLength.Focus();
                    return;
                }
            }

            // 3. Initialize database and persist global settings
            DatabaseManager.InitializeDatabase();
            DatabaseManager.SaveGlobalSetting("HeaderTitle", companyName);
            DatabaseManager.SaveGlobalSetting("LogoPath",    txtLogoPath.Text.Trim());

            string printer   = cmbPrinter.SelectedItem?.ToString();
            string paperSize = cmbPaperSize.SelectedItem?.ToString() ?? PaperSizeHelper.DefaultProfile;
            if (!string.IsNullOrEmpty(printer))
                DatabaseManager.SaveGlobalSetting("SelectedPrinter", printer);
            DatabaseManager.SaveGlobalSetting("PaperSizeProfile", paperSize);
            DatabaseManager.SaveGlobalSetting("SlipCustomLength",
                slipLenIn.ToString("G", CultureInfo.InvariantCulture));

            // 4. Write the chosen printer/paper values to a profile.
            //
            // This happens whether or not "save as preset" is ticked. The checkbox only decides
            // WHICH profile gets the values — a named one, or the "Default" that InitializeDatabase
            // just seeded. It must never mean "discard them": the seeded Default is hardcoded to
            // Small240x102 and is the active profile, and SlipPrintEngine.GetPageDimensionsMm reads
            // the active profile ahead of GlobalSettings. Leaving it untouched meant an operator who
            // selected A4 and unticked the box silently got a 240 x 139.7 mm page.
            string presetName = chkSavePreset.Checked ? txtPresetName.Text.Trim() : "Default";
            if (string.IsNullOrEmpty(presetName)) presetName = "Default";

            var (wMM, hMM) = PaperSizeHelper.GetDimensionsMm(paperSize, slipLenIn);

            DatabaseManager.SaveOrUpdatePrinterProfile(
                presetName,
                printer ?? "EPSON LX-350",
                paperSize,
                wMM, hMM,
                10, 10, 10, 10,
                1,
                "Portrait",
                slipLenIn);

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
