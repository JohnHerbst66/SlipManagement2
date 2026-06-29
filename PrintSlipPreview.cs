using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.Windows.Forms;

namespace SlipManagement2
{
    public partial class PrintSlipPreview : Form
    {
        private readonly Dictionary<string, string> _slipData;
        private PrintDocument       _printDocument;
        private PrintPreviewControl _previewControl;
        private Label               _errorLabel;

        public PrintSlipPreview(Dictionary<string, string> slipData)
        {
            InitializeComponent();
            _slipData = slipData;

            pnlSlip.Visible = false;

            Text            = "Slip Preview — confirm before printing";
            ClientSize      = new Size(850, 700);
            StartPosition   = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox     = false;

            BuildLayout();
            RefreshPreview();
        }

        // ===================================================================
        // LAYOUT
        // ===================================================================
        private void BuildLayout()
        {
            // Button panel — docked to bottom
            var buttonPanel = new Panel
            {
                Dock      = DockStyle.Bottom,
                Height    = 60,
                BackColor = SystemColors.Control
            };

            // Centre three buttons: Print | Adjust Settings | Cancel
            // Total button width: 130 + 150 + 130 = 410, gaps 2×20 = 40 → 450px
            // Horizontal offset from panel left: (850 - 450) / 2 = 200
            int bx = 200;

            var btnPrint = MakeButton("Print", bx, Color.LightGreen, 130);
            btnPrint.Click += BtnPrint_Click;
            bx += 130 + 20;

            var btnAdjust = MakeButton("Adjust Settings", bx, Color.LightSkyBlue, 150);
            btnAdjust.Click += BtnAdjust_Click;
            bx += 150 + 20;

            var btnCancel = MakeButton("Cancel", bx, Color.LightCoral, 130);
            btnCancel.Click += BtnCancel_Click;

            buttonPanel.Controls.Add(btnPrint);
            buttonPanel.Controls.Add(btnAdjust);
            buttonPanel.Controls.Add(btnCancel);

            // Preview fills remaining space
            _previewControl = new PrintPreviewControl
            {
                Dock     = DockStyle.Fill,
                AutoZoom = true
            };

            // Error label shown when preview generation fails
            _errorLabel = new Label
            {
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.DarkRed,
                Font      = new Font("Arial", 10),
                Visible   = false
            };

            // Fill must be added before Bottom so docking resolves correctly
            Controls.Add(_previewControl);
            Controls.Add(_errorLabel);
            Controls.Add(buttonPanel);
        }

        private static Button MakeButton(string text, int x, Color back, int width)
            => new Button
            {
                Text      = text,
                Size      = new Size(width, 40),
                Location  = new Point(x, 10),
                BackColor = back,
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Arial", 10, FontStyle.Bold)
            };

        // ===================================================================
        // PREVIEW REFRESH — called on open AND after settings change
        // ===================================================================
        private void RefreshPreview()
        {
            try
            {
                _printDocument          = SlipPrintEngine.BuildPrintDocument(_slipData);
                _previewControl.Document = _printDocument;
                _previewControl.InvalidatePreview();  // clears cached pages, forces re-render
                _previewControl.Visible = true;
                _errorLabel.Visible     = false;
            }
            catch (Exception ex)
            {
                _previewControl.Visible = false;
                _errorLabel.Text    = "Preview unavailable: " + ex.Message +
                                      "\n\nYou can still click Print to send to the printer.";
                _errorLabel.Visible = true;

                // Keep a valid document so Print still works even if preview failed
                if (_printDocument == null)
                    _printDocument = SlipPrintEngine.BuildPrintDocument(_slipData);
            }
        }

        // ===================================================================
        // BUTTON HANDLERS
        // ===================================================================
        private void BtnPrint_Click(object sender, EventArgs e)
        {
            try
            {
                _printDocument.Print();
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Print failed: " + ex.Message, "Printer Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnAdjust_Click(object sender, EventArgs e)
        {
            using (var sf = new PrinterSettingsForm())
                sf.ShowDialog(this);

            // Always refresh — the user may have changed settings even if they
            // cancelled the settings dialog (the preview should show current DB state).
            RefreshPreview();
        }

        private void BtnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }
}
