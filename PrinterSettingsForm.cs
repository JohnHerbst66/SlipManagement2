using System;
using System.Drawing.Printing;
using System.Windows.Forms;

namespace SlipManagement2 // Double-check that this matches your exact namespace spelling
{
    public partial class PrinterSettingsForm : Form
    {
        // Global variables available instantly to your print preview forms
        public static string SelectedPrinterName = "EPSON LX-350";
        public static PaperSize SelectedPaperSizeObject = null;
        public static float CustomPageLengthInches = 5.5f;

        public PrinterSettingsForm()
        {
            InitializeComponent();
            this.Text = "Hardware Settings Center";
            this.StartPosition = FormStartPosition.CenterParent;

            // 1. Populate all installed printers on the computer
            foreach (string printer in PrinterSettings.InstalledPrinters)
            {
                cmbPrinters.Items.Add(printer);
            }

            if (cmbPrinters.Items.Contains(SelectedPrinterName))
                cmbPrinters.SelectedItem = SelectedPrinterName;
            else if (cmbPrinters.Items.Count > 0)
                cmbPrinters.SelectedIndex = 0;

            // 2. Wire an event when the printer dropdown changes to refresh available paper formats
            cmbPrinters.SelectedIndexChanged += (s, e) => {
                SelectedPrinterName = cmbPrinters.SelectedItem.ToString();
                LoadPrinterPaperSizes();
            };

            // Initial load of paper profiles based on default printer selection
            LoadPrinterPaperSizes();

            txtSlipLength.Text = CustomPageLengthInches.ToString();
            cmbPaperSizes.SelectedIndexChanged += (s, e) => ToggleLengthInputVisibility();
        }

        // ⭐ THE DYNAMIC FUNCTION: Fetches size profiles directly from the hardware driver
        private void LoadPrinterPaperSizes()
        {
            cmbPaperSizes.Items.Clear();
            try
            {
                PrinterSettings settings = new PrinterSettings { PrinterName = SelectedPrinterName };

                // Add your custom continuous slip option as a manual override layout
                cmbPaperSizes.Items.Add("Custom Continuous Slip");

                foreach (PaperSize size in settings.PaperSizes)
                {
                    cmbPaperSizes.Items.Add(size.PaperName);
                }

                if (cmbPaperSizes.Items.Count > 0)
                    cmbPaperSizes.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading printer paper formats: " + ex.Message);
            }
        }

        private void ToggleLengthInputVisibility()
        {
            if (cmbPaperSizes.SelectedItem != null)
            {
                // Only enable the custom length text field if "Custom Continuous Slip" is active
                bool isCustom = cmbPaperSizes.SelectedItem.ToString() == "Custom Continuous Slip";
                txtSlipLength.Enabled = isCustom;
            }
        }

        private void btnCancelSettings_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void btnSaveSettings_Click(object sender, EventArgs e)
        {
            if (cmbPrinters.SelectedItem != null)
                SelectedPrinterName = cmbPrinters.SelectedItem.ToString();

            if (cmbPaperSizes.SelectedItem != null)
            {
                string selectedName = cmbPaperSizes.SelectedItem.ToString();

                if (selectedName == "Custom Continuous Slip")
                {
                    SelectedPaperSizeObject = null; // Use manual length formula override
                }
                else
                {
                    // Search for the matching PaperSize object inside the active driver map
                    PrinterSettings settings = new PrinterSettings { PrinterName = SelectedPrinterName };
                    foreach (PaperSize size in settings.PaperSizes)
                    {
                        if (size.PaperName == selectedName)
                        {
                            SelectedPaperSizeObject = size;
                            break;
                        }
                    }
                }
            }

            if (float.TryParse(txtSlipLength.Text, out float checkLength))
                CustomPageLengthInches = checkLength;

            MessageBox.Show("Printer configurations applied successfully!", "System Notice");
            this.Close();
        }
    }
}
