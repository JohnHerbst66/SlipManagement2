using System;
using System.Data;
using System.Windows.Forms;

namespace SlipManagement2
{
    public partial class CustomizeSlipsForm : Form
    {
        private TextBox txtCompanyHeader;
        private TextBox txtLogoPath;
        private CheckedListBox chkListSlipFields;
        private CheckedListBox chkListRequiredFields;
        private bool _loadingChecklists = false;

        public CustomizeSlipsForm()
        {
            InitializeComponent();
            this.StartPosition = FormStartPosition.CenterParent;

            SetupSlipDesignTabControls();
            LoadFieldSetupGrid();
            LoadSlipDesignSettings();

            tbcCustomize.SelectedIndexChanged += TbcCustomize_SelectedIndexChanged;
        }

        private void LoadFieldSetupGrid()
        {
            try
            {
                // Instruction banner — 52px gives 2 lines enough room at 100-150% DPI
                const int bannerH = 52;
                const int hintH   = 22;  // "Type here ↓" row
                const int gapH    = 4;

                var lblHint = new Label
                {
                    Text      = "Edit the Label Name column to rename fields shown throughout the application.\r\n" +
                                "Database Slot is for reference only and cannot be changed. To control field visibility and required settings, use the Slip Design tab.",
                    Location  = new System.Drawing.Point(dgvFieldSetup.Left, 6),
                    AutoSize  = false,
                    Size      = new System.Drawing.Size(dgvFieldSetup.Width, bannerH),
                    Font      = new System.Drawing.Font("Arial", 9),
                    ForeColor = System.Drawing.Color.FromArgb(30, 60, 110),
                    Anchor    = System.Windows.Forms.AnchorStyles.Top
                              | System.Windows.Forms.AnchorStyles.Left
                              | System.Windows.Forms.AnchorStyles.Right,
                };
                tabFieldSetup.Controls.Add(lblHint);

                // "Type here ↓" floats just above the Label Name column header
                var lblTypeHere = new Label
                {
                    Text     = "Type here  ↓",
                    AutoSize = true,
                    Font     = new System.Drawing.Font("Arial", 9, System.Drawing.FontStyle.Bold | System.Drawing.FontStyle.Italic),
                    ForeColor = System.Drawing.Color.FromArgb(180, 0, 0),
                };
                tabFieldSetup.Controls.Add(lblTypeHere);

                int totalShift = bannerH + hintH + gapH;
                dgvFieldSetup.Top    += totalShift;
                dgvFieldSetup.Height -= totalShift;

                // Align hint over the Label Name column; recalculates when the grid is resized
                void PositionTypeHereHint()
                {
                    int dataW   = dgvFieldSetup.Width - dgvFieldSetup.RowHeadersWidth;
                    int colLeft = dgvFieldSetup.Left + dgvFieldSetup.RowHeadersWidth + dataW / 2;
                    lblTypeHere.Location = new System.Drawing.Point(colLeft + 8, dgvFieldSetup.Top - hintH - 2);
                }
                PositionTypeHereHint();
                dgvFieldSetup.Resize += (s, e) => PositionTypeHereHint();

                dgvFieldSetup.DataSource = DatabaseManager.GetFieldConfigAsDataTable();

                dgvFieldSetup.AutoSizeColumnsMode  = DataGridViewAutoSizeColumnsMode.Fill;
                dgvFieldSetup.AllowUserToAddRows    = false;
                dgvFieldSetup.AllowUserToDeleteRows = false;

                // Only show Database Slot (read-only reference) and Label Name (editable).
                // Order Line, Hidden, and Required are managed elsewhere.
                foreach (DataGridViewColumn col in dgvFieldSetup.Columns)
                {
                    switch (col.Name)
                    {
                        case "Label Name":
                            col.Visible  = true;
                            col.ReadOnly = false;
                            break;
                        case "Database Slot":
                            col.Visible  = true;
                            col.ReadOnly = true;
                            break;
                        default:
                            col.Visible = false;
                            break;
                    }
                }

                // Enforce 14-char max on Label Name column via the editing TextBox
                dgvFieldSetup.EditingControlShowing += DgvFieldSetup_EditingControlShowing;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed loading field layout: " + ex.Message);
            }
        }

        private void DgvFieldSetup_EditingControlShowing(object sender, DataGridViewEditingControlShowingEventArgs e)
        {
            if (e.Control is TextBox tb)
            {
                bool isLabelColumn = dgvFieldSetup.CurrentCell?.OwningColumn?.HeaderText == "Label Name";
                tb.MaxLength = isLabelColumn ? 14 : int.MaxValue;
            }
        }

