using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Printing;
using System.Windows.Forms;

namespace SlipManagement2
{
    public static class SlipPrintEngine
    {
        // ===================================================================
        // DOCUMENT BUILDERS
        // ===================================================================

        // Builds a slip PrintDocument using all calibration settings from the active profile.
        public static PrintDocument BuildPrintDocument(Dictionary<string, string> slipData)
        {
            // Read everything from the active profile once, outside the PrintPage event.
            var profile            = DatabaseManager.GetActiveProfile();
            var (pageWMm, pageHMm) = GetPageDimensionsMm();

            var pd = BuildBaseDocument();
            pd.PrintPage += (s, ev) =>
                RenderSlipFromDimensions(ev, slipData,
                    pageWMm, pageHMm,
                    profile.MarginLeftMM, profile.MarginTopMM,
                    profile.MarginRightMM, profile.MarginBottomMM,
                    profile.SlipFontScale, profile.SlipOffsetXMm, profile.SlipOffsetYMm);
            return pd;
        }

        // Builds a calibration-test PrintDocument with explicit parameters.
        // Used by PrintCalibrationForm to test settings before saving.
        public static PrintDocument BuildCalibrationPrintDocument(
            Dictionary<string, string> slipData,
            double pageWidthMm, double pageHeightMm,
            double marginTopMm, double marginLeftMm,
            double marginRightMm, double marginBottomMm,
            float fontScale = 1.0f,
            float contentOffsetXMm = 0f, float contentOffsetYMm = 0f,
            int copiesPerPage = 0)
        {
            var pd = new PrintDocument();

            string printerName = DatabaseManager.GetGlobalSetting("SelectedPrinter",  "EPSON LX-350");
            string orientStr   = DatabaseManager.GetGlobalSetting("PrintOrientation", "Portrait");
            string copiesStr   = DatabaseManager.GetGlobalSetting("PrintCopiesCount", "1");

            pd.PrinterSettings.PrinterName = printerName;
            if (short.TryParse(copiesStr, out short copies)) pd.PrinterSettings.Copies = copies;

            bool isLandscape = orientStr.Equals("Landscape", StringComparison.OrdinalIgnoreCase);
            pd.DefaultPageSettings.Landscape = isLandscape;
            pd.PrinterSettings.DefaultPageSettings.Landscape = isLandscape;

            int wU = (int)(pageWidthMm  / 25.4 * 100);
            int hU = (int)(pageHeightMm / 25.4 * 100);
            pd.DefaultPageSettings.PaperSize = new PaperSize("Calibration", wU, hU);
            pd.DefaultPageSettings.Margins   = new Margins(
                (int)(marginLeftMm   / 25.4 * 100), (int)(marginRightMm  / 25.4 * 100),
                (int)(marginTopMm    / 25.4 * 100), (int)(marginBottomMm / 25.4 * 100));

            pd.PrintPage += (s, ev) =>
                RenderSlipFromDimensions(ev, slipData,
                    pageWidthMm, pageHeightMm,
                    marginLeftMm, marginTopMm, marginRightMm, marginBottomMm,
                    fontScale, contentOffsetXMm, contentOffsetYMm, copiesPerPage);
            return pd;
        }

        // Builds a calibration-grid-only document (used by PrinterSettingsForm).
        public static PrintDocument BuildCalibrationOnlyDocument(
            string printerName, string paperProfile,
            double widthMm, double heightMm, bool isLandscape,
            int mLeft, int mRight, int mTop, int mBottom)
        {
            var pd = new PrintDocument();
            pd.PrinterSettings.PrinterName = printerName;
            pd.DefaultPageSettings.Landscape = isLandscape;
            pd.PrinterSettings.DefaultPageSettings.Landscape = isLandscape;
            int wu = (int)(widthMm  / 25.4 * 100);
            int hu = (int)(heightMm / 25.4 * 100);
            pd.DefaultPageSettings.PaperSize = new PaperSize(paperProfile, wu, hu);
            pd.DefaultPageSettings.Margins   = new Margins(mLeft, mRight, mTop, mBottom);
            pd.PrintPage += (s, ev) => RenderCalibrationGrid(ev);
            return pd;
        }

