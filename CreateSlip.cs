using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace SlipManagement2
{
    public partial class CreateSlip : Form
    {
        public int ExistingSlipId = 0;

        // Tracks which fields are shown as ComboBoxes (lookup fields) instead of TextBoxes
        private readonly Dictionary<string, ComboBox> _lookupBoxes = new Dictionary<string, ComboBox>();

        public CreateSlip()
        {
            InitializeComponent();

            var fieldConfigs = DatabaseManager.GetActiveFieldConfigurations();

            for (int i = 1; i <= 10; i++)
            {
                string key = "Field" + i;
                if (!fieldConfigs.ContainsKey(key)) continue;

                var cfg = fieldConfigs[key];
                var labelMatches = this.Controls.Find("lblField" + i, true);
                var boxMatches   = this.Controls.Find("txtField" + i, true);

                if (labelMatches.Length == 0 || boxMatches.Length == 0) continue;
                if (!(labelMatches[0] is Label targetLabel) || !(boxMatches[0] is TextBox targetBox)) continue;

                if (cfg.IsHidden)
                {
                    targetLabel.Visible = false;
                    targetBox.Visible   = false;
                    continue;
                }

                targetLabel.Visible = true;
                targetLabel.Text = string.IsNullOrWhiteSpace(cfg.CustomName) ? $"Field {i}:" : cfg.CustomName;

                // Field7 (Tons) is always a plain TextBox — numeric entry only, no lookup
                if (!string.IsNullOrEmpty(cfg.LookupTable) && i != 7)
                {
                    // Overlay a ComboBox at the same position as the TextBox
                    targetBox.Visible = false;

                    var combo = new ComboBox
                    {
                        Name               = "cboField" + i,
                        Location           = targetBox.Location,
                        Size               = new Size(targetBox.Width, 24),
                        DropDownStyle      = ComboBoxStyle.DropDown,
                        AutoCompleteMode   = AutoCompleteMode.SuggestAppend,
                        AutoCompleteSource = AutoCompleteSource.ListItems,
                        Font               = targetBox.Font,
                        TabIndex           = i,
                    };

                    var values = DatabaseManager.GetLookupValues(cfg.LookupTable);
                    combo.Items.AddRange(values.ToArray());

                    string tableName = cfg.LookupTable;
                    combo.Leave += (s, e) =>
                    {
                        string val = combo.Text.Trim();
                        if (!string.IsNullOrEmpty(val))
                            DatabaseManager.SaveLookupValue(tableName, val);
                    };

                    Control parent = targetBox.Parent ?? this;
                    parent.Controls.Add(combo);
                    combo.BringToFront();

                    _lookupBoxes[key] = combo;
                }
                else
                {
                    targetBox.Visible = true;
                }
            }

            txtBilNumber.Text = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            txtSlipID.Text    = "Auto-assigned";

            txtField7.TextChanged += (s, e) => ValidatePrintButton();
            txtField7.KeyPress    += txtField7_KeyPress;
            ValidatePrintButton();

            this.Shown += (s, e) =>
            {
                Control first = _lookupBoxes.ContainsKey("Field1")
                    ? (Control)_lookupBoxes["Field1"]
                    : txtField1;
                if (first.Visible) first.Focus();
            };
        }

        // Sets a field value regardless of whether it uses a ComboBox or TextBox.
        // Call this instead of directly accessing txtField{n} from outside the form.
        public void SetFieldValue(int fieldNumber, string value)
        {
            string key = "Field" + fieldNumber;
            // Field7 (Tons) is stored with period; display with comma to match input format
            string display = (fieldNumber == 7) ? value.Replace('.', ',') : value;
            if (_lookupBoxes.TryGetValue(key, out ComboBox cb))
                cb.Text = display;
            else
            {
                var matches = this.Controls.Find("txtField" + fieldNumber, true);
                if (matches.Length > 0 && matches[0] is TextBox tb)
                    tb.Text = display;
            }
        }

        private void txtField7_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (char.IsControl(e.KeyChar)) return;

            if (!char.IsDigit(e.KeyChar) && e.KeyChar != ',' && e.KeyChar != '.')
            { e.Handled = true; return; }

            char ch          = (e.KeyChar == '.') ? ',' : e.KeyChar;
            string current   = txtField7.Text;
            int selStart     = txtField7.SelectionStart;
            int selLen       = txtField7.SelectionLength;
            string simulated = current.Remove(selStart, selLen).Insert(selStart, ch.ToString());

            int commaPos = simulated.IndexOf(',');
            if (commaPos < 0)
            {
                if (simulated.Length > 2) { e.Handled = true; return; }
            }
            else
            {
                string before = simulated.Substring(0, commaPos);
                string after  = simulated.Substring(commaPos + 1);
                if (before.Length > 2 || after.Length > 3 || after.Contains(','))
                { e.Handled = true; return; }
            }

            // Redirect period keystroke so it arrives as a comma
            if (e.KeyChar == '.')
            {
                e.Handled                 = true;
                txtField7.Text            = simulated;
                txtField7.SelectionStart  = selStart + 1;
                txtField7.SelectionLength = 0;
            }
        }

        private void ValidatePrintButton()
        {
            if (!txtField7.Visible)
            {
                btnPrint.Enabled = true;
                return;
            }
            string tons = txtField7.Text.Trim().Replace(',', '.');
            btnPrint.Enabled = tons.Length > 0 &&
                               double.TryParse(tons, NumberStyles.Any, CultureInfo.InvariantCulture, out _);
        }

        private static string NormalizeTons(string raw) => raw.Trim().Replace(',', '.');

        private void btnCancel_Click(object sender, EventArgs e)
        {
            if (ExistingSlipId == 0)
            {
                this.Close();
                return;
            }

            var choice = MessageBox.Show(
                "This slip has been saved (Status: Unprinted).\n\n" +
                "YES  — Void this slip (enter a reason)\n" +
                "NO   — Close without voiding (slip stays on the dashboard)\n" +
                "CANCEL — Go back to editing",
                "Void or Close?",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Question);

            if (choice == DialogResult.Cancel) return;

            if (choice == DialogResult.Yes)
            {
                string reason = PromptForVoidReason();
                if (reason == null) return;

                DatabaseManager.VoidSlip(ExistingSlipId, reason);

                if (this.Tag is Button cardToRemove)
                {
                    Main mainPage = (Main)Application.OpenForms["Main"];
                    mainPage?.flpSlips.Controls.Remove(cardToRemove);
                    cardToRemove?.Dispose();
                }
            }

            this.Close();
        }

        private string PromptForVoidReason()
        {
            using (Form dialog = new Form
            {
                Width  = 360, Height = 165,
                Text   = "Void Reason",
                StartPosition   = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false, MinimizeBox = false
            })
            {
                var lbl    = new Label  { Text = "Reason:", Left = 12, Top = 16, AutoSize = true };
                var txt    = new TextBox { Left = 12, Top = 38, Width = 316, MaxLength = 20 };
                var ok     = new Button { Text = "Void Slip", Left = 75,  Top = 80, Width = 105,
                                          BackColor = Color.Tomato, FlatStyle = FlatStyle.Flat,
                                          DialogResult = DialogResult.None };
                var cancel = new Button { Text = "Cancel", Left = 195, Top = 80, Width = 80,
                                          DialogResult = DialogResult.Cancel };

                // Validate inline — dialog stays open until reason is entered or user cancels
                ok.Click += (s, e) =>
                {
                    if (string.IsNullOrWhiteSpace(txt.Text))
                    {
                        MessageBox.Show(
                            "A void reason is required.\n\nPlease enter a reason before continuing, or click Cancel to abort.",
                            "Reason Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        txt.Focus();
                        return;
                    }
                    dialog.DialogResult = DialogResult.OK;
                };

                dialog.Controls.AddRange(new Control[] { lbl, txt, ok, cancel });
                dialog.AcceptButton = ok;
                dialog.CancelButton = cancel;

                if (dialog.ShowDialog(this) == DialogResult.OK)
                    return txt.Text.Trim();
                return null;
            }
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            string[] fields = CollectFields();

            if (string.IsNullOrWhiteSpace(fields[0]))
            {
                MessageBox.Show("Truck Reg (Field 1) is required before saving.",
                    "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            fields[6] = NormalizeTons(fields[6]);

            if (ExistingSlipId == 0)
            {
                ExistingSlipId = DatabaseManager.InsertSlip(
                    txtBilNumber.Text,
                    fields[0], fields[1], fields[2], fields[3], fields[4],
                    fields[5], fields[6], fields[7], fields[8], fields[9]);

                if (ExistingSlipId <= 0) return;
                txtSlipID.Text = ExistingSlipId.ToString();
            }
            else
            {
                if (!DatabaseManager.UpdateSlipFields(
                    ExistingSlipId,
                    fields[0], fields[1], fields[2], fields[3], fields[4],
                    fields[5], fields[6], fields[7], fields[8], fields[9]))
                    return;
            }

            UpdateOrCreateTile(fields);
            this.Close();
        }

        private void SlipCard_Click(object sender, EventArgs e)
        {
            Button clickedCard = (Button)sender;
            if (!(clickedCard.Tag is Dictionary<string, string> savedData)) return;

            CreateSlip editForm = new CreateSlip();
            editForm.ExistingSlipId    = int.Parse(savedData["SlipID"]);
            editForm.txtSlipID.Text    = savedData["SlipID"];
            editForm.txtBilNumber.Text = savedData["BillNumber"];

            for (int i = 1; i <= 10; i++)
                editForm.SetFieldValue(i, savedData.ContainsKey("Field" + i) ? savedData["Field" + i] : "");

            editForm.Tag = clickedCard;
            editForm.ShowDialog();
        }

        private void btnPrint_Click(object sender, EventArgs e)
        {
            string[] fields = CollectFields();
            fields[6] = NormalizeTons(fields[6]);

            // Field1 (Truck Reg) is always required — spec §4.2/4.4, same strictness as Tons
            if (string.IsNullOrWhiteSpace(fields[0]))
            {
                MessageBox.Show("Truck Reg (Field 1) is required before printing.",
                    "Required Field", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                if (_lookupBoxes.ContainsKey("Field1"))
                    _lookupBoxes["Field1"].Focus();
                else
                    txtField1.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(fields[6]))
            {
                MessageBox.Show("Tons must be filled in before printing.",
                    "Required Field", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtField7.Focus();
                return;
            }

            // Re-check the format here, not just via the Print button's enabled state — spec §4.4
            // requires that a slip can never be printed with an unparseable tonnage even if the
            // UI state is stale. Typing is already filtered, but a pasted value bypasses KeyPress.
            if (!double.TryParse(fields[6], NumberStyles.Any, CultureInfo.InvariantCulture, out _))
            {
                MessageBox.Show("Tons must be a valid number (for example 36,98).\n\n" +
                                "Please correct it before printing.",
                    "Invalid Tons", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtField7.Focus();
                return;
            }

            var fieldConfigs = DatabaseManager.GetActiveFieldConfigurations();
            var missing = new System.Collections.Generic.List<string>();
            for (int i = 1; i <= 10; i++)
            {
                string key = "Field" + i;
                if (!fieldConfigs.ContainsKey(key)) continue;
                var cfg = fieldConfigs[key];
                if (cfg.IsHidden || !cfg.IsRequired) continue;
                if (string.IsNullOrWhiteSpace(fields[i - 1]))
                    missing.Add(cfg.CustomName);
            }
            if (missing.Count > 0)
            {
                MessageBox.Show(
                    "The following required fields must be filled before printing:\n\n• " +
                    string.Join("\n• ", missing),
                    "Required Fields", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (ExistingSlipId == 0)
            {
                ExistingSlipId = DatabaseManager.InsertSlip(
                    txtBilNumber.Text,
                    fields[0], fields[1], fields[2], fields[3], fields[4],
                    fields[5], fields[6], fields[7], fields[8], fields[9]);

                if (ExistingSlipId <= 0) return;
                txtSlipID.Text = ExistingSlipId.ToString();
            }
            else
            {
                // The draft already exists, but the operator may have edited it since the last
                // Save. Without this the paper carries the new values while the database keeps
                // the old ones — the printed record and the stored record must never disagree
                // (spec §6.2, the same proof-of-record rule behind DEF-002).
                if (!DatabaseManager.UpdateSlipFields(
                    ExistingSlipId,
                    fields[0], fields[1], fields[2], fields[3], fields[4],
                    fields[5], fields[6], fields[7], fields[8], fields[9]))
                    return;
            }

            var printPackage = new Dictionary<string, string>
            {
                { "SlipID",     txtSlipID.Text },
                { "BillNumber", txtBilNumber.Text },
            };
            for (int i = 1; i <= 10; i++)
                printPackage.Add("Field" + i, fields[i - 1]);

            using (var preview = new PrintSlipPreview(printPackage))
            {
                if (preview.ShowDialog(this) != DialogResult.OK)
                    return;
            }

            DatabaseManager.MarkSlipAsPrinted(ExistingSlipId);

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

        // Reads current values from all 10 fields — checks ComboBoxes before TextBoxes
        private string[] CollectFields()
        {
            string[] fields = new string[10];
            for (int i = 1; i <= 10; i++)
            {
                string key = "Field" + i;
                if (_lookupBoxes.TryGetValue(key, out ComboBox cb))
                    fields[i - 1] = cb.Text;
                else
                {
                    var matches = this.Controls.Find("txtField" + i, true);
                    fields[i - 1] = (matches.Length > 0 && matches[0] is TextBox tb) ? tb.Text : "";
                }
            }
            return fields;
        }

        private void UpdateOrCreateTile(string[] fields)
        {
            string regDisplay  = fields[0];
            string tonsDisplay = string.IsNullOrWhiteSpace(fields[6]) ? "0" : fields[6];
            string tileText    = $"Reg: {regDisplay}\nSlip: {ExistingSlipId}\nTons: {tonsDisplay}";

            if (this.Tag is Button existingCard)
            {
                existingCard.Text = tileText;
                if (existingCard.Tag is Dictionary<string, string> savedData)
                {
                    savedData["BillNumber"] = txtBilNumber.Text;
                    for (int i = 1; i <= 10; i++)
                        savedData["Field" + i] = fields[i - 1];
                }
                return;
            }

            Button slipCard = new Button
            {
                Size      = new Size(200, 110),
                BackColor = Color.LightYellow,
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Arial", 10, FontStyle.Bold),
                Text      = tileText,
            };

            var memoryCache = new Dictionary<string, string>
            {
                { "SlipID",     ExistingSlipId.ToString() },
                { "BillNumber", txtBilNumber.Text },
            };
            for (int i = 1; i <= 10; i++)
                memoryCache.Add("Field" + i, fields[i - 1]);

            slipCard.Tag    = memoryCache;
            slipCard.Click += SlipCard_Click;

            Main mainPage = (Main)Application.OpenForms["Main"];
            mainPage?.flpSlips.Controls.Add(slipCard);
        }

        private void btnSave_Click_1(object sender, EventArgs e)   => btnSave_Click(sender, e);
        private void btnCancel_Click_1(object sender, EventArgs e) => btnCancel_Click(sender, e);
        private void btnPrint_Click_1(object sender, EventArgs e)  => btnPrint_Click(sender, e);
    }
}
