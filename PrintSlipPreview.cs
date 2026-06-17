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

            btnFinalPrint.Click += BtnFinalPrint_Click;
            btnConfigure.Click += BtnConfigure_Click;
            btnCloseOut.Click += (s, e) => this.Close();

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

        private void BtnFinalPrint_Click(object sender, EventArgs e)
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

                    // 2. Data Alignment Column Stack (Perfect column lining via ,-15 padding rule)
                    g.DrawString($"{lblSlipField1.Text,-15} {lblOutput1.Text}", boldFont, Brushes.Black, startX, startY); startY += gap;
                    g.DrawString($"{lblSlipField2.Text,-15} {lblOutput2.Text}", regularFont, Brushes.Black, startX, startY); startY += gap;
                    g.DrawString($"{lblSlipField3.Text,-15} {lblOutput3.Text}", regularFont, Brushes.Black, startX, startY); startY += gap;
                    g.DrawString($"{lblSlipField4.Text,-15} {lblOutput4.Text}", regularFont, Brushes.Black, startX, startY); startY += gap;
                    g.DrawString($"{lblSlipField5.Text,-15} {lblOutput5.Text}", regularFont, Brushes.Black, startX, startY); startY += gap;
                    g.DrawString($"{lblSlipField6.Text,-15} {lblOutput6.Text}", regularFont, Brushes.Black, startX, startY); startY += gap * 1.5f;

                    // 3. Prominent Highlighted Net Weight Section
                    g.DrawString("------------------------------------------", regularFont, Brushes.Black, startX, startY);
                    startY += gap * 0.5f;
                    g.DrawString($"{lblSlipField7.Text} {lblOutput7.Text} t", giantFont, Brushes.Black, startX, startY);
                    startY += gap * 1.5f;
                    g.DrawString("------------------------------------------", regularFont, Brushes.Black, startX, startY);
                    startY += gap * 2f;

                    // 4. Verification Footer Trace line
                    g.DrawString("Operator Sign: ___________________________", regularFont, Brushes.Black, startX, startY);

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


    }
}
