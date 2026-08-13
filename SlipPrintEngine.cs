using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Printing;
using System.Linq;
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
            pd.DocumentName = BuildDocumentName(slipData);
            pd.PrintPage += (s, ev) =>
                RenderSlipFromDimensions(ev, slipData,
                    pageWMm, pageHMm,
                    profile.MarginLeftMM, profile.MarginTopMM,
                    profile.MarginRightMM, profile.MarginBottomMM,
                    profile.SlipFontScale, profile.SlipOffsetXMm, profile.SlipOffsetYMm,
                    profile.CopiesPerPage, profile.MultiSlipLayout);
            return pd;
        }

        // The job name, which is what "Microsoft Print to PDF" offers as the default filename in
        // its Save dialog. Without it every slip lands as "SlipManagement2.exe" and the operator
        // has to invent a name for each one. Bill number first because it is unique and sorts by
        // date; then the leading field's value — Truck Reg by default — so a folder of PDFs can
        // be scanned by eye. The operator can still overwrite it in the dialog.
        private static string BuildDocumentName(Dictionary<string, string> slipData)
        {
            string bill = V(slipData, "BillNumber");
            if (bill == "----") bill = "Slip";

            // The leading field by the operator's own ordering, not necessarily Field1 — if they
            // reordered the form, the first thing they see is the first thing they will look for.
            string lead = "";
            var cfgs = DatabaseManager.GetActiveFieldConfigurations();
            foreach (var kvp in cfgs.OrderBy(k => k.Value.PositionOrder))
            {
                if (kvp.Value.IsHidden || kvp.Key == "Field7") continue;
                if (slipData.TryGetValue(kvp.Key, out string v) && !string.IsNullOrWhiteSpace(v))
                { lead = v.Trim(); break; }
            }

            string name = string.IsNullOrEmpty(lead) ? bill : bill + " - " + lead;

            // Strip what Windows will not accept in a filename, so the Save dialog opens with a
            // name it can actually use rather than one the operator has to correct.
            foreach (char c in System.IO.Path.GetInvalidFileNameChars())
                name = name.Replace(c, ' ');
            name = System.Text.RegularExpressions.Regex.Replace(name, @"\s+", " ").Trim();

            return name.Length > 80 ? name.Substring(0, 80).Trim() : name;
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
            int copiesPerPage = 0, string multiSlipLayout = null)
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
                    fontScale, contentOffsetXMm, contentOffsetYMm, copiesPerPage, multiSlipLayout);
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

        // "Microsoft Print to PDF" and the XPS writer collect a filename through their own Save
        // dialog, and that dialog ignores DocumentName — it opens with the box empty no matter
        // what the job is called. Setting DocumentName only renames the job, which is why it
        // shows in the Printing progress window but not in the Save box.
        //
        // So we ask first, with the name already filled in, and hand the driver the chosen path.
        // Given a path it writes straight to it and never shows its own dialog, so the operator
        // still sees exactly one Save box — just one that arrives pre-named.
        private static bool IsFileOutputPrinter(string printerName)
        {
            if (string.IsNullOrEmpty(printerName)) return false;
            string n = printerName.ToLowerInvariant();
            // Only the two drivers we know honour PrintFileName. A third-party PDF printer that
            // ignored it would show its own dialog after ours, which is worse than leaving it be.
            return n.Contains("print to pdf") || n.Contains("xps document writer");
        }

        // Returns false if the operator cancelled — the caller must then not treat the slip as
        // printed. Nothing to do for a real printer, which returns true untouched.
        public static bool TryPrepareFileOutput(PrintDocument pd, IWin32Window owner)
        {
            if (pd == null) return false;
            if (!IsFileOutputPrinter(pd.PrinterSettings.PrinterName)) return true;

            bool isXps = pd.PrinterSettings.PrinterName
                .IndexOf("xps", StringComparison.OrdinalIgnoreCase) >= 0;
            string ext = isXps ? ".xps" : ".pdf";

            using (var sfd = new SaveFileDialog
            {
                Title            = "Save Slip As",
                FileName         = (string.IsNullOrWhiteSpace(pd.DocumentName) ? "Slip" : pd.DocumentName) + ext,
                Filter           = isXps ? "XPS Document (*.xps)|*.xps" : "PDF Document (*.pdf)|*.pdf",
                DefaultExt       = ext.TrimStart('.'),
                AddExtension     = true,
                OverwritePrompt  = true,
                InitialDirectory = LastFileOutputFolder(),
            })
            {
                if (sfd.ShowDialog(owner) != DialogResult.OK) return false;

                // Remember the folder so a run of slips does not restart in Documents each time.
                try { DatabaseManager.SaveGlobalSetting("LastPrintToFileFolder",
                          System.IO.Path.GetDirectoryName(sfd.FileName) ?? ""); } catch { }

                pd.PrinterSettings.PrintToFile   = true;
                pd.PrinterSettings.PrintFileName = sfd.FileName;
            }
            return true;
        }

        private static string LastFileOutputFolder()
        {
            string saved = DatabaseManager.GetGlobalSetting("LastPrintToFileFolder", "");
            if (!string.IsNullOrWhiteSpace(saved) && System.IO.Directory.Exists(saved)) return saved;
            return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
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
            int copiesPerPage = 0, string multiSlipLayout = null)
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
            RenderSlipForPreview(ev.Graphics, area, slipData, fontScale, copiesPerPage, multiSlipLayout);

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

        // RenderSlipContent runs once per copy and again for every preview repaint, so a broken
        // logo would otherwise raise a dialog dozens of times. Warn on the first one only.
        private static bool _logoFailureReported;

        private static void ReportLogoFailure(Exception ex)
        {
            if (_logoFailureReported) return;
            _logoFailureReported = true;

            MessageBox.Show(
                "The company logo could not be drawn, so slips will print without it.\n\n" +
                "The saved image is unreadable: " + ex.Message + "\n\n" +
                "Everything else on the slip is unaffected. To fix it, open Customize Slips " +
                "and choose the logo image again.",
                "Logo Could Not Be Printed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        // Lets a freshly chosen logo re-warn if it too turns out to be broken.
        internal static void ResetLogoFailureWarning() => _logoFailureReported = false;

        // Printed height of the logo in millimetres. Bounded because this reaches the page
        // directly: an unbounded value could push every field off the bottom of a small slip.
        internal const float LogoHeightMinMm     = 3f;
        internal const float LogoHeightMaxMm     = 40f;
        internal const float LogoHeightDefaultMm = 15f;

        private static float LogoHeightMm()
        {
            string raw = DatabaseManager.GetGlobalSetting(
                "LogoHeightMm", LogoHeightDefaultMm.ToString(System.Globalization.CultureInfo.InvariantCulture));

            float mm;
            if (!float.TryParse(raw, System.Globalization.NumberStyles.Any,
                                System.Globalization.CultureInfo.InvariantCulture, out mm))
                mm = LogoHeightDefaultMm;

            return Math.Max(LogoHeightMinMm, Math.Min(LogoHeightMaxMm, mm));
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
            string company   = DatabaseManager.GetGlobalSetting("HeaderTitle", "SLIP MANAGEMENT SYSTEM");
            byte[] logoBytes = DatabaseManager.GetCompanyLogo();

            // Placement earns its keep on small paper. Stacked above the name the logo costs its
            // full height before a single field prints — on 102mm-tall stock that is roughly a
            // sixth of the slip. Beside the name it shares a row that has to exist regardless.
            bool  logoOnLeft = DatabaseManager.GetGlobalSetting("LogoPlacement", "Above")
                                              .Equals("Left", StringComparison.OrdinalIgnoreCase);
            float logoH      = LogoHeightMm() / 25.4f * 100f;   // mm to printer units (100ths in)

            Image                  logoImg    = null;
            System.IO.MemoryStream logoStream = null;
            if (logoBytes != null)
            {
                try
                {
                    // GDI+ reads from the stream lazily, so it must stay open until after the
                    // draw. Hence the explicit dispose below rather than a using here.
                    logoStream = new System.IO.MemoryStream(logoBytes);
                    logoImg    = Image.FromStream(logoStream);
                }
                catch (Exception ex)
                {
                    if (logoStream != null) { logoStream.Dispose(); logoStream = null; }
                    logoImg = null;

                    // Stored but undecodable. Say so once rather than print a slip that quietly
                    // lacks its letterhead — on a proof-of-record document a silent omission is
                    // worse than an interruption, and the operator can still choose to continue.
                    ReportLogoFailure(ex);
                }
            }

            try
            {
                using (var hFont = new Font("Arial", 13 * fontScale, FontStyle.Bold))
                {
                    float textH = hFont.GetHeight(g) + 4;

                    if (logoImg != null && logoOnLeft)
                    {
                        // Width follows the image's own proportions so it never distorts, but is
                        // capped at 40% so a very wide logo cannot crowd out the company name.
                        float logoW = Math.Min(logoImg.Width * (logoH / logoImg.Height), w * 0.4f);
                        float rowH  = Math.Max(logoH, textH);
                        const float gap = 6;

                        g.DrawImage(logoImg, x, y + (rowH - logoH) / 2f, logoW, logoH);

                        var nameRect = new RectangleF(x + logoW + gap, y, w - logoW - gap, rowH);
                        g.DrawString(company, hFont, Brushes.Black, nameRect,
                            new StringFormat { Alignment     = StringAlignment.Center,
                                               LineAlignment = StringAlignment.Center });
                        y += rowH + 5;
                    }
                    else
                    {
                        if (logoImg != null)
                        {
                            float logoW = Math.Min(logoImg.Width * (logoH / logoImg.Height), w);
                            g.DrawImage(logoImg, x + (w - logoW) / 2f, y, logoW, logoH);
                            y += logoH + 4;
                        }

                        var hRect = new RectangleF(x, y, w, textH);
                        g.DrawString(company, hFont, Brushes.Black, hRect,
                            new StringFormat { Alignment = StringAlignment.Center });
                        y += textH + 1;
                    }
                }
            }
            finally
            {
                if (logoImg    != null) logoImg.Dispose();
                if (logoStream != null) logoStream.Dispose();
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

        // How multiple copies are arranged on one page. The operator picks this on the
        // calibration screen, because which way the slips sit — and therefore which way the
        // page is torn or guillotined — depends on their paper and their habits, not on us.
        public const string LayoutColumns = "Columns";   // side by side, cut down the middle
        public const string LayoutRows    = "Rows";      // stacked, cut across
        public const string LayoutGrid    = "Grid";      // 2x2, only meaningful for 4 copies

        // Resolves copies + arrangement into a column/row count. Single source of truth: the
        // print path, the calibration preview and its cell-size maths all call this, so the
        // paper can never disagree with what was shown on screen.
        internal static void GetTiling(int copiesPerPage, string layout, out int cols, out int rows)
        {
            if (copiesPerPage <= 1) { cols = 1; rows = 1; return; }

            bool stacked = string.Equals(layout, LayoutRows, StringComparison.OrdinalIgnoreCase);

            if (copiesPerPage == 2)
            {
                // Grid is meaningless for two, so it falls back to side by side.
                if (stacked) { cols = 1; rows = 2; } else { cols = 2; rows = 1; }
                return;
            }

            if (stacked)                                                          { cols = 1; rows = 4; return; }
            if (string.Equals(layout, LayoutColumns, StringComparison.OrdinalIgnoreCase)) { cols = 4; rows = 1; return; }
            cols = 2; rows = 2;
        }

        // Renders the slip into slipAreaUnits, repeated across the tiling above. Dashed lines
        // mark where to cut. 3 copies is not supported (asymmetric layout removed) and is
        // treated as 2. copiesPerPage = 0 and layout = null read from the active profile.
        internal static void RenderSlipForPreview(
            Graphics g, RectangleF slipAreaUnits,
            Dictionary<string, string> slipData, float fontScale = 1.0f,
            int copiesPerPage = 0, string layout = null)
        {
            if (copiesPerPage <= 0 || layout == null)
            {
                var prof = DatabaseManager.GetActiveProfile();
                if (copiesPerPage <= 0) copiesPerPage = prof.CopiesPerPage;
                if (layout == null)     layout        = prof.MultiSlipLayout;
            }

            // Normalise: only 1, 2, 4 are valid; treat 3 as 2, clamp anything else.
            if (copiesPerPage == 3) copiesPerPage = 2;
            copiesPerPage = copiesPerPage < 1 ? 1 : copiesPerPage > 4 ? 4 : copiesPerPage;

            if (copiesPerPage == 1)
            {
                RenderSlipContent(g, slipAreaUnits, slipData, fontScale);
                return;
            }

            GetTiling(copiesPerPage, layout, out int cols, out int rows);

            float ax = slipAreaUnits.X;
            float ay = slipAreaUnits.Y;
            float aw = slipAreaUnits.Width;
            float ah = slipAreaUnits.Height;
            float cw = aw / cols;
            float ch = ah / rows;
            float gU = (float)(1.0 / 25.4 * 100);   // 1 mm half-gap each side of a divider

            for (int row = 0; row < rows; row++)
                for (int col = 0; col < cols; col++)
                {
                    // Only inner edges lose the gap; the outer edges keep the full margin.
                    float cellX = ax + col * cw + (col == 0 ? 0 : gU);
                    float cellY = ay + row * ch + (row == 0 ? 0 : gU);
                    float cellW = cw - (col == 0 || col == cols - 1 ? gU : gU * 2);
                    float cellH = ch - (row == 0 || row == rows - 1 ? gU : gU * 2);
                    if (cols == 1) cellW = cw;
                    if (rows == 1) cellH = ch;

                    RenderSlipContent(g, new RectangleF(cellX, cellY, cellW, cellH), slipData, fontScale);
                }

            using (var p = new Pen(Color.Black, 1) { DashStyle = DashStyle.Dash })
            {
                for (int col = 1; col < cols; col++)
                    g.DrawLine(p, ax + col * cw, ay, ax + col * cw, ay + ah);
                for (int row = 1; row < rows; row++)
                    g.DrawLine(p, ax, ay + row * ch, ax + aw, ay + row * ch);
            }
        }

        private static string V(Dictionary<string, string> d, string key)
            => d.TryGetValue(key, out var v) ? v : "----";
    }
}
