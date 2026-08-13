using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace SlipManagement2
{
    public partial class PrintSlipPreview : Form
    {
        private readonly Dictionary<string, string> _slipData;

        private double _pageWidthMm;
        private double _pageHeightMm;

        private PreviewCanvas _canvas;
        private Bitmap        _slipBitmap;

        private sealed class PreviewCanvas : Panel
        {
            public PreviewCanvas() { DoubleBuffered = true; }
        }

        public PrintSlipPreview(Dictionary<string, string> slipData)
        {
            InitializeComponent();
            _slipData = slipData;

            pnlSlip.Visible = false;

            Text            = "Print Preview";
            ClientSize      = new Size(900, 650);
            StartPosition   = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox     = false;

            (_pageWidthMm, _pageHeightMm) = SlipPrintEngine.GetPageDimensionsMm();

            BuildLayout();
            RebuildSlipBitmap();
        }

        // ===================================================================
        // LAYOUT
        // ===================================================================
        private void BuildLayout()
        {
            var buttonPanel = new Panel
            {
                Dock      = DockStyle.Bottom,
                Height    = 60,
                BackColor = SystemColors.Control
            };

            int bx = 30;
            var btnPrint = MakeButton("Print",              bx, Color.LightGreen,   130); bx += 150;
            var btnCal   = MakeButton("Calibrate Print",    bx, Color.LightSkyBlue, 160); bx += 180;
            var btnCncl  = MakeButton("Cancel",             bx, Color.LightCoral,   130);

            btnPrint.Click += BtnPrint_Click;
            btnCal.Click   += BtnCalibrate_Click;
            btnCncl.Click  += (s, e) => { DialogResult = System.Windows.Forms.DialogResult.Cancel; Close(); };

            buttonPanel.Controls.Add(btnPrint);
            buttonPanel.Controls.Add(btnCal);
            buttonPanel.Controls.Add(btnCncl);

            _canvas = new PreviewCanvas
            {
                Dock      = DockStyle.Fill,
                BackColor = Color.FromArgb(80, 80, 80),
            };
            _canvas.Paint += Canvas_Paint;

            Controls.Add(_canvas);
            Controls.Add(buttonPanel);
        }

        // ===================================================================
        // CANVAS PAINT — display only (no controls)
        // ===================================================================
        private void Canvas_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;

            float fitScale = Math.Min(
                (_canvas.Width  - 40f) / (float)_pageWidthMm,
                (_canvas.Height - 40f) / (float)_pageHeightMm);

            float pageW = (float)_pageWidthMm  * fitScale;
            float pageH = (float)_pageHeightMm * fitScale;
            float pageX = (_canvas.Width  - pageW) / 2f;
            float pageY = (_canvas.Height - pageH) / 2f;
            var pageRect = new RectangleF(pageX, pageY, pageW, pageH);

            // shadow
            g.FillRectangle(Brushes.Black, pageX + 4, pageY + 4, pageW, pageH);
            // page
            g.FillRectangle(Brushes.White, pageRect);

            // grid every 10mm
            float step = 10f * fitScale;
            using (var gridPen = new Pen(Color.FromArgb(200, 200, 200), 1))
            {
                for (float gx = pageX; gx <= pageX + pageW + 0.5f; gx += step)
                    g.DrawLine(gridPen, gx, pageY, gx, pageY + pageH);
                for (float gy = pageY; gy <= pageY + pageH + 0.5f; gy += step)
                    g.DrawLine(gridPen, pageX, gy, pageX + pageW, gy);
            }

            using (var pagePen = new Pen(Color.DarkGray, 2))
                g.DrawRectangle(pagePen, pageX, pageY, pageW - 1, pageH - 1);

            // margin boundary (from saved profile)
            var profile = DatabaseManager.GetActiveProfile();
            float mLeft   = (float)profile.MarginLeftMM   * fitScale;
            float mTop    = (float)profile.MarginTopMM    * fitScale;
            float mRight  = (float)profile.MarginRightMM  * fitScale;
            float mBottom = (float)profile.MarginBottomMM * fitScale;
            float mX = pageX + mLeft;
            float mY = pageY + mTop;
            float mW = pageW - mLeft - mRight;
            float mH = pageH - mTop  - mBottom;

            if (mW > 0 && mH > 0)
            {
                using (var redPen = new Pen(Color.Red, 1) { DashStyle = DashStyle.Dash })
                    g.DrawRectangle(redPen, mX, mY, mW - 1, mH - 1);

                if (_slipBitmap != null)
                {
                    // Apply the profile's calibration offsets, exactly as the print job does
                    // in RenderSlipFromDimensions — otherwise a calibrated slip previews
                    // centred but prints shifted.
                    float offX = profile.SlipOffsetXMm * fitScale;
                    float offY = profile.SlipOffsetYMm * fitScale;

                    g.SetClip(pageRect);
                    g.DrawImage(_slipBitmap, new RectangleF(mX + offX, mY + offY, mW, mH));
                    g.ResetClip();
                }
            }
        }

        // ===================================================================
        // SLIP BITMAP
        // ===================================================================
        private void RebuildSlipBitmap()
        {
            var profile = DatabaseManager.GetActiveProfile();
            double leftMm   = profile.MarginLeftMM;
            double topMm    = profile.MarginTopMM;
            double rightMm  = profile.MarginRightMM;
            double bottomMm = profile.MarginBottomMM;

            double slipWMm = _pageWidthMm  - leftMm - rightMm;
            double slipHMm = _pageHeightMm - topMm  - bottomMm;

            if (slipWMm <= 0 || slipHMm <= 0)
            {
                _slipBitmap?.Dispose();
                _slipBitmap = null;
                _canvas?.Invalidate();
                return;
            }

            // Font scale comes from the active profile — the same value BuildPrintDocument
            // passes to the real print job. It used to be read from GlobalSettings["SlipFontScale"],
            // which stopped being written when calibration moved into PrinterProfiles. Existing
            // databases still hold that orphaned value, so the preview kept rendering at the old
            // pre-refactor scale while the printer used the profile's — e.g. 2.75 vs 1.95.
            float fontScale = profile.SlipFontScale;

            int wU = Math.Max(1, (int)(slipWMm / 25.4 * 100));
            int hU = Math.Max(1, (int)(slipHMm / 25.4 * 100));

            var bmp = new Bitmap(wU, hU);
            bmp.SetResolution(100, 100);

            using (var bg = Graphics.FromImage(bmp))
            {
                bg.Clear(Color.White);
                bg.SmoothingMode     = SmoothingMode.AntiAlias;
                bg.TextRenderingHint = TextRenderingHint.AntiAlias;
                SlipPrintEngine.RenderSlipForPreview(
                    bg, new RectangleF(0, 0, wU, hU), _slipData, fontScale);
            }

            _slipBitmap?.Dispose();
            _slipBitmap = bmp;
            _canvas?.Invalidate();
        }

        // ===================================================================
        // BUTTONS
        // ===================================================================
        private void BtnPrint_Click(object sender, EventArgs e)
        {
            try
            {
                var pd = SlipPrintEngine.BuildPrintDocument(_slipData);

                // Backing out of the Save dialog when printing to PDF must not count as a
                // print. Otherwise the slip is marked Printed with no document to show for
                // it — the one thing a proof-of-record system cannot be allowed to do.
                if (!SlipPrintEngine.TryPrepareFileOutput(pd, this)) return;

                pd.Print();
                DialogResult = System.Windows.Forms.DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Print failed: " + ex.Message, "Printer Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnCalibrate_Click(object sender, EventArgs e)
        {
            using (var cal = new PrintCalibrationForm(_slipData))
                cal.ShowDialog(this);

            // Refresh after calibration in case settings changed
            (_pageWidthMm, _pageHeightMm) = SlipPrintEngine.GetPageDimensionsMm();
            RebuildSlipBitmap();
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
    }
}
