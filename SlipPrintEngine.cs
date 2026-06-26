using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.Windows.Forms;

namespace SlipManagement2
{
    public static class SlipPrintEngine
    {
        // Builds and returns a fully configured PrintDocument without printing.
        // Used by both ExecutePrintJob (direct print) and PrintSlipPreview (preview).
        public static PrintDocument BuildPrintDocument(Dictionary<string, string> slipData)
        {
            PrintDocument pd = new PrintDocument();

            string printerName      = DatabaseManager.GetGlobalSetting("SelectedPrinter",  "EPSON LX-350");
            string paperProfile     = DatabaseManager.GetGlobalSetting("PaperSizeProfile",  "Small240x102");
            string orientationSetting = DatabaseManager.GetGlobalSetting("PrintOrientation","Portrait");
            string copiesSetting    = DatabaseManager.GetGlobalSetting("PrintCopiesCount",  "1");

            pd.PrinterSettings.PrinterName = printerName;
            if (short.TryParse(copiesSetting, out short copies))
                pd.PrinterSettings.Copies = copies;

            bool isLandscape = orientationSetting.Equals("Landscape", StringComparison.OrdinalIgnoreCase);
            pd.DefaultPageSettings.Landscape = isLandscape;
            pd.PrinterSettings.DefaultPageSettings.Landscape = isLandscape;

            double widthMm = 240, heightMm = 102;
            switch (paperProfile)
            {
                case "A4":           widthMm = 210;   heightMm = 297;   break;
                case "A5":           widthMm = 148;   heightMm = 210;   break;
                case "A6":           widthMm = 105;   heightMm = 148;   break;
                case "Letter":       widthMm = 215.9; heightMm = 279.4; break;
                case "Small151x151": widthMm = 151;   heightMm = 151;   break;
                case "Small240x102":
                    widthMm = 240;
                    // Custom slip length (in inches) overrides the default 102mm height
                    string customLen = DatabaseManager.GetGlobalSetting("SlipCustomLength", "5.5");
                    if (double.TryParse(customLen, System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture, out double lenIn))
                        heightMm = lenIn * 25.4;
                    else
                        heightMm = 102;
                    break;
            }

            int widthUnits  = (int)((widthMm  / 25.4) * 100);
            int heightUnits = (int)((heightMm / 25.4) * 100);
            pd.DefaultPageSettings.PaperSize = new PaperSize(paperProfile, widthUnits, heightUnits);

            // Margins from the active PrinterProfile (falls back to 10mm if no profile saved yet)
            var profile = DatabaseManager.GetActiveProfile();
            int mTop    = (int)((profile.MarginTopMM    / 25.4) * 100);
            int mLeft   = (int)((profile.MarginLeftMM   / 25.4) * 100);
            int mRight  = (int)((profile.MarginRightMM  / 25.4) * 100);
            int mBottom = (int)((profile.MarginBottomMM / 25.4) * 100);
            pd.DefaultPageSettings.Margins = new Margins(mLeft, mRight, mTop, mBottom);

            pd.PrintPage += (s, ev) => RenderSlip(ev, slipData);
            return pd;
        }

        // Sends directly to the printer. Use this for reprints from History.
        public static void ExecutePrintJob(Dictionary<string, string> slipData)
        {
            try
            {
                BuildPrintDocument(slipData).Print();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Print failed: " + ex.Message, "Printer Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // The single shared rendering routine — called by both preview and actual print.
        private static void RenderSlip(PrintPageEventArgs ev, Dictionary<string, string> slipData)
        {
            Graphics g = ev.Graphics;

            Font titleFont   = new Font("Courier New", 12, FontStyle.Bold);
            Font regularFont = new Font("Courier New", 10, FontStyle.Regular);
            Font boldFont    = new Font("Courier New", 10, FontStyle.Bold);
            Font giantFont   = new Font("Courier New", 16, FontStyle.Bold);

            float x    = ev.MarginBounds.Left;
            float y    = ev.MarginBounds.Top;
            float gap  = regularFont.GetHeight(g) + 4;

            string header = DatabaseManager.GetGlobalSetting("HeaderTitle", "SLIP MANAGEMENT SYSTEM");
            g.DrawString(header, titleFont, Brushes.Black, x, y);
            y += gap * 2;

            g.DrawString($"SLIP ID:       {V(slipData, "SlipID")}",     boldFont,    Brushes.Black, x, y); y += gap;
            g.DrawString($"BIL NUMBER:    {V(slipData, "BillNumber")}", regularFont, Brushes.Black, x, y); y += gap;
            g.DrawString(new string('-', 42), regularFont, Brushes.Black, x, y); y += gap * 1.2f;

            var fieldConfigs = DatabaseManager.GetActiveFieldConfigurations();
            for (int i = 1; i <= 10; i++)
            {
                string key = "Field" + i;
                if (key == "Field7") continue; // rendered separately below as the large totals block

                if (fieldConfigs.TryGetValue(key, out var cfg) && !cfg.IsHidden)
                {
                    string label = string.IsNullOrWhiteSpace(cfg.CustomName) ? $"Field {i}:" : cfg.CustomName;
                    g.DrawString($"{label,-15} {V(slipData, key)}", regularFont, Brushes.Black, x, y);
                    y += gap;
                }
            }

            if (fieldConfigs.TryGetValue("Field7", out var cfg7) && !cfg7.IsHidden)
            {
                string f7Label = string.IsNullOrWhiteSpace(cfg7.CustomName) ? "Tons:" : cfg7.CustomName;
                y += gap;
                g.DrawString(new string('-', 42), regularFont, Brushes.Black, x, y); y += gap * 0.5f;
                g.DrawString($"{f7Label} {V(slipData, "Field7")} t", giantFont, Brushes.Black, x, y); y += gap * 1.5f;
                g.DrawString(new string('-', 42), regularFont, Brushes.Black, x, y); y += gap * 2f;
            }

            g.DrawString("Operator Sign: ___________________________", regularFont, Brushes.Black, x, y);
            ev.HasMorePages = false;
        }

        private static string V(Dictionary<string, string> d, string key)
            => d.TryGetValue(key, out var v) ? v : "----";
    }
}
