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

            // 1. Create a dynamic string array to store all 10 field values safely
            string[] fields = new string[10];
            for (int i = 1; i <= 10; i++)
            {
                Control[] matches = this.Controls.Find("txtField" + i, true);
                fields[i - 1] = (matches.Length > 0 && matches[0] is TextBox tb) ? tb.Text : "";
            }

            // 2. SAVE SECURELY TO SQLITE DATABASE WITH ALL 10 FIELD ARRAYS
            bool databaseSaved = DatabaseManager.SaveSlip(
                bilNumber, slipId,
                fields[0], fields[1], fields[2], fields[3], fields[4],
                fields[5], fields[6], fields[7], fields[8], fields[9]
            );

            if (!databaseSaved) return;

            // 3. Setup text display metrics for your visible dashboard tracking tile card (Field 1 and Field 7)
            string regDisplay = string.IsNullOrWhiteSpace(fields[0]) ? "No Reg" : fields[0];
            string tonsDisplay = string.IsNullOrWhiteSpace(fields[6]) ? "0" : fields[6];
            string formattedTileText = $"🚚 Reg: {regDisplay}\n🆔 Slip: {slipId}\n⚖️ Tons: {tonsDisplay}";

            if (this.Tag is Button existingCard)
            {
                // EDIT MODE: Update existing dashboard card face text
                existingCard.Text = formattedTileText;

                // Synchronize all 10 fields within the card tile container memory dictionary cache
                if (existingCard.Tag is Dictionary<string, string> savedData)
                {
                    for (int i = 1; i <= 10; i++)
                    {
                        savedData["Field" + i] = fields[i - 1];
                    }
                }
            }
            else
            {
                // NEW MODE: Build a brand new yellow workspace tracking card tile
                Button slipCard = new Button();
                slipCard.Size = new Size(200, 110);
                slipCard.BackColor = Color.LightYellow;
                slipCard.FlatStyle = FlatStyle.Flat;
                slipCard.Font = new Font("Arial", 10, FontStyle.Bold);
                slipCard.Text = formattedTileText;
                slipCard.Click += SlipCard_Click;

                // Initialize and fill the core dictionary container data properties block
                var memoryCache = new Dictionary<string, string>
        {
            { "SlipID", slipId },
            { "BilNumber", bilNumber }
        };

                for (int i = 1; i <= 10; i++)
                {
                    memoryCache.Add("Field" + i, fields[i - 1]);
                }
                slipCard.Tag = memoryCache;

                // Mount the tile directly to your main workspace panel container
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

                // ⭐ LOOP TO LOAD ALL 10 POPULATED FIELDS BACK INTO TEXTBOX CONTROLS
                for (int i = 1; i <= 10; i++)
                {
                    Control[] txtMatches = editForm.Controls.Find("txtField" + i, true);
                    if (txtMatches.Length > 0 && txtMatches[0] is TextBox tb && savedData.ContainsKey("Field" + i))
                    {
                        tb.Text = savedData["Field" + i];
                    }
                }

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