        private void SetupSlipDesignTabControls()
        {
            Label lblHeader = new Label() { Text = "Company Header Text:", Location = new System.Drawing.Point(20, 25), AutoSize = true, Font = new System.Drawing.Font("Arial", 9, System.Drawing.FontStyle.Bold) };
            txtCompanyHeader = new TextBox() { Location = new System.Drawing.Point(20, 45), Size = new System.Drawing.Size(350, 22) };

            Label lblLogo = new Label() { Text = "Company Image Logo File Path:", Location = new System.Drawing.Point(20, 85), AutoSize = true, Font = new System.Drawing.Font("Arial", 9, System.Drawing.FontStyle.Bold) };
            txtLogoPath = new TextBox() { Location = new System.Drawing.Point(20, 105), Size = new System.Drawing.Size(260, 22) };
            Button btnBrowse = new Button() { Text = "Browse...", Location = new System.Drawing.Point(290, 103), Size = new System.Drawing.Size(80, 25), FlatStyle = FlatStyle.Flat };
            btnBrowse.Click += BtnBrowse_Click;

            Label lblChecklist = new Label() { Text = "Select which fields print on the final slip:", Location = new System.Drawing.Point(20, 150), AutoSize = true, Font = new System.Drawing.Font("Arial", 9, System.Drawing.FontStyle.Bold) };
            chkListSlipFields = new CheckedListBox() { Location = new System.Drawing.Point(20, 175), Size = new System.Drawing.Size(350, 200), CheckOnClick = true };
            chkListSlipFields.ItemCheck += ChkListSlipFields_ItemCheck;

            Label lblRequired = new Label() { Text = "Select which fields are required before printing:", Location = new System.Drawing.Point(520, 150), AutoSize = true, Font = new System.Drawing.Font("Arial", 9, System.Drawing.FontStyle.Bold) };
            chkListRequiredFields = new CheckedListBox() { Location = new System.Drawing.Point(520, 175), Size = new System.Drawing.Size(350, 200), CheckOnClick = true };
            chkListRequiredFields.ItemCheck += ChkListRequiredFields_ItemCheck;

            Label lblSlipNote = new Label()
            {
                Text      = "*Field1(Truck Reg) and Field7(Tons/Weight) are always printed and cannot be unchecked.",
                Location  = new System.Drawing.Point(20, 380),
                AutoSize  = true,
                Font      = new System.Drawing.Font("Arial", 8, System.Drawing.FontStyle.Italic),
                ForeColor = System.Drawing.Color.FromArgb(120, 80, 0),
            };
            Label lblReqNote = new Label()
            {
                Text      = "* Field1 (Truck Reg) and Field7 (Tons/Weight) are always required and cannot be unchecked.",
                Location  = new System.Drawing.Point(520, 380),
                AutoSize  = true,
                Font      = new System.Drawing.Font("Arial", 8, System.Drawing.FontStyle.Italic),
                ForeColor = System.Drawing.Color.FromArgb(120, 80, 0),
            };

            tabSlipDesign.Controls.Add(lblHeader);
            tabSlipDesign.Controls.Add(txtCompanyHeader);
            tabSlipDesign.Controls.Add(lblLogo);
            tabSlipDesign.Controls.Add(txtLogoPath);
            tabSlipDesign.Controls.Add(btnBrowse);
            tabSlipDesign.Controls.Add(lblChecklist);
            tabSlipDesign.Controls.Add(chkListSlipFields);
            tabSlipDesign.Controls.Add(lblRequired);
            tabSlipDesign.Controls.Add(chkListRequiredFields);
            tabSlipDesign.Controls.Add(lblSlipNote);
            tabSlipDesign.Controls.Add(lblReqNote);
        }

        private void LoadSlipDesignSettings()
        {
            try
            {
                txtCompanyHeader.Text = DatabaseManager.GetGlobalSetting("HeaderTitle", "UITVAL GRONDE PTY (LTD)");
                txtLogoPath.Text      = DatabaseManager.GetGlobalSetting("LogoPath", "");

                RefreshChecklistFromGrid();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading slip design settings: " + ex.Message);
            }
        }

