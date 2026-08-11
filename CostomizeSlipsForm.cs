using System;
using System.Collections.Generic;
using System.Data;
using System.Windows.Forms;

namespace SlipManagement2
{
    public partial class CustomizeSlipsForm : Form
    {
        private TextBox txtCompanyHeader;
        private TextBox txtLogoPath;
        private Panel pnlSlipFields;
        private Panel pnlRequiredFields;
        private bool _loadingChecklists = false;

        // Labels as they were when the screen opened, so Save can report exactly what changed
        private readonly Dictionary<string, string> _originalLabels = new Dictionary<string, string>();

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
                const int hintH   = 22;
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

                var lblTypeHere = new Label
                {
                    Text      = "Type here  ↓",
                    AutoSize  = true,
                    Font      = new System.Drawing.Font("Arial", 9, System.Drawing.FontStyle.Bold | System.Drawing.FontStyle.Italic),
                    ForeColor = System.Drawing.Color.FromArgb(180, 0, 0),
                };
                tabFieldSetup.Controls.Add(lblTypeHere);

                int totalShift = bannerH + hintH + gapH;
                dgvFieldSetup.Top    += totalShift;
                dgvFieldSetup.Height -= totalShift;

                void PositionTypeHereHint()
                {
                    int dataW   = dgvFieldSetup.Width - dgvFieldSetup.RowHeadersWidth;
                    int colLeft = dgvFieldSetup.Left + dgvFieldSetup.RowHeadersWidth + dataW / 2;
                    lblTypeHere.Location = new System.Drawing.Point(colLeft + 8, dgvFieldSetup.Top - hintH - 2);
                }
                PositionTypeHereHint();
                dgvFieldSetup.Resize += (s, e) => PositionTypeHereHint();

                dgvFieldSetup.DataSource = DatabaseManager.GetFieldConfigAsDataTable();

                _originalLabels.Clear();
                foreach (DataRow row in ((DataTable)dgvFieldSetup.DataSource).Rows)
                    _originalLabels[row["Database Slot"].ToString()] = row["Label Name"].ToString();

                dgvFieldSetup.AutoSizeColumnsMode  = DataGridViewAutoSizeColumnsMode.Fill;
                dgvFieldSetup.AllowUserToAddRows    = false;
                dgvFieldSetup.AllowUserToDeleteRows = false;

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
            pnlSlipFields = new Panel()
            {
                Location    = new System.Drawing.Point(20, 175),
                Size        = new System.Drawing.Size(350, 200),
                AutoScroll  = true,
                BorderStyle = BorderStyle.Fixed3D,
                BackColor   = System.Drawing.SystemColors.Window
            };

            Label lblRequired = new Label() { Text = "Select which fields are required before printing:", Location = new System.Drawing.Point(520, 150), AutoSize = true, Font = new System.Drawing.Font("Arial", 9, System.Drawing.FontStyle.Bold) };
            pnlRequiredFields = new Panel()
            {
                Location    = new System.Drawing.Point(520, 175),
                Size        = new System.Drawing.Size(350, 200),
                AutoScroll  = true,
                BorderStyle = BorderStyle.Fixed3D,
                BackColor   = System.Drawing.SystemColors.Window
            };

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
            tabSlipDesign.Controls.Add(pnlSlipFields);
            tabSlipDesign.Controls.Add(lblRequired);
            tabSlipDesign.Controls.Add(pnlRequiredFields);
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

