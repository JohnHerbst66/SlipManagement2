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
                dgvFieldSetup.DataSource = DatabaseManager.GetFieldConfigAsDataTable();

                dgvFieldSetup.AutoSizeColumnsMode  = DataGridViewAutoSizeColumnsMode.Fill;
                dgvFieldSetup.AllowUserToAddRows    = false;
                dgvFieldSetup.AllowUserToDeleteRows = false;
                if (dgvFieldSetup.Columns.Contains("Database Slot"))
                    dgvFieldSetup.Columns["Database Slot"].ReadOnly = true;

                // Required is managed by the checklist on the Slip Design tab — hide from grid
                if (dgvFieldSetup.Columns.Contains("Required (1=Yes)"))
                    dgvFieldSetup.Columns["Required (1=Yes)"].Visible = false;

                // Field1 (Truck Reg) and Field7 (Tons) can never be hidden — spec §4.5
                if (dgvFieldSetup.Columns.Contains("Hidden (1=Yes)"))
                {
                    foreach (DataGridViewRow row in dgvFieldSetup.Rows)
                    {
                        string slot = row.Cells["Database Slot"].Value?.ToString() ?? "";
                        if (slot == "Field1" || slot == "Field7")
                            row.Cells["Hidden (1=Yes)"].ReadOnly = true;
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

            Label lblRequired = new Label() { Text = "Select which fields are required before printing:", Location = new System.Drawing.Point(420, 150), AutoSize = true, Font = new System.Drawing.Font("Arial", 9, System.Drawing.FontStyle.Bold) };
            chkListRequiredFields = new CheckedListBox() { Location = new System.Drawing.Point(420, 175), Size = new System.Drawing.Size(350, 200), CheckOnClick = true };
            chkListRequiredFields.ItemCheck += ChkListRequiredFields_ItemCheck;

            tabSlipDesign.Controls.Add(lblHeader);
            tabSlipDesign.Controls.Add(txtCompanyHeader);
            tabSlipDesign.Controls.Add(lblLogo);
            tabSlipDesign.Controls.Add(txtLogoPath);
            tabSlipDesign.Controls.Add(btnBrowse);
            tabSlipDesign.Controls.Add(lblChecklist);
            tabSlipDesign.Controls.Add(chkListSlipFields);
            tabSlipDesign.Controls.Add(lblRequired);
            tabSlipDesign.Controls.Add(chkListRequiredFields);
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

        // Refresh checklist every time the user switches to Tab 2
        private void TbcCustomize_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (tbcCustomize.SelectedTab == tabSlipDesign)
                RefreshChecklistFromGrid();
        }

        // Checkbox change → update DataTable so Tab 1 and the save path see it
        private void ChkListSlipFields_ItemCheck(object sender, ItemCheckEventArgs args)
        {
            string item = chkListSlipFields.Items[args.Index].ToString();

            // Field1 and Field7 are mandatory — reject any change
            if (item.StartsWith("Field1 :") || item.StartsWith("Field7 :"))
            {
                args.NewValue = args.CurrentValue;
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
            string item = chkListRequiredFields.Items[args.Index].ToString();

            // Field7 (Tons) is always required — reject any change
            if (item.StartsWith("Field7 :"))
            {
                args.NewValue = args.CurrentValue;
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
