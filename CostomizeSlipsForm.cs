using System;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Windows.Forms;

namespace SlipManagement2
{
    public partial class CustomizeSlipsForm : Form
    {
        private string dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WeighbridgeData.db");
        private string connectionString;

        // Visual design panels to map dynamically inside Tab 2 to prevent designer glitches
        private TextBox txtCompanyHeader;
        private TextBox txtLogoPath;
        private CheckedListBox chkListSlipFields;

        public CustomizeSlipsForm()
        {
            InitializeComponent();
            connectionString = $"Data Source={dbPath};Version=3;";
            this.StartPosition = FormStartPosition.CenterParent;

            // Wire up the dynamic code layout blocks for both configuration tabs
            SetupSlipDesignTabControls();
            LoadFieldSetupGrid();
            LoadSlipDesignSettings();
        }

        // ==========================================
        // TAB 1 LAYOUT MECHANICS: FIELD SETUP GRID
        // ==========================================
        private void LoadFieldSetupGrid()
        {
            try
            {
                using (SQLiteConnection conn = new SQLiteConnection(connectionString))
                {
                    conn.Open();
                    string query = "SELECT FieldKey AS [Database Slot], CustomName AS [Label Name], PositionOrder AS [Order Line], IsHidden AS [Hidden (1=Yes)] FROM field_config ORDER BY PositionOrder ASC;";

                    using (SQLiteDataAdapter adapter = new SQLiteDataAdapter(query, conn))
                    {
                        DataTable dt = new DataTable();
                        adapter.Fill(dt);
                        dgvFieldSetup.DataSource = dt;
                    }
                }

                // Fine-tune grid behaviors so the manager can click and type changes right into cells
                dgvFieldSetup.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
                dgvFieldSetup.AllowUserToAddRows = false;
                dgvFieldSetup.AllowUserToDeleteRows = false;
                if (dgvFieldSetup.Columns.Contains("Database Slot"))
                {
                    dgvFieldSetup.Columns["Database Slot"].ReadOnly = true; // Protect system schema key definitions
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed loading field layout maps: " + ex.Message);
            }
        }

        // ==========================================
        // TAB 2 LAYOUT MECHANICS: SLIP TEMPLATE DESIGN
        // ==========================================
        private void SetupSlipDesignTabControls()
        {
            // Injecting components directly via code protects layout coordinates from ever disappearing or getting hidden
            Label lblHeader = new Label() { Text = "Company Header Text:", Location = new System.Drawing.Point(20, 25), AutoSize = true, Font = new System.Drawing.Font("Arial", 9, System.Drawing.FontStyle.Bold) };
            txtCompanyHeader = new TextBox() { Location = new System.Drawing.Point(20, 45), Size = new System.Drawing.Size(350, 22) };

            Label lblLogo = new Label() { Text = "Company Image Logo File Path:", Location = new System.Drawing.Point(20, 85), AutoSize = true, Font = new System.Drawing.Font("Arial", 9, System.Drawing.FontStyle.Bold) };
            txtLogoPath = new TextBox() { Location = new System.Drawing.Point(20, 105), Size = new System.Drawing.Size(260, 22) };
            Button btnBrowse = new Button() { Text = "Browse...", Location = new System.Drawing.Point(290, 103), Size = new System.Drawing.Size(80, 25), FlatStyle = FlatStyle.Flat };
            btnBrowse.Click += BtnBrowse_Click;

            Label lblChecklist = new Label() { Text = "Select which fields print out onto final slips:", Location = new System.Drawing.Point(20, 150), AutoSize = true, Font = new System.Drawing.Font("Arial", 9, System.Drawing.FontStyle.Bold) };
            chkListSlipFields = new CheckedListBox() { Location = new System.Drawing.Point(20, 175), Size = new System.Drawing.Size(350, 160), CheckOnClick = true };

            // Drop elements straight into your named tab layout sheet panel surface
            tabSlipDesign.Controls.Add(lblHeader);
            tabSlipDesign.Controls.Add(txtCompanyHeader);
            tabSlipDesign.Controls.Add(lblLogo);
            tabSlipDesign.Controls.Add(txtLogoPath);
            tabSlipDesign.Controls.Add(btnBrowse);
            tabSlipDesign.Controls.Add(lblChecklist);
            tabSlipDesign.Controls.Add(chkListSlipFields);
        }

        private void LoadSlipDesignSettings()
        {
            try
            {
                // Pull active white-labeling text configurations metadata values out of SQLite 
                txtCompanyHeader.Text = DatabaseManager.GetGlobalSetting("HeaderTitle", "UITVAL GRONDE PTY (LTD)");
                txtLogoPath.Text = DatabaseManager.GetGlobalSetting("LogoPath", "");

                // Pull active fields visibility checkboxes statuses out of SQLite
                chkListSlipFields.Items.Clear();
                using (SQLiteConnection conn = new SQLiteConnection(connectionString))
                {
                    conn.Open();
                    string query = "SELECT FieldKey, CustomName, IsHidden FROM field_config ORDER BY PositionOrder ASC;";
                    using (SQLiteCommand cmd = new SQLiteCommand(query, conn))
                    using (SQLiteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string key = reader["FieldKey"].ToString();
                            string name = reader["CustomName"].ToString();
                            int isHidden = Convert.ToInt32(reader["IsHidden"]);

                            string displayString = $"{key} : {name}";
                            // If isHidden is 0, then checked = true (it means field prints!)
                            chkListSlipFields.Items.Add(displayString, isHidden == 0);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error mapping slip template parameters: " + ex.Message);
            }
        }

        private void BtnBrowse_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog() { Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp" })
            {
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    txtLogoPath.Text = ofd.FileName;
                }
            }
        }

        // ==========================================
        // MAIN FORM ACTION DECISIONS BUTTON PIPELINES
        // ==========================================
        private void btnSave_Click(object sender, EventArgs e)
        {
            try
            {
                using (SQLiteConnection conn = new SQLiteConnection(connectionString))
                {
                    conn.Open();

                    // 1. SAVE TAB 1 DATA CHANGES (Cell modifications from Gridview table)
                    DataTable gridData = (DataTable)dgvFieldSetup.DataSource;
                    foreach (DataRow row in gridData.Rows)
                    {
                        string updateGridQuery = @"
                            UPDATE field_config SET 
                                CustomName = @CustomName, 
                                PositionOrder = @PositionOrder, 
                                IsHidden = @IsHidden 
                            WHERE FieldKey = @FieldKey;";

                        using (SQLiteCommand cmd = new SQLiteCommand(updateGridQuery, conn))
                        {
                            cmd.Parameters.AddWithValue("@FieldKey", row["Database Slot"].ToString());
                            cmd.Parameters.AddWithValue("@CustomName", row["Label Name"].ToString());
                            cmd.Parameters.AddWithValue("@PositionOrder", Convert.ToInt32(row["Order Line"]));
                            cmd.Parameters.AddWithValue("@IsHidden", Convert.ToInt32(row["Hidden (1=Yes)"]));
                            cmd.ExecuteNonQuery();
                        }
                    }

                    // 2. SAVE TAB 2 DESIGN CONFIGURATIONS (Branding titles and paths definitions)
                    DatabaseManager.SaveGlobalSetting("HeaderTitle", txtCompanyHeader.Text.Trim());
                    DatabaseManager.SaveGlobalSetting("LogoPath", txtLogoPath.Text.Trim());

                    // 3. SAVE TAB 2 FIELD DISPLAY CHECKBOX STATES Loops
                    for (int i = 0; i < chkListSlipFields.Items.Count; i++)
                    {
                        string itemText = chkListSlipFields.Items[i].ToString();
                        string fieldKey = itemText.Split(':')[0].Trim(); // Isolates e.g. "Field1"
                        int isHiddenStatus = chkListSlipFields.GetItemChecked(i) ? 0 : 1; // Checked = Visible(0), Unchecked = Hidden(1)

                        string updateChecklistQuery = "UPDATE field_config SET IsHidden = @IsHidden WHERE FieldKey = @FieldKey;";
                        using (SQLiteCommand cmd = new SQLiteCommand(updateChecklistQuery, conn))
                        {
                            cmd.Parameters.AddWithValue("@FieldKey", fieldKey);
                            cmd.Parameters.AddWithValue("@IsHidden", isHiddenStatus);
                            cmd.ExecuteNonQuery();
                        }
                    }
                }

                MessageBox.Show("Master Weighbridge template saved and configuration caches successfully updated!", "System Synced"); this.DialogResult = DialogResult.OK; this.Close();
            }
            catch (Exception ex) { MessageBox.Show("Failed executing configuration save queries: " + ex.Message); }
        }
        private void btnExit_Click(object sender, EventArgs e) { this.Close(); }
    }
}