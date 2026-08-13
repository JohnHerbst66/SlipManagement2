using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Printing;
using System.Globalization;
using System.Windows.Forms;

namespace SlipManagement2
{
    public partial class PrinterSettingsForm : Form
    {
        // Right-column controls (built in code)
        private TextBox       txtHeaderTitle;
        private NumericUpDown numMarginTop;
        private NumericUpDown numMarginLeft;
        private NumericUpDown numMarginRight;
        private NumericUpDown numMarginBottom;

        // Preset row controls
        private ComboBox cmbPresets;
        private Label    lblPresetName;
        private TextBox  txtPresetName;
        private bool     _suppressPresetChange;

        private const string NewPresetSentinel = "— New preset... —";

        public PrinterSettingsForm()
        {
            InitializeComponent();
            Text          = "Printer Settings";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize   = new Size(870, 500);   // prevents shrinking below where controls clip

            cmbPrinters.DropDownStyle = ComboBoxStyle.DropDownList;
            numCopies.Minimum = 1;
            numCopies.Maximum = 10;

            BuildPresetRow();
            BuildRightColumn();
            LoadSettingsFromDb();

            // Wire after load so preset restore doesn't interfere, then sync initial state
            cmbPrinters.SelectedIndexChanged += (s, e) =>
                btnCalibration.Enabled = cmbPrinters.SelectedItem != null;
            btnCalibration.Enabled = cmbPrinters.SelectedItem != null;
        }

        // ===================================================================
        // PRESET ROW  (sits above all designer controls at y < 43)
        // ===================================================================
        private void BuildPresetRow()
        {
            Controls.Add(new Label
            {
                Text      = "Preset:",
                Location  = new Point(37, 18),
                AutoSize  = true,
                Font      = new Font("Arial", 9, FontStyle.Bold),
            });

            cmbPresets = new ComboBox
            {
                Location      = new Point(100, 14),
                Size          = new Size(220, 24),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font          = new Font("Arial", 9),
            };
            cmbPresets.SelectedIndexChanged += OnPresetSelectionChanged;
            Controls.Add(cmbPresets);

            lblPresetName = new Label
            {
                Text     = "Preset name:",
                Location = new Point(335, 18),
                AutoSize = true,
                Visible  = false,
            };
            Controls.Add(lblPresetName);

            txtPresetName = new TextBox
            {
                Location  = new Point(420, 14),
                Size      = new Size(240, 22),
                MaxLength = 50,
                Visible   = false,
            };
            Controls.Add(txtPresetName);

            // Thin separator below the preset row — anchored so it stretches when the form is resized
            var sep = new Panel
            {
                Location  = new Point(0, 42),
                Size      = new Size(ClientSize.Width, 1),
                Anchor    = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top,
                BackColor = Color.LightGray,
            };
            Controls.Add(sep);
        }

        // ===================================================================
        // RIGHT COLUMN  (company name, slip length, margins)
        // ===================================================================
        private void BuildRightColumn()
        {
            const int lx = 430;   // right-column label x — starts well clear of the left column
            const int cx = 640;   // right-column control x
            const int cw = 190;   // control width — right edge at 830, clear of the form edge once
                                  // Windows scales the form up on a high-DPI display
            const int rh = 38;    // row pitch — a 22px control plus breathing room

            AddSectionHeader("Company / Slip", lx, 65);

            // These two rows used to sit 15px apart while the boxes are 22px tall, so they
            // overlapped and read as one control with two lines of text in it.
            int rowY = 99;
            AddLabel("Company Name:", lx, rowY + 3);
            txtHeaderTitle = AddTextBox(cx, rowY, cw);

            rowY += rh;
            // label2 from the designer is the slip-length label — relocate it to the right column
            label2.Text            = "Slip Length (in):";
            label2.Location        = new Point(lx, rowY + 3);
            txtSlipLength.Location = new Point(cx, rowY);
            txtSlipLength.Width    = cw;

            rowY += rh;
            AddSectionHeader("Print Margins (mm)", lx, rowY);

            rowY += 26;
            AddLabel("Top:",    lx, rowY + 3);
            numMarginTop    = AddMarginSpinner(cx, rowY);

            rowY += rh;
            AddLabel("Left:",   lx, rowY + 3);
            numMarginLeft   = AddMarginSpinner(cx, rowY);

            rowY += rh;
            AddLabel("Right:",  lx, rowY + 3);
            numMarginRight  = AddMarginSpinner(cx, rowY);

            rowY += rh;
            AddLabel("Bottom:", lx, rowY + 3);
            numMarginBottom = AddMarginSpinner(cx, rowY);
        }