        private void BtnBrowse_Click(object sender, EventArgs e)
        {
            using (var ofd = new OpenFileDialog() { Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp" })
            {
                if (ofd.ShowDialog() == DialogResult.OK)
                    txtLogoPath.Text = ofd.FileName;
            }
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            try
            {
                // Tab 1 — save grid edits back to FieldConfig
                DatabaseManager.SaveFieldConfigFromDataTable((DataTable)dgvFieldSetup.DataSource);

                // Tab 2 — save branding settings
                DatabaseManager.SaveGlobalSetting("HeaderTitle", txtCompanyHeader.Text.Trim());
                DatabaseManager.SaveGlobalSetting("LogoPath",    txtLogoPath.Text.Trim());
                // Tab 2 checklist writes directly to the DataTable above — no separate save needed

                MessageBox.Show("Configuration saved successfully.", "Saved");
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed saving configuration: " + ex.Message);
            }
        }

        // Rebuild checklists from current DataTable state — called on form load and on tab switch
        private void RefreshChecklistFromGrid()
        {
            DataTable dt = (DataTable)dgvFieldSetup.DataSource;
            if (dt == null) return;

            dgvFieldSetup.EndEdit(); // commit any in-progress cell edit before reading

            // Suppress ItemCheck handlers during programmatic loading — WinForms fires ItemCheck
            // when Items.Add() is called with a pre-checked state, which would trigger spurious popups.
            _loadingChecklists = true;
            try
            {
                chkListSlipFields.Items.Clear();
                chkListRequiredFields.Items.Clear();
                foreach (DataRow row in dt.Rows)
                {
                    string slot     = row["Database Slot"].ToString();
                    string label    = row["Label Name"].ToString();
                    bool   visible  = Convert.ToInt32(row["Hidden (1=Yes)"])   == 0;
                    bool   required = Convert.ToInt32(row["Required (1=Yes)"]) == 1;
                    chkListSlipFields.Items.Add($"{slot} : {label}", visible);
                    chkListRequiredFields.Items.Add($"{slot} : {label}", required);
                }
            }
            finally
            {
                _loadingChecklists = false;
            }
        }

        // Refresh checklist every time the user switches to Tab 2
        private void TbcCustomize_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (tbcCustomize.SelectedTab == tabSlipDesign)
                RefreshChecklistFromGrid();
        }

        // Checkbox change → update DataTable so Tab 1 and the save path see it
        private void ChkListSlipFields_ItemCheck(object sender, ItemCheckEventArgs args)
        {
            if (_loadingChecklists) return;

            string item = chkListSlipFields.Items[args.Index].ToString();

            // Field1 and Field7 are mandatory — reject any change and explain why
            if (item.StartsWith("Field1 :") || item.StartsWith("Field7 :"))
            {
                args.NewValue = args.CurrentValue;
                this.BeginInvoke(new Action(() =>
                    MessageBox.Show(
                        "Field1 (Truck Reg) and Field7 (Tons/Weight) are required system fields.\r\n" +
                        "They are always printed on slips and cannot be hidden.",
                        "Field Locked", MessageBoxButtons.OK, MessageBoxIcon.Information)));
                return;
            }

            string fieldSlot = item.Split(':')[0].Trim();
            int newHidden = args.NewValue == CheckState.Checked ? 0 : 1;

            DataTable dt = (DataTable)dgvFieldSetup.DataSource;
            if (dt == null) return;
            foreach (DataRow row in dt.Rows)
            {
                if (row["Database Slot"].ToString() == fieldSlot)
                {
                    row["Hidden (1=Yes)"] = newHidden;
                    break;
                }
            }
        }

        private void ChkListRequiredFields_ItemCheck(object sender, ItemCheckEventArgs args)
        {
            if (_loadingChecklists) return;

            string item = chkListRequiredFields.Items[args.Index].ToString();

            // Field1 (Truck Reg) and Field7 (Tons) are always required — spec §4.2/4.4
            if (item.StartsWith("Field1 :") || item.StartsWith("Field7 :"))
            {
                args.NewValue = args.CurrentValue;
                this.BeginInvoke(new Action(() =>
                    MessageBox.Show(
                        "Field1 (Truck Reg) and Field7 (Tons/Weight) are required system fields.\r\n" +
                        "They must always be filled in before printing and cannot be made optional.",
                        "Field Locked", MessageBoxButtons.OK, MessageBoxIcon.Information)));
                return;
            }

            string fieldSlot  = item.Split(':')[0].Trim();
            int    newRequired = args.NewValue == CheckState.Checked ? 1 : 0;

            DataTable dt = (DataTable)dgvFieldSetup.DataSource;
            if (dt == null) return;
            foreach (DataRow row in dt.Rows)
            {
                if (row["Database Slot"].ToString() == fieldSlot)
                {
                    row["Required (1=Yes)"] = newRequired;
                    break;
                }
            }
        }

        private void btnExit_Click(object sender, EventArgs e) => this.Close();
    }
}
