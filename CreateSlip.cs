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

