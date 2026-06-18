using System;
using System.Drawing.Printing;
using System.Windows.Forms;

namespace SlipManagement2
{
    public partial class PrinterSettingsForm : Form
    {
        public PrinterSettingsForm()
        {
            InitializeComponent();
            this.Text = "Hardware Profile Settings Center";
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;

            LoadSettingsFromDb();
        }

        private void LoadSettingsFromDb()
        {
            try
            {
                // 1. Clear and populate all active Windows system printers
                cmbPrinters.Items.Clear();
                foreach (string printer in PrinterSettings.InstalledPrinters)
                {
                    cmbPrinters.Items.Add(printer);
                }

                // Restore saved printer selection, default to Epson LX-350 if not set
                string savedPrinter = DatabaseManager.GetGlobalSetting("SelectedPrinter", "EPSON LX-350");
                if (cmbPrinters.Items.Contains(savedPrinter))
                    cmbPrinters.SelectedItem = savedPrinter;
                else if (cmbPrinters.Items.Count > 0)
                    cmbPrinters.SelectedIndex = 0;

                // 2. Populate specified paper profile sizes
                cmbPaperSizes.Items.Clear();
                cmbPaperSizes.Items.Add("A4");
                cmbPaperSizes.Items.Add("A5");
                cmbPaperSizes.Items.Add("A6");
                cmbPaperSizes.Items.Add("Letter");
                cmbPaperSizes.Items.Add("Small151x151");
                cmbPaperSizes.Items.Add("Small240x102");

                string savedPaper = DatabaseManager.GetGlobalSetting("PaperSizeProfile", "A4");
                if (cmbPaperSizes.Items.Contains(savedPaper))
                    cmbPaperSizes.SelectedItem = savedPaper;
                else
                    cmbPaperSizes.SelectedIndex = 0;

                // 3. Populate Orientation profile settings
                cmbOrientation.Items.Clear();
                cmbOrientation.Items.Add("Portrait");
                cmbOrientation.Items.Add("Landscape");

                string savedOrientation = DatabaseManager.GetGlobalSetting("PrintOrientation", "Portrait");
                if (cmbOrientation.Items.Contains(savedOrientation))
                    cmbOrientation.SelectedItem = savedOrientation;
                else
                    cmbOrientation.SelectedIndex = 0;

                // 4. Populate Print Copies Counter Selection bounds
                string savedCopies = DatabaseManager.GetGlobalSetting("PrintCopiesCount", "1");
                if (numCopies != null)
                {
                    if (decimal.TryParse(savedCopies, out decimal copiesVal))
                    {
                        if (copiesVal >= numCopies.Minimum && copiesVal <= numCopies.Maximum)
                            numCopies.Value = copiesVal;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading device configuration states: " + ex.Message, "System Registry Error");
            }
        }

        // Link this to your btnSaveSettings_Click event inside your properties click panels
        private void btnSaveSettings_Click(object sender, EventArgs e)
        {
            try
            {
                string printerName = cmbPrinters.SelectedItem?.ToString() ?? "EPSON LX-350";
                string paperSize = cmbPaperSizes.SelectedItem?.ToString() ?? "A4";
                string orientation = cmbOrientation.SelectedItem?.ToString() ?? "Portrait";
                string copiesCount = numCopies != null ? numCopies.Value.ToString() : "1";

                // Save configurations directly inside the portable SQLite file storage tracking slots
                DatabaseManager.SaveGlobalSetting("SelectedPrinter", printerName);
                DatabaseManager.SaveGlobalSetting("PaperSizeProfile", paperSize);
                DatabaseManager.SaveGlobalSetting("PrintOrientation", orientation);
                DatabaseManager.SaveGlobalSetting("PrintCopiesCount", copiesCount);

                MessageBox.Show("Hardware profile definitions saved securely into local database storage cache!", "Configuration Protected");
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed updating device parameters data entries: " + ex.Message);
            }
        }

        // Link this to your btnCancelSettings_Click event inside your designer properties click panels
        private void btnCancelSettings_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