        // Sends directly to the printer using current DB settings.
        public static void ExecutePrintJob(Dictionary<string, string> slipData)
        {
            try   { BuildPrintDocument(slipData).Print(); }
            catch (Exception ex)
            {
                MessageBox.Show("Print failed: " + ex.Message, "Printer Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ===================================================================
        // BASE DOCUMENT SETUP
        // ===================================================================
        private static PrintDocument BuildBaseDocument(
            double marginTopMm = 0, double marginLeftMm = 0,
            double marginRightMm = 0, double marginBottomMm = 0)
        {
            PrintDocument pd = new PrintDocument();

            string printerName  = DatabaseManager.GetGlobalSetting("SelectedPrinter",  "EPSON LX-350");
            string paperProfile = DatabaseManager.GetGlobalSetting("PaperSizeProfile",  "Small240x102");
            string orientStr    = DatabaseManager.GetGlobalSetting("PrintOrientation",  "Portrait");
            string copiesStr    = DatabaseManager.GetGlobalSetting("PrintCopiesCount",  "1");

            pd.PrinterSettings.PrinterName = printerName;
            if (short.TryParse(copiesStr, out short copies))
                pd.PrinterSettings.Copies = copies;

            bool isLandscape = orientStr.Equals("Landscape", StringComparison.OrdinalIgnoreCase);
            pd.DefaultPageSettings.Landscape = isLandscape;
            pd.PrinterSettings.DefaultPageSettings.Landscape = isLandscape;

            // Use profile's stored dimensions so calibrated sizes are respected
            var (widthMm, heightMm) = GetPageDimensionsMm();
            int widthUnits  = (int)(widthMm  / 25.4 * 100);
            int heightUnits = (int)(heightMm / 25.4 * 100);
            pd.DefaultPageSettings.PaperSize = new PaperSize(paperProfile, widthUnits, heightUnits);

            double tMm, lMm, rMm, bMm;
            if (marginTopMm == 0 && marginLeftMm == 0 && marginRightMm == 0 && marginBottomMm == 0)
            {
                var profile = DatabaseManager.GetActiveProfile();
                tMm = profile.MarginTopMM;  lMm = profile.MarginLeftMM;
                rMm = profile.MarginRightMM; bMm = profile.MarginBottomMM;
            }
            else
            {
                tMm = marginTopMm; lMm = marginLeftMm;
                rMm = marginRightMm; bMm = marginBottomMm;
            }

            pd.DefaultPageSettings.Margins = new Margins(
                (int)(lMm / 25.4 * 100), (int)(rMm / 25.4 * 100),
                (int)(tMm / 25.4 * 100), (int)(bMm / 25.4 * 100));

            return pd;
        }

        // ===================================================================
        // CORE RENDERER  — positions content using EXPLICIT mm dimensions,
        // NOT ev.MarginBounds (which varies with the printer driver's paper).
        // ===================================================================
        private static void RenderSlipFromDimensions(PrintPageEventArgs ev,
            Dictionary<string, string> slipData,
            double pageWidthMm, double pageHeightMm,
            double leftMm, double topMm, double rightMm, double bottomMm,
            float fontScale, float contentOffsetXMm, float contentOffsetYMm,
            int copiesPerPage = 0)
        {
            float leftU = (float)(leftMm  / 25.4 * 100);
            float topU  = (float)(topMm   / 25.4 * 100);
            float areaW = (float)((pageWidthMm  - leftMm - rightMm)  / 25.4 * 100);
            float areaH = (float)((pageHeightMm - topMm  - bottomMm) / 25.4 * 100);
            float offXU = contentOffsetXMm / 25.4f * 100f;
            float offYU = contentOffsetYMm / 25.4f * 100f;

            if (areaW <= 0 || areaH <= 0) { ev.HasMorePages = false; return; }

            // Clip to declared page size so content never bleeds regardless of driver paper
            float pgW = (float)(pageWidthMm  / 25.4 * 100);
            float pgH = (float)(pageHeightMm / 25.4 * 100);
            ev.Graphics.SetClip(new RectangleF(0, 0, pgW, pgH));

            var area = new RectangleF(leftU + offXU, topU + offYU, areaW, areaH);
            RenderSlipForPreview(ev.Graphics, area, slipData, fontScale, copiesPerPage);

            ev.Graphics.ResetClip();
            ev.HasMorePages = false;
        }

        // ===================================================================
        // CALIBRATION GRID RENDERER  (shared by overlay and standalone page)
        // ===================================================================
        internal static void RenderCalibrationGrid(PrintPageEventArgs ev)
        {
            Graphics g = ev.Graphics;
            float MmToU(double mm) => (float)(mm / 25.4 * 100);

            using (var gridPen = new Pen(Color.FromArgb(210, 210, 210), 1))
            {
                float step = MmToU(10);
                for (float gx = ev.PageBounds.Left; gx <= ev.PageBounds.Right;  gx += step)
                    g.DrawLine(gridPen, gx, ev.PageBounds.Top, gx, ev.PageBounds.Bottom);
                for (float gy = ev.PageBounds.Top;  gy <= ev.PageBounds.Bottom; gy += step)
                    g.DrawLine(gridPen, ev.PageBounds.Left, gy, ev.PageBounds.Right, gy);
            }

            using (var pagePen = new Pen(Color.DarkGray, 2))
                g.DrawRectangle(pagePen, ev.PageBounds.X, ev.PageBounds.Y,
                    ev.PageBounds.Width - 1, ev.PageBounds.Height - 1);

            using (var marginPen = new Pen(Color.Red, 1) { DashStyle = DashStyle.Dash })
                g.DrawRectangle(marginPen, ev.MarginBounds.X, ev.MarginBounds.Y,
                    ev.MarginBounds.Width - 1, ev.MarginBounds.Height - 1);

            float tick5 = MmToU(5);
            using (var tickPen = new Pen(Color.Black, 1))
            {
                int idx = 0;
                for (float rx = ev.PageBounds.Left; rx <= ev.PageBounds.Right; rx += tick5, idx++)
                {
                    float len = MmToU(idx % 2 == 0 ? 4 : 2);
                    g.DrawLine(tickPen, rx, ev.PageBounds.Top, rx, ev.PageBounds.Top + len);
                }
                idx = 0;
                for (float ry = ev.PageBounds.Top; ry <= ev.PageBounds.Bottom; ry += tick5, idx++)
                {
                    float len = MmToU(idx % 2 == 0 ? 4 : 2);
                    g.DrawLine(tickPen, ev.PageBounds.Left, ry, ev.PageBounds.Left + len, ry);
                }
            }

            float arm = MmToU(8);
            using (var crossPen = new Pen(Color.DarkBlue, 2))
            {
                DrawCrosshair(g, crossPen, ev.MarginBounds.Left,  ev.MarginBounds.Top,    arm);
                DrawCrosshair(g, crossPen, ev.MarginBounds.Right, ev.MarginBounds.Top,    arm);
                DrawCrosshair(g, crossPen, ev.MarginBounds.Left,  ev.MarginBounds.Bottom, arm);
                DrawCrosshair(g, crossPen, ev.MarginBounds.Right, ev.MarginBounds.Bottom, arm);
            }

            float cx = ev.MarginBounds.Left + ev.MarginBounds.Width  / 2f;
            float cy = ev.MarginBounds.Top  + ev.MarginBounds.Height / 2f;
            using (var centrePen = new Pen(Color.Green, 2))
                DrawCrosshair(g, centrePen, cx, cy, MmToU(15));
        }

        internal static void DrawCrosshair(Graphics g, Pen pen, float cx, float cy, float arm)
        {
            g.DrawLine(pen, cx - arm, cy, cx + arm, cy);
            g.DrawLine(pen, cx, cy - arm, cx, cy + arm);
        }

        // ===================================================================
        // SLIP CONTENT RENDERER
        // ===================================================================
        internal static void RenderSlipContent(Graphics g, RectangleF area,
            Dictionary<string, string> slipData, float userFontScale = 1.0f)
        {
            float x = area.X;
            float y = area.Y;
            float w = area.Width;

            // widthMm converts printer units (100/inch) to mm; then apply user multiplier
            float widthMm  = w * 0.254f;
            float fontScale = Math.Max(0.3f, Math.Min(4.0f, (widthMm / 200f) * userFontScale));

            var fieldCfgs = DatabaseManager.GetActiveFieldConfigurations();

            // ---- HEADER ----
            string company  = DatabaseManager.GetGlobalSetting("HeaderTitle", "SLIP MANAGEMENT SYSTEM");
            string logoPath = DatabaseManager.GetGlobalSetting("LogoPath", "");

            if (!string.IsNullOrEmpty(logoPath) && System.IO.File.Exists(logoPath))
            {
                try
                {
                    using (var img = Image.FromFile(logoPath))
                    {
                        float logoH = (float)(15 / 25.4 * 100);   // 15mm
                        float logoW = img.Width * (logoH / img.Height);
                        g.DrawImage(img, x + (w - logoW) / 2f, y, logoW, logoH);
                        y += logoH + 4;
                    }
                }
                catch { }
            }

            using (var hFont = new Font("Arial", 13 * fontScale, FontStyle.Bold))
            {
                var sfCentre = new StringFormat { Alignment = StringAlignment.Center };
                var hRect    = new RectangleF(x, y, w, hFont.GetHeight(g) + 4);
                g.DrawString(company, hFont, Brushes.Black, hRect, sfCentre);
                y += hFont.GetHeight(g) + 5;
            }

            using (var thick = new Pen(Color.Black, 2)) g.DrawLine(thick, x, y, x + w, y);
            y += 4;

            // ---- REFERENCE ROW (SlipID left, BillNumber right) ----
            using (var refFont = new Font("Arial", 8 * fontScale, FontStyle.Bold))
            {
                float lh    = refFont.GetHeight(g) + 2;
                var sfRight = new StringFormat { Alignment = StringAlignment.Far };
                g.DrawString($"Slip #: {V(slipData, "SlipID")}", refFont, Brushes.Black, x + 2, y);
                g.DrawString($"Bill: {V(slipData, "BillNumber")}", refFont, Brushes.Black,
                    new RectangleF(x, y, w - 2, lh), sfRight);
                y += lh + 2;
            }

            using (var thin = new Pen(Color.DarkGray, 1)) g.DrawLine(thin, x, y, x + w, y);
            y += 5;

            // ---- TWO-COLUMN BODY ----
            float labelColW  = w * 0.38f;
            float valueColX  = x + labelColW + 5;
            float valueColW  = w - labelColW - 7;
            float bodyStartY = y;

            using (var lblFont = new Font("Arial", 8.5f * fontScale))
            using (var valFont = new Font("Arial", 8.5f * fontScale, FontStyle.Bold))
            {
                float lineH = lblFont.GetHeight(g) + 3;
                var sfTrim  = new StringFormat { Trimming = StringTrimming.EllipsisCharacter };

                for (int i = 1; i <= 10; i++)
                {
                    string key = "Field" + i;
                    if (key == "Field7") continue;
                    if (!fieldCfgs.TryGetValue(key, out var cfg) || cfg.IsHidden) continue;

                    string lbl = string.IsNullOrWhiteSpace(cfg.CustomName) ? $"Field {i}" : cfg.CustomName;
                    g.DrawString(lbl + ":", lblFont, Brushes.Black,
                        new RectangleF(x + 2, y, labelColW - 4, lineH), sfTrim);
                    g.DrawString(V(slipData, key), valFont, Brushes.Black,
                        new RectangleF(valueColX, y, valueColW, lineH), sfTrim);
                    y += lineH;
                }
            }

            using (var divPen = new Pen(Color.LightGray, 1))
                g.DrawLine(divPen, x + labelColW + 2, bodyStartY, x + labelColW + 2, y);

            // ---- TONS SECTION ----
            y += 5;
            using (var thick = new Pen(Color.Black, 2)) g.DrawLine(thick, x, y, x + w, y);
            y += 5;

            if (fieldCfgs.TryGetValue("Field7", out var cfg7))
            {
                string f7Label = string.IsNullOrWhiteSpace(cfg7.CustomName) ? "Tons" : cfg7.CustomName;
                string tons    = V(slipData, "Field7");

                using (var tLblFont = new Font("Arial", 9  * fontScale, FontStyle.Bold))
                using (var tValFont = new Font("Arial", 20 * fontScale, FontStyle.Bold))
                {
                    g.DrawString(f7Label.ToUpper() + ":", tLblFont, Brushes.Black, x + 2, y);

                    string tStr    = $"{tons} t";
                    var sfCentre   = new StringFormat { Alignment = StringAlignment.Center };
                    float valH     = tValFont.GetHeight(g) + 4;
                    g.DrawString(tStr, tValFont, Brushes.Black,
                        new RectangleF(x, y + tLblFont.GetHeight(g) + 2, w, valH), sfCentre);
                    y += tLblFont.GetHeight(g) + 2 + valH + 4;
                }
            }

            using (var thick = new Pen(Color.Black, 2)) g.DrawLine(thick, x, y, x + w, y);
            y += 5;

            // ---- TIMESTAMP ----
            // Prefer the slip's recorded first-print date, so a reprint carries the same date as
            // the original sheet and as the database row. Only a slip being printed for the very
            // first time has no PrintedAt yet — for that one, "now" IS the first print date.
            string printedAt = V(slipData, "PrintedAt");
            if (string.IsNullOrWhiteSpace(printedAt) || printedAt == "----")
                printedAt = DateTime.Now.ToString("yyyy-MM-dd  HH:mm");
            else if (DateTime.TryParse(printedAt, out DateTime parsed))
                printedAt = parsed.ToString("yyyy-MM-dd  HH:mm");

            using (var tsFont = new Font("Arial", 7 * fontScale, FontStyle.Italic))
                g.DrawString($"Printed: {printedAt}", tsFont, Brushes.Black, x + 2, y);

            // ---- SIGNATURE LINE ----
            float sigLineY = area.Bottom - (float)(10 / 25.4 * 100);
            if (sigLineY > y + 15)
            {
                using (var sigFont = new Font("Arial", 8 * fontScale))
                using (var sp     = new Pen(Color.Black, 1))
                {
                    float sigLineW = w * 0.58f;
                    g.DrawLine(sp, x, sigLineY, x + sigLineW, sigLineY);
                    g.DrawString("Operator Signature", sigFont, Brushes.Black, x, sigLineY + 2);
                }
            }

        }

        // ===================================================================
        // PREVIEW HELPERS  (owner-draw canvas in PrintSlipPreview/Calibration)
        // ===================================================================

        // Returns the active profile's stored page dimensions, or falls back to
        // the paper-profile switch.  Always call this rather than hard-coding.
        internal static (double widthMm, double heightMm) GetPageDimensionsMm()
        {
            var profiles = DatabaseManager.GetAllPrinterProfiles();
            foreach (var p in profiles)
                if (p.IsActive && p.WidthMM > 0 && p.HeightMM > 0)
                    return (p.WidthMM, p.HeightMM);

            // Fallback
            string paperProfile = DatabaseManager.GetGlobalSetting("PaperSizeProfile", PaperSizeHelper.DefaultProfile);
            string customLen    = DatabaseManager.GetGlobalSetting("SlipCustomLength", "5.5");
            if (!double.TryParse(customLen, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out double lenIn))
                lenIn = 5.5;

            return PaperSizeHelper.GetDimensionsMm(paperProfile, lenIn);
        }

        // Renders the slip content into slipAreaUnits using the copies-per-page grid layout:
        //   1 copy  → full area
        //   2 copies → split on X axis (left | right column), full height each
        //   4 copies → 2×2 grid, all four cells filled
        // 3-copies is not supported (asymmetric layout removed); any caller passing 3
        // is treated as 2. copiesPerPage = 0 reads from the active profile.
        internal static void RenderSlipForPreview(
            Graphics g, RectangleF slipAreaUnits,
            Dictionary<string, string> slipData, float fontScale = 1.0f,
            int copiesPerPage = 0)
        {
            if (copiesPerPage <= 0)
                copiesPerPage = DatabaseManager.GetActiveProfile().CopiesPerPage;

            // Normalise: only 1, 2, 4 are valid; treat 3 as 2, clamp anything else.
            if (copiesPerPage == 3) copiesPerPage = 2;
            copiesPerPage = copiesPerPage < 1 ? 1 : copiesPerPage > 4 ? 4 : copiesPerPage;

            if (copiesPerPage == 1)
            {
                RenderSlipContent(g, slipAreaUnits, slipData, fontScale);
                return;
            }

            float ax = slipAreaUnits.X;
            float ay = slipAreaUnits.Y;
            float aw = slipAreaUnits.Width;
            float ah = slipAreaUnits.Height;
            float hw = aw / 2f;
            float hh = ah / 2f;
            float gU = (float)(1.0 / 25.4 * 100);   // 1 mm half-gap each side of divider

            if (copiesPerPage == 2)
            {
                // Two columns, full height
                RenderSlipContent(g, new RectangleF(ax,           ay, hw - gU, ah), slipData, fontScale);
                RenderSlipContent(g, new RectangleF(ax + hw + gU, ay, hw - gU, ah), slipData, fontScale);

                float midX = ax + hw;
                using (var p = new Pen(Color.Black, 1) { DashStyle = DashStyle.Dash })
                    g.DrawLine(p, midX, ay, midX, ay + ah);
            }
            else   // 4 copies — full 2×2 grid
            {
                RectangleF Cell(int col, int row)
                    => new RectangleF(ax + col * hw + (col == 0 ? 0 : gU),
                                      ay + row * hh + (row == 0 ? 0 : gU),
                                      hw - gU, hh - gU);

                RenderSlipContent(g, Cell(0, 0), slipData, fontScale);
                RenderSlipContent(g, Cell(1, 0), slipData, fontScale);
                RenderSlipContent(g, Cell(0, 1), slipData, fontScale);
                RenderSlipContent(g, Cell(1, 1), slipData, fontScale);

                float midX = ax + hw;
                float midY = ay + hh;
                using (var p = new Pen(Color.Black, 1) { DashStyle = DashStyle.Dash })
                {
                    g.DrawLine(p, midX, ay,  midX, ay + ah);
                    g.DrawLine(p, ax,   midY, ax + aw, midY);
                }
            }
        }

        private static string V(Dictionary<string, string> d, string key)
            => d.TryGetValue(key, out var v) ? v : "----";
    }
}
