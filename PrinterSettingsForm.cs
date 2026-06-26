using System;
using System.Drawing;
using System.Drawing.Printing;
using System.Globalization;
using System.Windows.Forms;

namespace SlipManagement2
{
    public partial class PrinterSettingsForm : Form
    {
        // Extra controls added in code (not in designer) — right column
        private TextBox        txtHeaderTitle;
        private NumericUpDown  numMarginTop;
        private NumericUpDown  numMarginLeft;
        private NumericUpDown  numMarginRight;
        private NumericUpDown  numMarginBottom;

        public PrinterSettingsForm()
        {
            InitializeComponent();
            Text            = "Printer Settings";
            StartPosition   = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox     = false;
            ClientSize      = new Size(750, 420);

            // Right-column wiring: DropDownStyle for printer combo, numCopies bounds
            cmbPrinters.DropDownStyle = ComboBoxStyle.DropDownList;
            numCopies.Minimum = 1;
            numCopies.Maximum = 10;

            BuildRightColumn();
            LoadSettingsFromDb();
        }

        // ===================================================================
        // EXTRA CONTROLS (right column)
        // ===================================================================
        private void BuildRightColumn()
        {
            const int lx = 390;   // label x
            const int cx = 560;   // control x
            const int cw = 160;   // control width

            AddSectionHeader("Company / Slip",  lx, 20);

            AddLabel("Company Name:",            lx, 57);
            txtHeaderTitle = AddTextBox(cx, 54, cw);

            AddLabel("Slip Length (inches):",   lx, 107);
            // txtSlipLength already exists from designer — we just reposition its label

            AddSectionHeader("Print Margins (mm)", lx, 140);

            AddLabel("Top:",    lx, 166);
            numMarginTop    = AddMarginSpinner(cx, 163);

            AddLabel("Left:",   lx, 204);
            numMarginLeft   = AddMarginSpinner(cx, 201);

            AddLabel("Right:",  lx, 242);
            numMarginRight  = AddMarginSpinner(cx, 239);

            AddLabel("Bottom:", lx, 280);
            numMarginBottom = AddMarginSpinner(cx, 277);

            // Move txtSlipLength label text and position — it belongs in the right column
            label2.Text     = "Slip Length (in):";
            label2.Location = new Point(lx, 107);
            txtSlipLength.Location = new Point(cx, 104);
            txtSlipLength.Width    = cw;
        }

        private void AddSectionHeader(string text, int x, int y)
        {
            var lbl = new Label
            {
                Text      = text,
                Location  = new Point(x, y),
                AutoSize  = true,
                Font      = new Font("Arial", 9, FontStyle.Bold),
                ForeColor = Color.DimGray
            };
            Controls.Add(lbl);
        }

        private void AddLabel(string text, int x, int y)
        {
            Controls.Add(new Label { Text = text, Location = new Point(x, y), AutoSize = true });
        }

        private TextBox AddTextBox(int x, int y, int width)
        {
            var tb = new TextBox { Location = new Point(x, y), Size = new Size(width, 22) };
            Controls.Add(tb);
            return tb;
        }

        private NumericUpDown AddMarginSpinner(int x, int y)
        {
            var n = new NumericUpDown
            {
                Location       = new Point(x, y),
                Size           = new Size(90, 22),
                Minimum        = 0,
                Maximum        = 50,
                DecimalPlaces  = 1,
                Increment      = 0.5m,
                Value          = 10
            };
            Controls.Add(n);
            return n;
        }

