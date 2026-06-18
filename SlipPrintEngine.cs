using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Drawing;
using System.Drawing.Printing;
using System.Windows.Forms;

namespace SlipManagement2
{
    public static class SlipPrintEngine
    {
        // MASTER FUNCTION: Call this from any button to execute adaptive scaling prints
        public static void ExecutePrintJob(Dictionary<string, string> slipData)
        {
            try
            {
                PrintDocument pd = new PrintDocument();

                // 1. Fetch persistent user configurations out of SQLite global settings
                string printerName = DatabaseManager.GetGlobalSetting("SelectedPrinter", "EPSON LX-350");
                string paperProfile = DatabaseManager.GetGlobalSetting("PaperSizeProfile", "A4");
                string orientationSetting = DatabaseManager.GetGlobalSetting("PrintOrientation", "Portrait");
                string copiesSetting = DatabaseManager.GetGlobalSetting("PrintCopiesCount", "1");

                // Apply hardware targeting parameters
                pd.PrinterSettings.PrinterName = printerName;
                if (short.TryParse(copiesSetting, out short copiesCount))
                {
                    pd.PrinterSettings.Copies = copiesCount;
                }

                // 2. Set Orientation rules explicitly
                bool isLandscape = orientationSetting.Equals("Landscape", StringComparison.OrdinalIgnoreCase);
                pd.DefaultPageSettings.Landscape = isLandscape;
                pd.PrinterSettings.DefaultPageSettings.Landscape = isLandscape;

                // 3. ⭐ THE ADAPTIVE PROFILE BOUNDARY MATRICES ENGINE
                // Define width and height in millimeters (mm) based on specified size profiles
                double widthMm = 210;
                double heightMm = 297;

                switch (paperProfile)
                {
                    case "A4": widthMm = 210; heightMm = 297; break;
                    case "A5": widthMm = 148; heightMm = 210; break;
                    case "A6": widthMm = 105; heightMm = 148; break;
                    case "Letter": widthMm = 215.9; heightMm = 279.4; break;
                    case "Small151x151": widthMm = 151; heightMm = 151; break;
                    case "Small240x102": widthMm = 240; heightMm = 102; break;
                }

                // Convert millimeters into standard C# Print Units (hundredths of an inch)
                // Formula: (mm / 25.4) * 100
                int widthUnits = (int)((widthMm / 25.4) * 100);
                int heightUnits = (int)((heightMm / 25.4) * 100);

                // Assign the computed paper boundaries straight to the printing driver layout
                pd.DefaultPageSettings.PaperSize = new PaperSize(paperProfile, widthUnits, heightUnits);

                // 4. THE 10MM SAFETY BOUNDARY LOCK
                // 10mm converts mathematically to exactly 39.37 print units (round to 39)
                int marginUnits = 39;
                pd.DefaultPageSettings.Margins = new Margins(marginUnits, marginUnits, marginUnits, marginUnits);

                // We will add the pd.PrintPage text drawing loop next!
                // 5. THE DYNAMIC RENDERING LOOP
                pd.PrintPage += (s, ev) =>
                {
                    Graphics g = ev.Graphics;

                    // Base professional typography for impact and thermal heads
                    Font titleFont = new Font("Courier New", 12, FontStyle.Bold);
                    Font regularFont = new Font("Courier New", 10, FontStyle.Regular);
                    Font boldFont = new Font("Courier New", 10, FontStyle.Bold);
                    Font giantFont = new Font("Courier New", 16, FontStyle.Bold);

                    // Dynamic coordinate placement locked inside our 10mm margins safety frame
                    float currentX = ev.MarginBounds.Left;
                    float currentY = ev.MarginBounds.Top;
                    float lineSpacing = regularFont.GetHeight(g) + 4;

                    // Fetch custom company title from database
                    string companyTitle = DatabaseManager.GetGlobalSetting("HeaderTitle", "SLIP MANAGEMENT SYSTEM");
                    g.DrawString(companyTitle, titleFont, Brushes.Black, currentX, currentY);
                    currentY += lineSpacing * 2;

                    // Audit Trail Stationary Block (Always Visible)
                    g.DrawString($"SLIP ID:       {(slipData.ContainsKey("SlipID") ? slipData["SlipID"] : "----")}", boldFont, Brushes.Black, currentX, currentY);
                    currentY += lineSpacing;
                    g.DrawString($"BIL NUMBER:    {(slipData.ContainsKey("BilNumber") ? slipData["BilNumber"] : "----")}", regularFont, Brushes.Black, currentX, currentY);
                    currentY += lineSpacing;
                    g.DrawString(new string('-', 42), regularFont, Brushes.Black, currentX, currentY);
                    currentY += lineSpacing * 1.2f;

                    // Dynamic data rows extraction loop (Fields 1 to 10)
                    var fieldConfigs = DatabaseManager.GetActiveFieldConfigurations();
                    for (int i = 1; i <= 10; i++)
                    {
                        string key = "Field" + i;
                        if (key == "Field7") continue; // Field 7 is reserved for the large totals display below

                        if (fieldConfigs.ContainsKey(key))
                        {
                            var cfg = fieldConfigs[key];
                            if (cfg.IsHidden) continue; // Compress vertical page footprint if checkbox is hidden

                            string displayValue = slipData.ContainsKey(key) ? slipData[key] : "";
                            string customHeaderName = string.IsNullOrWhiteSpace(cfg.CustomName) ? $"Field {i}:" : cfg.CustomName;

                            // Column spacing padded layout alignment map
                            g.DrawString($"{customHeaderName,-15} {displayValue}", regularFont, Brushes.Black, currentX, currentY);
                            currentY += lineSpacing;
                        }
                    }

                    // Large Highlighted Summary Block (Field 7 / Weight Metrics)
                    if (fieldConfigs.ContainsKey("Field7") && !fieldConfigs["Field7"].IsHidden)
                    {
                        currentY += lineSpacing;
                        string f7Header = string.IsNullOrWhiteSpace(fieldConfigs["Field7"].CustomName) ? "Tons:" : fieldConfigs["Field7"].CustomName;
                        string tonsValue = slipData.ContainsKey("Field7") ? slipData["Field7"] : "0";

                        g.DrawString(new string('-', 42), regularFont, Brushes.Black, currentX, currentY);
                        currentY += lineSpacing * 0.5f;
                        g.DrawString($"{f7Header} {tonsValue} t", giantFont, Brushes.Black, currentX, currentY);
                        currentY += lineSpacing * 1.5f;
                        g.DrawString(new string('-', 42), regularFont, Brushes.Black, currentX, currentY);
                        currentY += lineSpacing * 2f;
                    }

                    // Operator Validation Signature Line
                    g.DrawString("Operator Sign: ___________________________", regularFont, Brushes.Black, currentX, currentY);

                    ev.HasMorePages = false; // Gracefully terminates printer feed assignment map
                };

                // Execute the layout pipeline via Windows spooler
                pd.Print();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Printing Class Engine Failed: " + ex.Message, "Hardware Interface Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
