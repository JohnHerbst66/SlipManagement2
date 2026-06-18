using System;
using System.Drawing;
using System.Drawing.Printing;
using System.Windows.Forms;

namespace SlipManagement2 // ⚠️ Change this to match your exact project namespace!
{
    public partial class PrintSlipPreview : Form
    {
        private PrinterSettings userSettings = new PrinterSettings();
        private PageSettings pageSettings = new PageSettings();

        public PrintSlipPreview()
        {
            InitializeComponent();
            // 🟢 FIXED MARGIN ENGINE: Forces labels to render below the black line control
            var fieldConfigs = DatabaseManager.GetActiveFieldConfigurations();

            // Change 'panelDivider' to the exact name of your black line control!
            // We add 15 pixels of clean padding whitespace below it.
            int nextYPosition = panelDivider.Bottom + 15;
            int lineSpacingGap = 25;

            for (int i = 1; i <= 6; i++)
            {
                string key = "Field" + i;
                if (fieldConfigs.ContainsKey(key))
                {
                    var cfg = fieldConfigs[key];

                    // 1. Fetch the control arrays safely using the Find function
                    Control[] labelMatches = this.Controls.Find("lblSlipField" + i, true);
                    Control[] outputMatches = this.Controls.Find("lblOutput" + i, true);

                    // Extract the single label if an item was found in the list index
                    Label descriptionLabel = labelMatches.Length > 0 ? labelMatches[0] as Label : null;
                    Label outputValueLabel = outputMatches.Length > 0 ? outputMatches[0] as Label : null;

                    if (descriptionLabel != null && outputValueLabel != null)
                    {
                        if (cfg.IsHidden)
                        {
                            descriptionLabel.Visible = false;
                            outputValueLabel.Visible = false;
                        }
                        else
                        {
                            descriptionLabel.Visible = true;
                            outputValueLabel.Visible = true;

                            descriptionLabel.Text = string.IsNullOrWhiteSpace(cfg.CustomName) ? $"Field {i}:" : cfg.CustomName;

                            descriptionLabel.Top = nextYPosition;
                            outputValueLabel.Top = nextYPosition;

                            nextYPosition += lineSpacingGap;
                        }
                    }
                }
            }

            // 2. Handle your Net Weight metric field box safely (Field 7)
            if (fieldConfigs.ContainsKey("Field7"))
            {
                var cfg7 = fieldConfigs["Field7"];
                Control[] label7Matches = this.Controls.Find("lblSlipField7", true);
                Control[] output7Matches = this.Controls.Find("lblOutput7", true);

                Label descriptionLabel7 = label7Matches.Length > 0 ? label7Matches[0] as Label : null;
                Label outputValueLabel7 = output7Matches.Length > 0 ? output7Matches[0] as Label : null;

                if (descriptionLabel7 != null && outputValueLabel7 != null)
                {
                    if (cfg7.IsHidden)
                    {
                        descriptionLabel7.Visible = false;
                        outputValueLabel7.Visible = false;
                    }
                    else
                    {
                        descriptionLabel7.Visible = true;
                        outputValueLabel7.Visible = true;
                        descriptionLabel7.Text = string.IsNullOrWhiteSpace(cfg7.CustomName) ? "Tons:" : cfg7.CustomName;

                        nextYPosition += 15;
                        descriptionLabel7.Top = nextYPosition;
                        outputValueLabel7.Top = nextYPosition;
                    }
                }
            }


            // Re-configure windows frame appearance properties programmatically 
            this.Size = new Size(500, 680);
            this.Text = "Weighbridge Delivery Slip - Final Print Validation";
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = Color.White; // Simulated clean paper sheet look
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;

            // Generate clean buttons dynamically on the workspace area
            Button btnFinalPrint = new Button() { Text = "🟢 Print Final", Location = new Point(40, 560), Size = new Size(130, 40), BackColor = Color.LightGreen, FlatStyle = FlatStyle.Flat };
            Button btnConfigure = new Button() { Text = "⚙️ Settings", Location = new Point(185, 560), Size = new Size(100, 40), FlatStyle = FlatStyle.Flat };
            Button btnCloseOut = new Button() { Text = "🔴 Cancel", Location = new Point(300, 560), Size = new Size(100, 40), BackColor = Color.LightCoral, FlatStyle = FlatStyle.Flat };

            /*btnFinalPrint.Click += BtnFinalPrint_Click;
            btnConfigure.Click += BtnConfigure_Click;
            btnCloseOut.Click += (s, e) => this.Close();*/

            this.Controls.Add(btnFinalPrint);
            this.Controls.Add(btnConfigure);
            this.Controls.Add(btnCloseOut);

            userSettings.PrinterName = "EPSON LX-350";
        }

        private void BtnConfigure_Click(object sender, EventArgs e)
        {
            using (PrintDialog pd = new PrintDialog())
            {
                pd.PrinterSettings = userSettings;
                if (pd.ShowDialog() == DialogResult.OK)
                {
                    userSettings = pd.PrinterSettings;
                    pageSettings = userSettings.DefaultPageSettings;
                }
            }
        }