        // ===================================================================
        // LOAD / SAVE
        // ===================================================================
        private void LoadSettingsFromDb()
        {
            try
            {
                // Installed printers
                cmbPrinters.Items.Clear();
                foreach (string p in PrinterSettings.InstalledPrinters)
                    cmbPrinters.Items.Add(p);

                string savedPrinter = DatabaseManager.GetGlobalSetting("SelectedPrinter", "EPSON LX-350");
                cmbPrinters.SelectedItem = cmbPrinters.Items.Contains(savedPrinter)
                    ? savedPrinter
                    : (cmbPrinters.Items.Count > 0 ? cmbPrinters.Items[0] : null);

                // Paper sizes
                cmbPaperSizes.Items.Clear();
                foreach (var s in new[] { "A4", "A5", "A6", "Letter", "Small151x151", "Small240x102" })
                    cmbPaperSizes.Items.Add(s);

                string savedPaper = DatabaseManager.GetGlobalSetting("PaperSizeProfile", "Small240x102");
                cmbPaperSizes.SelectedItem = cmbPaperSizes.Items.Contains(savedPaper) ? savedPaper : cmbPaperSizes.Items[0];

                // Orientation
                cmbOrientation.Items.Clear();
                cmbOrientation.Items.Add("Portrait");
                cmbOrientation.Items.Add("Landscape");
                string savedOrientation = DatabaseManager.GetGlobalSetting("PrintOrientation", "Portrait");
                cmbOrientation.SelectedItem = cmbOrientation.Items.Contains(savedOrientation) ? savedOrientation : cmbOrientation.Items[0];

                // Copies
                string savedCopies = DatabaseManager.GetGlobalSetting("PrintCopiesCount", "1");
                if (decimal.TryParse(savedCopies, out decimal copiesVal))
                    numCopies.Value = Math.Max(numCopies.Minimum, Math.Min(numCopies.Maximum, copiesVal));

                // Slip length
                txtSlipLength.Text = DatabaseManager.GetGlobalSetting("SlipCustomLength", "5.5");

                // Header title
                txtHeaderTitle.Text = DatabaseManager.GetGlobalSetting("HeaderTitle", "UITVAL GRONDE PTY (LTD)");

                // Margins from the active printer profile
                var profile = DatabaseManager.GetActiveProfile();
                numMarginTop.Value    = (decimal)Math.Min((double)numMarginTop.Maximum,    profile.MarginTopMM);
                numMarginLeft.Value   = (decimal)Math.Min((double)numMarginLeft.Maximum,   profile.MarginLeftMM);
                numMarginRight.Value  = (decimal)Math.Min((double)numMarginRight.Maximum,  profile.MarginRightMM);
                numMarginBottom.Value = (decimal)Math.Min((double)numMarginBottom.Maximum, profile.MarginBottomMM);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading settings: " + ex.Message);
            }
        }

        private void btnSaveSettings_Click(object sender, EventArgs e)
        {
            try
            {
                string printer     = cmbPrinters.SelectedItem?.ToString()     ?? "EPSON LX-350";
                string paperSize   = cmbPaperSizes.SelectedItem?.ToString()   ?? "Small240x102";
                string orientation = cmbOrientation.SelectedItem?.ToString()  ?? "Portrait";
                string copies      = numCopies.Value.ToString();
                string slipLength  = txtSlipLength.Text.Trim().Replace(',', '.');
                string headerTitle = txtHeaderTitle.Text.Trim();

                // Validate slip length
                if (!double.TryParse(slipLength, NumberStyles.Any, CultureInfo.InvariantCulture, out double lenIn) || lenIn <= 0)
                {
                    MessageBox.Show("Slip Length must be a positive number (inches).", "Validation",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    txtSlipLength.Focus();
                    return;
                }

                // Save GlobalSettings
                DatabaseManager.SaveGlobalSetting("SelectedPrinter",  printer);
                DatabaseManager.SaveGlobalSetting("PaperSizeProfile", paperSize);
                DatabaseManager.SaveGlobalSetting("PrintOrientation", orientation);
                DatabaseManager.SaveGlobalSetting("PrintCopiesCount", copies);
                DatabaseManager.SaveGlobalSetting("SlipCustomLength", slipLength);
                DatabaseManager.SaveGlobalSetting("HeaderTitle",      headerTitle);

                // Resolve paper dimensions from the selected profile
                double wMM = 240, hMM = lenIn * 25.4;
                switch (paperSize)
                {
                    case "A4":           wMM = 210;   hMM = 297;         break;
                    case "A5":           wMM = 148;   hMM = 210;         break;
                    case "A6":           wMM = 105;   hMM = 148;         break;
                    case "Letter":       wMM = 215.9; hMM = 279.4;       break;
                    case "Small151x151": wMM = 151;   hMM = 151;         break;
                    case "Small240x102": wMM = 240;   hMM = lenIn * 25.4; break;
                }

                // Save PrinterProfiles (active profile)
                DatabaseManager.SaveActiveProfile(
                    printer, paperSize, wMM, hMM,
                    (double)numMarginTop.Value,
                    (double)numMarginLeft.Value,
                    (double)numMarginRight.Value,
                    (double)numMarginBottom.Value,
                    (int)numCopies.Value);

                MessageBox.Show("Settings saved.", "Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error saving settings: " + ex.Message);
            }
        }

        private void btnCancelSettings_Click(object sender, EventArgs e) => Close();
    }
}
