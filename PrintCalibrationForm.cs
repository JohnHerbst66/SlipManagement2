using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace SlipManagement2
{
    public partial class PrintCalibrationForm : Form
    {
        private readonly Dictionary<string, string> _slipData;
        private readonly string _presetName;   // null = use active profile

        // settings controls
        private NumericUpDown _numPaperW, _numPaperH;
        private NumericUpDown _numTop, _numLeft, _numRight, _numBottom;
        private NumericUpDown _numFontScale;
        private NumericUpDown _numOffsetX, _numOffsetY;
        private ComboBox      _cmbCopiesPerPage;
        private Label         _lblCopiesHint;

        // preview
        private PreviewCanvas _canvas;
        private Bitmap        _slipBitmap;

        private bool _suppressRebuild;

        private sealed class PreviewCanvas : Panel
        {
            public PreviewCanvas() { DoubleBuffered = true; }
        }

        // Called from PrintSlipPreview (uses active profile, no preset name).
        // Called from PrinterSettingsForm (uses specific named preset).
        public PrintCalibrationForm(Dictionary<string, string> slipData = null, string presetName = null)
        {
            InitializeComponent();
            _slipData    = slipData ?? SampleData();
            _presetName  = presetName;

            Text          = presetName != null
                          ? $"Print Calibration — {presetName}"
                          : "Print Calibration";
            ClientSize    = new Size(1020, 680);
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize   = new Size(800, 520);

            _suppressRebuild = true;   // suppress during layout + load
            BuildLayout();
            LoadCurrentSettings();
            _suppressRebuild = false;
            RebuildBitmap();
        }

        // ===================================================================
        // LAYOUT
        // ===================================================================
        private void BuildLayout()
        {
            var left = new Panel
            {
                Dock       = DockStyle.Left,
                Width      = 300,
                BackColor  = SystemColors.Control,
                AutoScroll = true,
                Padding    = new Padding(0, 0, 0, 12),
            };

            int y = 14;

            Section(left, "Paper Size (mm)", ref y);
            _numPaperW = Row(left, "Width:",  ref y, 10, 2000, 151, 0.5m, 1);
            _numPaperH = Row(left, "Height:", ref y, 10, 2000, 151, 0.5m, 1);
            y += 6;

            Section(left, "Print Margins (mm)", ref y);
            _numTop    = Row(left, "Top:",    ref y, 0, 100, 5, 0.5m, 1);
            _numLeft   = Row(left, "Left:",   ref y, 0, 100, 5, 0.5m, 1);
            _numRight  = Row(left, "Right:",  ref y, 0, 100, 5, 0.5m, 1);
            _numBottom = Row(left, "Bottom:", ref y, 0, 100, 5, 0.5m, 1);
            y += 6;

            Section(left, "Font Size Scale", ref y);
            _numFontScale = Row(left, "Scale:", ref y, 0.3m, 4.0m, 1.0m, 0.05m, 2);
            left.Controls.Add(new Label
            {
                Text      = "0.3 = smallest  →  4.0 = largest",
                Location  = new Point(14, y),
                AutoSize  = true,
                Font      = new Font("Arial", 7.5f, FontStyle.Italic),
                ForeColor = Color.DimGray,
            });
            y += 20;
            y += 6;

            Section(left, "Content Position Offset (mm)", ref y);
            _numOffsetX = Row(left, "X (right +):", ref y, -100, 100, 0, 0.5m, 1);
            _numOffsetY = Row(left, "Y (down +):",  ref y, -100, 100, 0, 0.5m, 1);
            y += 6;

            Section(left, "Copies Per Page (A4 or larger)", ref y);
            left.Controls.Add(new Label
            {
                Text     = "Copies:",
                Location = new Point(14, y + 3),
                AutoSize = true,
                Font     = new Font("Arial", 9),
            });
            _cmbCopiesPerPage = new ComboBox
            {
                Location      = new Point(150, y),
                Size          = new Size(88, 22),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font          = new Font("Arial", 9),
            };
            _cmbCopiesPerPage.Items.AddRange(new object[] { 1, 2, 4 });
            _cmbCopiesPerPage.SelectedIndex = 0;
            _cmbCopiesPerPage.SelectedIndexChanged += (s, e) => { if (!_suppressRebuild) RebuildBitmap(); };
            left.Controls.Add(_cmbCopiesPerPage);
            y += 28;
            _lblCopiesHint = new Label
            {
                Text      = "Only available for A4 / Letter or larger paper.",
                Location  = new Point(14, y),
                Size      = new Size(272, 30),
                Font      = new Font("Arial", 7.5f, FontStyle.Italic),
                ForeColor = Color.DimGray,
            };
            left.Controls.Add(_lblCopiesHint);
            y += 34;
            y += 6;

            // ---- BUTTONS ----
            var btnTest = MakeBtn("Print Test Page", Color.LightGreen, new Point(14, y), new Size(272, 38));
            btnTest.Font   = new Font("Arial", 10, FontStyle.Bold);
            btnTest.Click += BtnTest_Click;
            left.Controls.Add(btnTest);
            y += 44;

            var btnGrid = MakeBtn("Print Measuring Grid", Color.LightSkyBlue, new Point(14, y), new Size(272, 34));
            btnGrid.Font   = new Font("Arial", 9, FontStyle.Bold);
            btnGrid.Click += BtnPrintGrid_Click;
            left.Controls.Add(btnGrid);
            y += 38;

            left.Controls.Add(new Label
            {
                Text      = "Prints a ruled sheet at these dimensions. Measure where the "
                          + "lines actually land with a ruler, then correct the margins "
                          + "and offsets above from real numbers.",
                Location  = new Point(14, y),
                Size      = new Size(272, 46),
                Font      = new Font("Arial", 7.5f, FontStyle.Italic),
                ForeColor = Color.DimGray,
            });
            y += 50;

            var btnSave   = MakeBtn("Save Settings", Color.PaleGreen,  new Point(14,  y), new Size(130, 32));
            var btnCancel = MakeBtn("Cancel",         Color.LightCoral, new Point(156, y), new Size(130, 32));
            btnSave.Click   += BtnSave_Click;
            btnCancel.Click += (s, e) => Close();
            left.Controls.Add(btnSave);
            left.Controls.Add(btnCancel);

            _canvas = new PreviewCanvas
            {
                Dock      = DockStyle.Fill,
                BackColor = Color.FromArgb(72, 72, 72),
            };
            _canvas.Paint += Canvas_Paint;

            Controls.Add(_canvas);
            Controls.Add(left);
        }

        private void Section(Panel parent, string title, ref int y)
        {
            parent.Controls.Add(new Label
            {
                Text      = "  " + title,
                Location  = new Point(0, y),
                Size      = new Size(300, 20),
                Font      = new Font("Arial", 9, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(30, 60, 110),
                AutoSize  = false,
                TextAlign = ContentAlignment.MiddleLeft,
            });
            y += 24;
        }

        private NumericUpDown Row(Panel parent, string label,
            ref int y, decimal min, decimal max, decimal val, decimal inc, int dec)
        {
            parent.Controls.Add(new Label
            {
                Text     = label,
                Location = new Point(14, y + 3),
                AutoSize = true,
                Font     = new Font("Arial", 9),
            });
            var n = new NumericUpDown
            {
                Location      = new Point(150, y),
                Size          = new Size(88, 22),
                Minimum       = min,
                Maximum       = max,
                Value         = Math.Max(min, Math.Min(max, val)),
                Increment     = inc,
                DecimalPlaces = dec,
                Font          = new Font("Arial", 9),
            };
            n.ValueChanged += (s, e) => { if (!_suppressRebuild) RebuildBitmap(); };
            parent.Controls.Add(n);
            y += 28;
            return n;
        }

        private static Button MakeBtn(string text, Color back, Point loc, Size size)
            => new Button
            {
                Text      = text,
                Location  = loc,
                Size      = size,
                BackColor = back,
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Arial", 9),
            };

        // ===================================================================
        // LOAD SETTINGS
        // ===================================================================
        private void LoadCurrentSettings()
        {
            _suppressRebuild = true;
            try
            {
                DatabaseManager.PrinterProfileData target = null;
                foreach (var p in DatabaseManager.GetAllPrinterProfiles())
                {
                    if (_presetName != null)
                    {
                        if (p.ProfileName == _presetName) { target = p; break; }
                    }
                    else if (p.IsActive) { target = p; break; }
                }

                if (target != null)
                {
                    Set(_numPaperW,    (decimal)target.WidthMM);
                    Set(_numPaperH,    (decimal)target.HeightMM);
                    Set(_numTop,       (decimal)target.MarginTopMM);
                    Set(_numLeft,      (decimal)target.MarginLeftMM);
                    Set(_numRight,     (decimal)target.MarginRightMM);
                    Set(_numBottom,    (decimal)target.MarginBottomMM);
                    Set(_numFontScale, (decimal)target.SlipFontScale);
                    Set(_numOffsetX,   (decimal)target.SlipOffsetXMm);
                    Set(_numOffsetY,   (decimal)target.SlipOffsetYMm);
                    SetCopies(target.CopiesPerPage);
                }
            }
            finally { /* _suppressRebuild is reset to false by the constructor */ }
        }

        private void Set(NumericUpDown n, decimal val)
            => n.Value = Math.Max(n.Minimum, Math.Min(n.Maximum, val));

        // Sets the ComboBox to 1, 2, or 4.  Treats 3 as 2 (layout removed).
        private void SetCopies(int value)
        {
            if (value == 3) value = 2;
            int idx = _cmbCopiesPerPage.Items.IndexOf(value);
            _cmbCopiesPerPage.SelectedIndex = idx >= 0 ? idx : 0;
        }

        private int GetCopies() => _cmbCopiesPerPage.SelectedItem is int v ? v : 1;

        // ===================================================================
        // COPIES ENABLED STATE
        // ===================================================================
        private bool IsLargeEnoughForMultipleCopies()
            => (double)_numPaperW.Value * (double)_numPaperH.Value >= 55000.0;   // ~A4 area

        private void UpdateCopiesEnabled()
        {
            bool canMulti = IsLargeEnoughForMultipleCopies();
            _cmbCopiesPerPage.Enabled = canMulti;
            _lblCopiesHint.Visible    = !canMulti;
            if (!canMulti && GetCopies() > 1)
            {
                _suppressRebuild = true;
                SetCopies(1);
                _suppressRebuild = false;
            }
        }

        // ===================================================================
        // CANVAS PAINT — three layers
        // ===================================================================
        private void Canvas_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;

            double pageWMm = (double)_numPaperW.Value;
            double pageHMm = (double)_numPaperH.Value;
            if (pageWMm <= 0 || pageHMm <= 0) return;

            float fitScale = Math.Min(
                (_canvas.Width  - 40f) / (float)pageWMm,
                (_canvas.Height - 40f) / (float)pageHMm);

            float pageW = (float)pageWMm * fitScale;
            float pageH = (float)pageHMm * fitScale;
            float pageX = (_canvas.Width  - pageW) / 2f;
            float pageY = (_canvas.Height - pageH) / 2f;
            var pageRect = new RectangleF(pageX, pageY, pageW, pageH);

            // LAYER 1 — page background + grid
            g.FillRectangle(Brushes.Black, pageX + 4, pageY + 4, pageW, pageH);
            g.FillRectangle(Brushes.White, pageRect);

            float step = 10f * fitScale;
            using (var gp = new Pen(Color.FromArgb(200, 200, 200), 1))
            {
                for (float gx = pageX; gx <= pageX + pageW + 0.5f; gx += step)
                    g.DrawLine(gp, gx, pageY, gx, pageY + pageH);
                for (float gy = pageY; gy <= pageY + pageH + 0.5f; gy += step)
                    g.DrawLine(gp, pageX, gy, pageX + pageW, gy);
            }
            using (var bp = new Pen(Color.DarkGray, 2))
                g.DrawRectangle(bp, pageX, pageY, pageW - 1, pageH - 1);

            // LAYER 2 — red printable-area boundary
            float mL = (float)_numLeft.Value   * fitScale;
            float mT = (float)_numTop.Value    * fitScale;
            float mR = (float)_numRight.Value  * fitScale;
            float mB = (float)_numBottom.Value * fitScale;
            float mX = pageX + mL;
            float mY = pageY + mT;
            float mW = pageW - mL - mR;
            float mH = pageH - mT - mB;

            if (mW > 0 && mH > 0)
            {
                using (var rp = new Pen(Color.Red, 1) { DashStyle = DashStyle.Dash })
                    g.DrawRectangle(rp, mX, mY, mW - 1, mH - 1);
            }

            // LAYER 3 — slip bitmap in grid layout matching RenderSlipForPreview
            if (_slipBitmap != null && mW > 0 && mH > 0)
            {
                int copies = GetCopies();
                float offX   = (float)_numOffsetX.Value * fitScale;
                float offY   = (float)_numOffsetY.Value * fitScale;
                float hw     = mW / 2f;
                float hh     = mH / 2f;

                g.SetClip(pageRect);

                if (copies == 1)
                {
                    g.DrawImage(_slipBitmap, new RectangleF(mX + offX, mY + offY, mW, mH));
                }
                else if (copies == 2)
                {
                    // Left and right columns, full height
                    g.DrawImage(_slipBitmap, new RectangleF(mX + offX,        mY + offY, hw, mH));
                    g.DrawImage(_slipBitmap, new RectangleF(mX + offX + hw,   mY + offY, hw, mH));
                    using (var sep = new Pen(Color.Black, 1) { DashStyle = DashStyle.Dash })
                        g.DrawLine(sep, mX + hw, mY, mX + hw, mY + mH);
                }
                else   // 4 copies — full 2×2 grid
                {
                    float[] xs = { mX + offX, mX + offX + hw };
                    float[] ys = { mY + offY, mY + offY + hh };

                    g.DrawImage(_slipBitmap, new RectangleF(xs[0], ys[0], hw, hh));
                    g.DrawImage(_slipBitmap, new RectangleF(xs[1], ys[0], hw, hh));
                    g.DrawImage(_slipBitmap, new RectangleF(xs[0], ys[1], hw, hh));
                    g.DrawImage(_slipBitmap, new RectangleF(xs[1], ys[1], hw, hh));

                    using (var sep = new Pen(Color.Black, 1) { DashStyle = DashStyle.Dash })
                    {
                        g.DrawLine(sep, mX + hw, mY, mX + hw, mY + mH);
                        g.DrawLine(sep, mX, mY + hh, mX + mW, mY + hh);
                    }
                }

                g.ResetClip();
            }
        }

        // ===================================================================
        // BITMAP REBUILD — single-cell bitmap; Canvas_Paint tiles it in the grid
        // ===================================================================
        private void RebuildBitmap()
        {
            if (_numPaperW == null || _cmbCopiesPerPage == null) return;

            UpdateCopiesEnabled();

            int    copies     = GetCopies();
            double printableW = (double)(_numPaperW.Value - _numLeft.Value - _numRight.Value);
            double printableH = (double)(_numPaperH.Value - _numTop.Value  - _numBottom.Value);
            float  fontScale  = (float)_numFontScale.Value;

            // Cell size follows the same grid logic as RenderSlipForPreview:
            //   1 copy  → full printable area
            //   2 copies → half width, full height
            //   4 copies → half width, half height
            double cellW = copies == 1 ? printableW : printableW / 2.0;
            double cellH = copies == 4 ? printableH / 2.0 : printableH;

            _slipBitmap?.Dispose();
            _slipBitmap = null;

            if (cellW > 0 && cellH > 0)
            {
                int wU = Math.Max(1, (int)(cellW / 25.4 * 100));
                int hU = Math.Max(1, (int)(cellH / 25.4 * 100));

                var bmp = new Bitmap(wU, hU);
                bmp.SetResolution(100, 100);

                using (var bg = Graphics.FromImage(bmp))
                {
                    bg.Clear(Color.White);
                    bg.SmoothingMode     = SmoothingMode.AntiAlias;
                    bg.TextRenderingHint = TextRenderingHint.AntiAlias;
                    SlipPrintEngine.RenderSlipContent(
                        bg, new RectangleF(0, 0, wU, hU), _slipData, fontScale);
                }

                _slipBitmap = bmp;
            }

            _canvas?.Invalidate();
        }

        // ===================================================================
        // PRINT TEST PAGE
        // ===================================================================
        private void BtnTest_Click(object sender, EventArgs e)
        {
            try
            {
                var pd = SlipPrintEngine.BuildCalibrationPrintDocument(
                    _slipData,
                    (double)_numPaperW.Value,
                    (double)_numPaperH.Value,
                    (double)_numTop.Value,
                    (double)_numLeft.Value,
                    (double)_numRight.Value,
                    (double)_numBottom.Value,
                    (float)_numFontScale.Value,
                    (float)_numOffsetX.Value,
                    (float)_numOffsetY.Value,
                    GetCopies());

                pd.Print();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Print failed:\n" + ex.Message, "Print Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ===================================================================
        // PRINT MEASURING GRID
        // ===================================================================
        // Prints the ruled calibration sheet rather than a slip: a 10mm grid, 5mm ticks down
        // the top and left edges, the page boundary, the margin boundary in red, and crosshairs
        // at the four margin corners and the centre.
        //
        // Unlike the slip renderer, this one deliberately draws from ev.PageBounds and
        // ev.MarginBounds — the printer driver's own idea of the page. That is the whole point:
        // comparing where those lines physically land against a ruler is what reveals the
        // mechanical offset the driver does not report.
        private void BtnPrintGrid_Click(object sender, EventArgs e)
        {
            try
            {
                string printerName  = DatabaseManager.GetGlobalSetting("SelectedPrinter", "EPSON LX-350");
                string paperProfile = DatabaseManager.GetGlobalSetting("PaperSizeProfile", PaperSizeHelper.DefaultProfile);
                string orientStr    = DatabaseManager.GetGlobalSetting("PrintOrientation", "Portrait");
                bool   isLandscape  = orientStr.Equals("Landscape", StringComparison.OrdinalIgnoreCase);

                // Margins cross into System.Drawing.Printing as hundredths of an inch
                int Units(decimal mm) => (int)((double)mm / 25.4 * 100);

                var pd = SlipPrintEngine.BuildCalibrationOnlyDocument(
                    printerName, paperProfile,
                    (double)_numPaperW.Value, (double)_numPaperH.Value, isLandscape,
                    Units(_numLeft.Value), Units(_numRight.Value),
                    Units(_numTop.Value),  Units(_numBottom.Value));

                pd.Print();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not print the measuring grid:\n\n" + ex.Message,
                    "Print Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ===================================================================
        // SAVE SETTINGS
        // ===================================================================
        private void BtnSave_Click(object sender, EventArgs e)
        {
            try
            {
                DatabaseManager.PrinterProfileData target = null;
                foreach (var p in DatabaseManager.GetAllPrinterProfiles())
                {
                    if (_presetName != null)
                    {
                        if (p.ProfileName == _presetName) { target = p; break; }
                    }
                    else if (p.IsActive) { target = p; break; }
                }

                if (target != null)
                {
                    // Paper/margin save (upsert; preserves calibration columns on conflict)
                    DatabaseManager.SaveOrUpdatePrinterProfile(
                        target.ProfileName, target.PrinterName, target.PaperSizeProfile,
                        (double)_numPaperW.Value, (double)_numPaperH.Value,
                        (double)_numTop.Value,    (double)_numLeft.Value,
                        (double)_numRight.Value,  (double)_numBottom.Value,
                        target.NumCopies, target.Orientation, target.SlipLengthIn);

                    // Calibration save — targeted UPDATE of the four per-preset columns
                    DatabaseManager.SaveCalibrationToProfile(
                        target.ProfileName,
                        (float)_numFontScale.Value,
                        (float)_numOffsetX.Value,
                        (float)_numOffsetY.Value,
                        GetCopies());
                }

                string savedTo = target?.ProfileName ?? "(no active profile)";
                MessageBox.Show($"Calibration settings saved to \"{savedTo}\".", "Saved",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to save:\n" + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ===================================================================
        // SAMPLE DATA — shown when no real slip is passed in
        // ===================================================================
        private static Dictionary<string, string> SampleData()
        {
            return new Dictionary<string, string>
            {
                ["SlipID"]     = "1234",
                ["BillNumber"] = "BN-2026-001",
                ["Field1"]     = "ABC 123 GP",
                ["Field2"]     = "Stockpile A",
                ["Field3"]     = "ROM Type 1",
                ["Field4"]     = "Block 5",
                ["Field5"]     = "Fine",
                ["Field6"]     = "Johannesburg",
                ["Field7"]     = "28.50",
                ["Field8"]     = "ORD-0042",
                ["Field9"]     = "Slot 3",
                ["Field10"]    = "Client ABC",
                ["PrintedAt"]  = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
            };
        }
    }
}
