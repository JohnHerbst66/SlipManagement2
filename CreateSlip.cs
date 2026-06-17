using SlipManagement2;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace SlipManagement2 // ⚠️ Change this to match your exact project namespace!
{
    public partial class CreateSlip : Form
    {
        public CreateSlip()
        {
            InitializeComponent();

            // Auto-fill configurations if launching a new entry transaction profile sequence
            if (this.Tag == null)
            {
                txtSlipID.Text = Main.NextSlipId.ToString();
                txtBilNumber.Text = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            }
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            // Gather the target prominent displays to show directly on the dashboard card
            string slipId = txtSlipID.Text;
            string field1 = string.IsNullOrWhiteSpace(txtField1.Text) ? "Empty" : txtField1.Text; // e.g. Truck Reg
            string field7 = string.IsNullOrWhiteSpace(txtField7.Text) ? "0" : txtField7.Text;     // e.g. Weight Tons

            string formattedTileText = $"🚚 Reg: {field1}\n🆔 Slip: {slipId}\n⚖️ Tons: {field7}";

            if (this.Tag is Button existingCard)
            {
                // EDIT MODE: Update existing dashboard tile display
                existingCard.Text = formattedTileText;

                if (existingCard.Tag is Dictionary<string, string> savedData)
                {
                    savedData["Field1"] = txtField1.Text;
                    savedData["Field2"] = txtField2.Text;
                    savedData["Field3"] = txtField3.Text;
                    savedData["Field4"] = txtField4.Text;
                    savedData["Field5"] = txtField5.Text;
                    savedData["Field6"] = txtField6.Text;
                    savedData["Field7"] = txtField7.Text;
                }
            }
            else
            {
                // NEW MODE: Build a brand new yellow tracking workspace card tile
                Button slipCard = new Button();
                slipCard.Size = new Size(200, 110);
                slipCard.BackColor = Color.LightYellow;
                slipCard.FlatStyle = FlatStyle.Flat;
                slipCard.Font = new Font("Arial", 10, FontStyle.Bold);
                slipCard.Text = formattedTileText;

                // Connect the click routing back into the system loop
                slipCard.Click += SlipCard_Click;

                // Save ALL fields securely into the card tile object memory data dictionary bucket
                slipCard.Tag = new Dictionary<string, string>
                {
                    { "SlipID", txtSlipID.Text },
                    { "BilNumber", txtBilNumber.Text },
                    { "Field1", txtField1.Text },
                    { "Field2", txtField2.Text },
                    { "Field3", txtField3.Text },
                    { "Field4", txtField4.Text },
                    { "Field5", txtField5.Text },
                    { "Field6", txtField6.Text },
                    { "Field7", txtField7.Text }
                };

                Main mainPage = (Main)Application.OpenForms["Main"];
                if (mainPage != null)
                {
                    // Adds the card right onto your named flow layout panel
                  //  mainPage.flpSlips.Controls.Add(slipCard);
                }

                // Increment the master tracking index configuration number
                Main.NextSlipId++;
            }

            this.Close();
        }

        private void SlipCard_Click(object sender, EventArgs e)
        {
            Button clickedCard = (Button)sender;
            if (clickedCard.Tag is Dictionary<string, string> savedData)
            {
                CreateExchangeEditMode(clickedCard, savedData);
            }
        }

        private void CreateExchangeEditMode(Button clickedCard, Dictionary<string, string> savedData)
        {
            CreateSlip editForm = new CreateSlip();
            editForm.txtSlipID.Text = savedData["SlipID"];
            editForm.txtBilNumber.Text = savedData["BilNumber"];
            editForm.txtField1.Text = savedData["Field1"];
            editForm.txtField2.Text = savedData["Field2"];
            editForm.txtField3.Text = savedData["Field3"];
            editForm.txtField4.Text = savedData["Field4"];
            editForm.txtField5.Text = savedData["Field5"];
            editForm.txtField6.Text = savedData["Field6"];
            editForm.txtField7.Text = savedData["Field7"];

            editForm.Tag = clickedCard;
            editForm.ShowDialog();
        }

        private void btnPrint_Click(object sender, EventArgs e)
        {
            // Connect to our new high-speed dot-matrix template processing window sheet
            PrintSlipPreview preview = new PrintSlipPreview();

            // Direct data binding mapping values safely into your specific output labels
            preview.lblSlipIdOutput.Text = txtSlipID.Text;
            preview.lblBilNumberOutput.Text = txtBilNumber.Text;
            preview.lblOutput1.Text = txtField1.Text;
            preview.lblOutput2.Text = txtField2.Text;
            preview.lblOutput3.Text = txtField3.Text;
            preview.lblOutput4.Text = txtField4.Text;
            preview.lblOutput5.Text = txtField5.Text;
            preview.lblOutput6.Text = txtField6.Text;
            preview.lblOutput7.Text = txtField7.Text;

            DialogResult result = preview.ShowDialog();

            // Clear down tracking references off active dashboard workflow if successfully printed
            if (result == DialogResult.OK)
            {
                if (this.Tag is Button cardToDelete)
                {
                    Main mainPage = (Main)Application.OpenForms["Main"];
                    if (mainPage != null)
                    {
                        mainPage.flpSlips.Controls.Remove(cardToDelete);
                        cardToDelete.Dispose();
                    }
                }
                this.Close();
            }
        }

        private void btnSave_Click_1(object sender, EventArgs e)
        {
            // 1. Gather values from your defined inputs
            string bilNumber = txtBilNumber.Text;
            string slipId = txtSlipID.Text;
            string f1 = txtField1.Text;
            string f2 = txtField2.Text;
            string f3 = txtField3.Text;
            string f4 = txtField4.Text;
            string f5 = txtField5.Text;
            string f6 = txtField6.Text;
            string f7 = txtField7.Text;

            // 2. ⭐ SAVE DATA DIRECTLY TO SQLITE DATABASE FIRST
            bool databaseSaved = DatabaseManager.SaveSlip(bilNumber, slipId, f1, f2, f3, f4, f5, f6, f7);

            if (!databaseSaved)
            {
                // If the database fails to write, stop here to protect data integrity
                return;
            }

            // Gather the target prominent displays to show directly on the dashboard card
            
            string field1 = string.IsNullOrWhiteSpace(txtField1.Text) ? "Empty" : txtField1.Text; // e.g. Truck Reg
            string field7 = string.IsNullOrWhiteSpace(txtField7.Text) ? "0" : txtField7.Text;     // e.g. Weight Tons

            string formattedTileText = $"🚚 Reg: {field1}\n🆔 Slip: {slipId}\n⚖️ Tons: {field7}";

            if (this.Tag is Button existingCard)
            {
                // EDIT MODE: Update existing dashboard tile display
                existingCard.Text = formattedTileText;

                if (existingCard.Tag is Dictionary<string, string> savedData)
                {
                    savedData["Field1"] = txtField1.Text;
                    savedData["Field2"] = txtField2.Text;
                    savedData["Field3"] = txtField3.Text;
                    savedData["Field4"] = txtField4.Text;
                    savedData["Field5"] = txtField5.Text;
                    savedData["Field6"] = txtField6.Text;
                    savedData["Field7"] = txtField7.Text;
                }
            }
            else
            {
                // NEW MODE: Build a brand new yellow tracking workspace card tile
                Button slipCard = new Button();
                slipCard.Size = new Size(200, 110);
                slipCard.BackColor = Color.LightYellow;
                slipCard.FlatStyle = FlatStyle.Flat;
                slipCard.Font = new Font("Arial", 10, FontStyle.Bold);
                slipCard.Text = formattedTileText;

                // Connect the click routing back into the system loop
                slipCard.Click += SlipCard_Click;

                // Save ALL fields securely into the card tile object memory data dictionary bucket
                slipCard.Tag = new Dictionary<string, string>
                {
                    { "SlipID", txtSlipID.Text },
                    { "BilNumber", txtBilNumber.Text },
                    { "Field1", txtField1.Text },
                    { "Field2", txtField2.Text },
                    { "Field3", txtField3.Text },
                    { "Field4", txtField4.Text },
                    { "Field5", txtField5.Text },
                    { "Field6", txtField6.Text },
                    { "Field7", txtField7.Text }
                };

                Main mainPage = (Main)Application.OpenForms["Main"];
                if (mainPage != null)
                {
                    // Adds the card right onto your named flow layout panel
                      mainPage.flpSlips.Controls.Add(slipCard);
                }

                // Increment the master tracking index configuration number
                Main.NextSlipId++;
            }

            this.Close();
        }

        private void btnCancel_Click_1(object sender, EventArgs e)
        {
            this.Close();
        }

        private void btnPrint_Click_1(object sender, EventArgs e)
        {
            // Connect to our new high-speed dot-matrix template processing window sheet
            PrintSlipPreview preview = new PrintSlipPreview();

            // Direct data binding mapping values safely into your specific output labels
            preview.lblSlipIdOutput.Text = txtSlipID.Text;
            preview.lblBilNumberOutput.Text = txtBilNumber.Text;
            preview.lblOutput1.Text = txtField1.Text;
            preview.lblOutput2.Text = txtField2.Text;
            preview.lblOutput3.Text = txtField3.Text;
            preview.lblOutput4.Text = txtField4.Text;
            preview.lblOutput5.Text = txtField5.Text;
            preview.lblOutput6.Text = txtField6.Text;
            preview.lblOutput7.Text = txtField7.Text;

            DialogResult result = preview.ShowDialog();

            // Clear down tracking references off active dashboard workflow if successfully printed
            if (result == DialogResult.OK)
            {
                if (this.Tag is Button cardToDelete)
                {
                    DatabaseManager.MarkSlipAsPrinted(txtBilNumber.Text);
                    Main mainPage = (Main)Application.OpenForms["Main"];
                    if (mainPage != null)
                    {
                        mainPage.flpSlips.Controls.Remove(cardToDelete);
                        cardToDelete.Dispose();
                    }
                }
                this.Close();
            }

        }
    }
}