        private void AddSectionHeader(string text, int x, int y)
        {
            Controls.Add(new Label
            {
                Text      = text,
                Location  = new Point(x, y),
                AutoSize  = true,
                Font      = new Font("Arial", 9, FontStyle.Bold),
                ForeColor = Color.DimGray,
            });
        }

        private void AddLabel(string text, int x, int y)
            => Controls.Add(new Label { Text = text, Location = new Point(x, y), AutoSize = true });

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
                Location      = new Point(x, y),
                Size          = new Size(90, 22),
                Minimum       = 0,
                Maximum       = 50,
                DecimalPlaces = 1,
                Increment     = 0.5m,
                Value         = 10,
            };
            Controls.Add(n);
            return n;
        }

        // ===================================================================
        // PRESET MANAGEMENT
        // ===================================================================
        private void ReloadPresetCombo(string selectName = null)
        {
            _suppressPresetChange = true;
            try
            {
                cmbPresets.Items.Clear();
                var profiles = DatabaseManager.GetAllPrinterProfiles();
                string activeName = null;
                foreach (var p in profiles)
                {
                    cmbPresets.Items.Add(p.ProfileName);
                    if (p.IsActive) activeName = p.ProfileName;
                }
                cmbPresets.Items.Add(NewPresetSentinel);

                string target = selectName ?? activeName;
                if (target != null && cmbPresets.Items.Contains(target))
                    cmbPresets.SelectedItem = target;
                else if (cmbPresets.Items.Count > 1)
                    cmbPresets.SelectedIndex = 0;
            }
            finally
            {
                _suppressPresetChange = false;
            }

            ApplySelectedPreset();
        }

        private void OnPresetSelectionChanged(object sender, EventArgs e)
        {
            if (_suppressPresetChange) return;
            ApplySelectedPreset();
        }

        private void ApplySelectedPreset()
        {
            string selected = cmbPresets.SelectedItem?.ToString();
            if (selected == null) return;

            bool isNew = selected == NewPresetSentinel;
            lblPresetName.Visible = isNew;
            txtPresetName.Visible = isNew;

            if (isNew)
            {
                txtPresetName.Text = "";
                if (cmbPaperSizes.Items.Contains(PaperSizeHelper.DefaultProfile))
                    cmbPaperSizes.SelectedItem = PaperSizeHelper.DefaultProfile;
                if (cmbOrientation.Items.Contains("Portrait"))
                    cmbOrientation.SelectedItem = "Portrait";
                numCopies.Value    = 1;
                txtSlipLength.Text = "5.5";
                numMarginTop.Value = numMarginLeft.Value = numMarginRight.Value = numMarginBottom.Value = 10;
            }
            else
            {
                var data = DatabaseManager.GetPrinterProfileByName(selected);
                if (data != null)
                {
                    LoadPresetValuesToForm(data);
                    // Selecting an existing preset immediately activates it — spec §6.6
                    DatabaseManager.SetActiveProfile(selected);
                }
            }
        }

        private void LoadPresetValuesToForm(DatabaseManager.PrinterProfileData data)
        {
            if (cmbPrinters.Items.Contains(data.PrinterName))
                cmbPrinters.SelectedItem = data.PrinterName;
            else if (cmbPrinters.Items.Count > 0)
                cmbPrinters.SelectedIndex = 0;

            if (cmbPaperSizes.Items.Contains(data.PaperSizeProfile))
                cmbPaperSizes.SelectedItem = data.PaperSizeProfile;

            if (cmbOrientation.Items.Contains(data.Orientation))
                cmbOrientation.SelectedItem = data.Orientation;

            numCopies.Value       = (decimal)Math.Max((double)numCopies.Minimum, Math.Min((double)numCopies.Maximum, data.NumCopies));
            txtSlipLength.Text    = data.SlipLengthIn.ToString("G", CultureInfo.InvariantCulture);
            numMarginTop.Value    = ClampMargin(data.MarginTopMM);
            numMarginLeft.Value   = ClampMargin(data.MarginLeftMM);
            numMarginRight.Value  = ClampMargin(data.MarginRightMM);
            numMarginBottom.Value = ClampMargin(data.MarginBottomMM);
        }

        private decimal ClampMargin(double mm)
            => (decimal)Math.Max(0, Math.Min((double)numMarginTop.Maximum, mm));

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

                // Paper sizes
                cmbPaperSizes.Items.Clear();
                foreach (var s in PaperSizeHelper.ProfileNames)
                    cmbPaperSizes.Items.Add(s);

                // Orientation
                cmbOrientation.Items.Clear();
                cmbOrientation.Items.Add("Portrait");
                cmbOrientation.Items.Add("Landscape");

                // Header title is global, not per-preset
                txtHeaderTitle.Text = DatabaseManager.GetGlobalSetting("HeaderTitle", "UITVAL GRONDE PTY (LTD)");

                // Load presets and activate the current one
                ReloadPresetCombo();
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
                string selected   = cmbPresets.SelectedItem?.ToString();
                bool   isNew      = selected == NewPresetSentinel;
                string presetName = isNew ? txtPresetName.Text.Trim() : selected;

                if (string.IsNullOrWhiteSpace(presetName))
                {
                    MessageBox.Show("Please enter a name for this preset.", "Preset Name Required",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    if (isNew) txtPresetName.Focus();
                    return;
                }

                string printer     = cmbPrinters.SelectedItem?.ToString()    ?? "EPSON LX-350";
                string paperSize   = cmbPaperSizes.SelectedItem?.ToString()  ?? PaperSizeHelper.DefaultProfile;
                string orientation = cmbOrientation.SelectedItem?.ToString() ?? "Portrait";
                string slipLength  = txtSlipLength.Text.Trim().Replace(',', '.');
                string headerTitle = txtHeaderTitle.Text.Trim();

                if (!double.TryParse(slipLength, NumberStyles.Any, CultureInfo.InvariantCulture, out double lenIn) || lenIn <= 0)
                {
                    MessageBox.Show("Slip Length must be a positive number (inches).", "Validation",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    txtSlipLength.Focus();
                    return;
                }

                var (wMM, hMM) = PaperSizeHelper.GetDimensionsMm(paperSize, lenIn);

                // HeaderTitle is global, not preset-specific
                DatabaseManager.SaveGlobalSetting("HeaderTitle", headerTitle);

                DatabaseManager.SaveOrUpdatePrinterProfile(
                    presetName, printer, paperSize, wMM, hMM,
                    (double)numMarginTop.Value,
                    (double)numMarginLeft.Value,
                    (double)numMarginRight.Value,
                    (double)numMarginBottom.Value,
                    (int)numCopies.Value,
                    orientation,
                    lenIn);

                // Refresh preset list and re-select the saved preset
                ReloadPresetCombo(presetName);

                MessageBox.Show("Settings saved.", "Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error saving settings: " + ex.Message);
            }
        }

        private void btnCancelSettings_Click(object sender, EventArgs e) => Close();

        // ===================================================================
        // CALIBRATION PAGE — opens PrintCalibrationForm for the selected preset
        // ===================================================================
        private void btnCalibration_Click(object sender, EventArgs e)
        {
            string selected = cmbPresets.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(selected) || selected == NewPresetSentinel)
            {
                MessageBox.Show("Please select a saved preset first, then calibrate.",
                    "No Preset Selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (var cal = new PrintCalibrationForm(null, selected))
                cal.ShowDialog(this);
        }
    }
}