        /*private void BtnFinalPrint_Click(object sender, EventArgs e)
        {
            try
            {
                PrintDocument pd = new PrintDocument();
                pd.PrinterSettings.PrinterName = PrinterSettingsForm.SelectedPrinterName;

                pd.DefaultPageSettings.Landscape = false;
                pd.PrinterSettings.DefaultPageSettings.Landscape = false;

                bool isCustomSlip = (PrinterSettingsForm.SelectedPaperSizeObject == null);

                if (isCustomSlip)
                {
                    // Continuous strip setup for the Epson LX-350 (8.5 width x your custom length input)
                    int customWidth = 850;
                    int customHeight = (int)(PrinterSettingsForm.CustomPageLengthInches);
                    pd.DefaultPageSettings.PaperSize = new PaperSize("CustomSlip", customWidth, customHeight);

                    // Wipe out top margins so impact head outputs instantly without blank gaps
                    pd.DefaultPageSettings.Margins = new Margins(0, 0, 0, 0);
                }
                else
                {
                    // ⭐ USE THE OFFICIAL DEVICE DRIVER PAPER SIZE DIRECTLY
                    pd.DefaultPageSettings.PaperSize = PrinterSettingsForm.SelectedPaperSizeObject;

                    // For official standard cut-sheets (like A4/Letter), assign a neat 0.5-inch margin safety zone
                    pd.DefaultPageSettings.Margins = new Margins(50, 50, 50, 50);
                }

                pd.PrintPage += (s, ev) =>
                {
                    Graphics g = ev.Graphics;

                    Font titleFont = new Font("Courier New", 12, FontStyle.Bold);
                    Font regularFont = new Font("Courier New", 10, FontStyle.Regular);
                    Font boldFont = new Font("Courier New", 10, FontStyle.Bold);
                    Font giantFont = new Font("Courier New", 16, FontStyle.Bold);

                    // Compute alignment tracking positions dynamically based on active print media boundaries
                    float startX = isCustomSlip ? 30 : ev.MarginBounds.Left;
                    float startY = isCustomSlip ? 20 : ev.MarginBounds.Top;
                    float gap = regularFont.GetHeight(g) + 4;

                    // 1. Title Header Layout
                    g.DrawString("UITVAL GRONDE - WEIGHBRIDGE SLIP", titleFont, Brushes.Black, startX, startY);
                    startY += gap * 1.5f;

                    g.DrawString($"SLIP ID:       {lblSlipIdOutput.Text}", boldFont, Brushes.Black, startX, startY);
                    startY += gap;
                    g.DrawString($"BIL NUMBER:    {lblBilNumberOutput.Text}", regularFont, Brushes.Black, startX, startY);
                    startY += gap;
                    g.DrawString("------------------------------------------", regularFont, Brushes.Black, startX, startY);
                    startY += gap * 1.2f;

                    // Replace the old block where you called DrawString for Field1 to 7 with this loop:
                    var fieldConfigs = DatabaseManager.GetActiveFieldConfigurations();

                    

                    for (int i = 1; i <= 10; i++)
                    {
                        string key = "Field" + i;

                        // Skip checking Field7 inside this loop because it handles your large Net Weight readout separately below!
                        if (key == "Field7") continue;

                        if (fieldConfigs.ContainsKey(key))
                        {
                            var cfg = fieldConfigs[key];

                            // If a field was unchecked or hidden in your customization template, skip it to save paper roll space!
                            if (cfg.IsHidden) continue;

                            // Fetch values dynamically from your public display labels sitting on the screen preview canvas
                            Control[] outputMatches = this.Controls.Find("lblOutput" + i, true);
                            Label outputLabel = outputMatches.Length > 0 ? outputMatches[0] as Label : null;

                            string displayValue = outputLabel != null ? outputLabel.Text : "";
                            string cleanHeaderName = string.IsNullOrWhiteSpace(cfg.CustomName) ? $"Field {i}:" : cfg.CustomName;

                            // Print to the physical paper using clean column spacing padding alignment
                            g.DrawString($"{cleanHeaderName,-15} {displayValue}", regularFont, Brushes.Black, startX, startY);
                            startY += gap;
                        }
                    }


                    // Draw a dynamic bold line block for your weight category metric (Field 7)
                    if (fieldConfigs.ContainsKey("Field7") && !fieldConfigs["Field7"].IsHidden)
                    {
                        startY += gap;
                        string f7Header = string.IsNullOrWhiteSpace(fieldConfigs["Field7"].CustomName) ? "Tons:" : fieldConfigs["Field7"].CustomName;
                        g.DrawString($"{f7Header} {lblOutput7.Text} t", giantFont, Brushes.Black, startX, startY);
                        startY += gap * 2;
                    }

                    // 4. Verification Footer Trace line
                    g.DrawString("Operator Sign: _________________________ ", regularFont, Brushes.Black, startX, startY);

                    if (isCustomSlip)
                    {

                        ev.HasMorePages = false;
                        return;
                    }

                    ev.HasMorePages = false;
                };
                    pd.Print();
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Printing Subsystem Error: " + ex.Message);
            }
        }
        */

    }
}