        // Lists any label changes and spells out where they will show up, so a rename is never
        // a silent surprise. Returns false if the operator backs out.
        private bool ConfirmLabelChanges()
        {
            dgvFieldSetup.EndEdit();
            var dt = (DataTable)dgvFieldSetup.DataSource;
            if (dt == null) return true;

            var renames = new List<string>();
            foreach (DataRow row in dt.Rows)
            {
                string slot = row["Database Slot"].ToString();
                string now  = row["Label Name"].ToString();
                if (_originalLabels.TryGetValue(slot, out string was) && was != now)
                {
                    string wasShown = string.IsNullOrWhiteSpace(was) ? "(blank)" : "\"" + was + "\"";
                    string nowShown = string.IsNullOrWhiteSpace(now) ? "(blank)" : "\"" + now + "\"";
                    renames.Add("     " + slot + ":   " + wasShown + "   →   " + nowShown);
                }
            }
            if (renames.Count == 0) return true;

            string msg =
                (renames.Count == 1 ? "You are renaming a field:" : "You are renaming " + renames.Count + " fields:")
                + "\n\n" + string.Join("\n", renames) + "\n\n"
                + "The new label will appear everywhere that field is shown:\n\n"
                + "     •  the Create Slip data entry form\n"
                + "     •  the Slip History grid, its filter row and the View and Edit tabs\n"
                + "     •  the printed slip\n"
                + "     •  the Excel export column headings\n"
                + "     •  the Main Page tiles, if the field is shown there\n"
                + "     •  the daily summary breakdown, if it is grouped by this field\n"
                + "     •  the Manage Lookups list selector\n\n"
                + "Slips already recorded are NOT changed. A slip stores the value that was "
                + "entered, never the label, so past records keep reading exactly as they were "
                + "printed.\n\n"
                + "One thing that does NOT follow the new name: the dropdown suggestions for "
                + "this field. Those still come from the list it was originally attached to.\n\n"
                + "Continue?";

            return MessageBox.Show(msg, "Renaming Fields",
                MessageBoxButtons.OKCancel, MessageBoxIcon.Information) == DialogResult.OK;
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            try
            {
                if (!ConfirmLabelChanges()) return;

                DatabaseManager.SaveFieldConfigFromDataTable((DataTable)dgvFieldSetup.DataSource);
                DatabaseManager.SaveGlobalSetting("HeaderTitle", txtCompanyHeader.Text.Trim());
                DatabaseManager.SaveGlobalSetting("LogoPath",    txtLogoPath.Text.Trim());

                MessageBox.Show("Configuration saved successfully.", "Saved");
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed saving configuration: " + ex.Message);
            }
        }

        // Rebuild checkbox panels from current DataTable state
        private void RefreshChecklistFromGrid()
        {
            DataTable dt = (DataTable)dgvFieldSetup.DataSource;
            if (dt == null) return;

            dgvFieldSetup.EndEdit();

            _loadingChecklists = true;
            try
            {
                pnlSlipFields.Controls.Clear();
                pnlRequiredFields.Controls.Clear();

                int y = 4;
                foreach (DataRow row in dt.Rows)
                {
                    string slot     = row["Database Slot"].ToString();
                    string label    = row["Label Name"].ToString();
                    bool   visible  = Convert.ToInt32(row["Hidden (1=Yes)"])   == 0;
                    bool   required = Convert.ToInt32(row["Required (1=Yes)"]) == 1;
                    bool   locked   = slot == "Field1" || slot == "Field7";
                    string text     = $"{slot} : {label}";

                    var chkVisible = new CheckBox()
                    {
                        Text     = text,
                        Location = new System.Drawing.Point(4, y),
                        AutoSize = true,
                        Checked  = visible,
                        Enabled  = !locked,
                        Tag      = slot
                    };
                    if (!locked) chkVisible.CheckedChanged += ChkVisible_CheckedChanged;
                    pnlSlipFields.Controls.Add(chkVisible);

                    var chkRequired = new CheckBox()
                    {
                        Text     = text,
                        Location = new System.Drawing.Point(4, y),
                        AutoSize = true,
                        Checked  = required,
                        Enabled  = !locked,
                        Tag      = slot
                    };
                    if (!locked) chkRequired.CheckedChanged += ChkRequired_CheckedChanged;
                    pnlRequiredFields.Controls.Add(chkRequired);

                    y += 22;
                }
            }
            finally
            {
                _loadingChecklists = false;
            }
        }

        private void TbcCustomize_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (tbcCustomize.SelectedTab == tabSlipDesign)
                RefreshChecklistFromGrid();
        }

        private void ChkVisible_CheckedChanged(object sender, EventArgs e)
        {
            if (_loadingChecklists) return;
            var chk      = (CheckBox)sender;
            string slot  = chk.Tag.ToString();
            int newHidden = chk.Checked ? 0 : 1;

            DataTable dt = (DataTable)dgvFieldSetup.DataSource;
            if (dt == null) return;
            foreach (DataRow row in dt.Rows)
            {
                if (row["Database Slot"].ToString() == slot)
                {
                    row["Hidden (1=Yes)"] = newHidden;
                    break;
                }
            }
        }

        private void ChkRequired_CheckedChanged(object sender, EventArgs e)
        {
            if (_loadingChecklists) return;
            var chk       = (CheckBox)sender;
            string slot   = chk.Tag.ToString();
            int newRequired = chk.Checked ? 1 : 0;

            DataTable dt = (DataTable)dgvFieldSetup.DataSource;
            if (dt == null) return;
            foreach (DataRow row in dt.Rows)
            {
                if (row["Database Slot"].ToString() == slot)
                {
                    row["Required (1=Yes)"] = newRequired;
                    break;
                }
            }
        }

        private void btnExit_Click(object sender, EventArgs e) => this.Close();
    }
}
