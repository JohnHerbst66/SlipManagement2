using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace SlipManagement2
{
    public partial class CreateSlip : Form
    {
        public CreateSlip()
        {
            InitializeComponent();
            // Add this routine right inside your CreateSlip constructor method block:
            var fieldConfigs = DatabaseManager.GetActiveFieldConfigurations();

            // Clean structural loop mapping rules dynamically over your 7 active field slots
            for (int i = 1; i <= 7; i++)
            {
                string key = "Field" + i;
                if (fieldConfigs.ContainsKey(key))
                {
                    var cfg = fieldConfigs[key];

                    // Find the visual controls sitting on your form canvas dynamically by name strings
                    Label targetLabel = this.Controls.Find("lblField" + i, true)[0] as Label;
                    TextBox targetTextBox = this.Controls.Find("txtField" + i, true)[0] as TextBox;

                    if (targetLabel != null && targetTextBox != null)
                    {
                        if (cfg.IsHidden)
                        {
                            // 1. Hide them completely from view if disabled by user settings
                            targetLabel.Visible = false;
                            targetTextBox.Visible = false;
                        }
                        else
                        {
                            // 2. Otherwise, keep them visible and overwrite text to show custom names!
                            targetLabel.Visible = true;
                            targetTextBox.Visible = true;
                            targetLabel.Text = string.IsNullOrWhiteSpace(cfg.CustomName) ? $"Field {i}:" : cfg.CustomName;
                        }
                    }
                }
            }


            // Auto-fill configuration properties if launching a new entry sequence
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
            string bilNumber = txtBilNumber.Text;
            string slipId = txtSlipID.Text;
            string f1 = txtField1.Text;
            string f2 = txtField2.Text;
            string f3 = txtField3.Text;
            string f4 = txtField4.Text;
            string f5 = txtField5.Text;
            string f6 = txtField6.Text;
            string f7 = txtField7.Text;

            // 1. Save data directly to SQLite database
            bool databaseSaved = DatabaseManager.SaveSlip(bilNumber, slipId, f1, f2, f3, f4, f5, f6, f7);
            if (!databaseSaved) return;

            string formattedTileText = $"🚚 Reg: {(string.IsNullOrWhiteSpace(f1) ? "No Reg" : f1)}\n🆔 Slip: {slipId}\n⚖️ Tons: {(string.IsNullOrWhiteSpace(f7) ? "0" : f7)}";

            if (this.Tag is Button existingCard)
            {
                existingCard.Text = formattedTileText;
                if (existingCard.Tag is Dictionary<string, string> savedData)
                {
                    savedData["Field1"] = f1; savedData["Field2"] = f2; savedData["Field3"] = f3;
                    savedData["Field4"] = f4; savedData["Field5"] = f5; savedData["Field6"] = f6; savedData["Field7"] = f7;
                }
            }
            else
            {
                Button slipCard = new Button();
                slipCard.Size = new Size(200, 110);
                slipCard.BackColor = Color.LightYellow;
                slipCard.FlatStyle = FlatStyle.Flat;
                slipCard.Font = new Font("Arial", 10, FontStyle.Bold);
                slipCard.Text = formattedTileText;
                slipCard.Click += SlipCard_Click;

                slipCard.Tag = new Dictionary<string, string>
                {
                    { "SlipID", slipId }, { "BilNumber", bilNumber },
                    { "Field1", f1 }, { "Field2", f2 }, { "Field3", f3 },
                    { "Field4", f4 }, { "Field5", f5 }, { "Field6", f6 }, { "Field7", f7 }
                };

                Main mainPage = (Main)Application.OpenForms["Main"];
                if (mainPage != null)
                {
                    mainPage.flpSlips.Controls.Add(slipCard);
                }

                Main.NextSlipId++;
            }

            this.Close();
        }

        private void SlipCard_Click(object sender, EventArgs e)
        {
            Button clickedCard = (Button)sender;
            if (clickedCard.Tag is Dictionary<string, string> savedData)
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
        }

        private void btnPrint_Click(object sender, EventArgs e)
        
        {
            PrintSlipPreview preview = new PrintSlipPreview();

            // 1. Map the stationary header identifiers
            preview.lblSlipIdOutput.Text = txtSlipID.Text;
            preview.lblBilNumberOutput.Text = txtBilNumber.Text;

            // 2. Map all 10 text inputs dynamically to your preview labels in a single loop
            // 🟢 FIXED ARRAY-SAFE PRINT TRANSMISSION LOOP
            for (int i = 1; i <= 10; i++)
            {
                // 1. Fetch the control arrays using the Find function
                Control[] textboxMatches = this.Controls.Find("txtField" + i, true);
                Control[] labelMatches = preview.Controls.Find("lblOutput" + i, true);

                // 2. Extract the single control safely if an item was found in the list array index [0]
                TextBox sourceTextBox = textboxMatches.Length > 0 ? textboxMatches[0] as TextBox : null;
                Label targetLabel = labelMatches.Length > 0 ? labelMatches[0] as Label : null;

                if (sourceTextBox != null && targetLabel != null)
                {
                    targetLabel.Text = sourceTextBox.Text;
                }
            }

            // 3. Open up your dynamically populated validation preview screen
            DialogResult result = preview.ShowDialog();

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


        // Paste these three helper routines to satisfy the background designer file errors:
        private void btnSave_Click_1(object sender, EventArgs e)
        {
            btnSave_Click(sender, e); // Routes the action right to your main save logic
        }

        private void btnCancel_Click_1(object sender, EventArgs e)
        {
            btnCancel_Click(sender, e); // Routes the action to your clean close logic
        }

        private void btnPrint_Click_1(object sender, EventArgs e)
        {
            btnPrint_Click(sender, e); // Routes the action to your universal print engine
        }

    }
}

