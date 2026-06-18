using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Windows.Forms;
using System.Drawing;

namespace SlipManagement2
{
    public partial class SlipsHistoryForm : Form
    {
        private string dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WeighbridgeData.db");
        private string connectionString;
        private string currentSelectedBilNumber = "";

        // UI Controls generated dynamically inside your tab control sheets
        private Dictionary<string, Label> viewLabels = new Dictionary<string, Label>();
        private Dictionary<string, TextBox> editTextBoxes = new Dictionary<string, TextBox>();

        // Dynamic Filtering Maps to track column search parameters on the fly
        private Dictionary<string, ComboBox> dynamicFilterDropdowns = new Dictionary<string, ComboBox>();

        public SlipsHistoryForm()
        {
            InitializeComponent();
            connectionString = $"Data Source={dbPath};Version=3;";
            this.Size = new Size(1150, 650); // Expanded width to support dynamic filter headers

            // Initialize programmatic fields setup layouts
            SetupTabPages();
            SetupDynamicFilterRow();

            // Wire up time controls to automatically refresh filters on change
            dtpFromDate.ValueChanged += (s, e) => { LoadUniqueValuesIntoDropdowns(); ExecuteDynamicFilterQuery(); };
            dtpToDate.ValueChanged += (s, e) => { LoadUniqueValuesIntoDropdowns(); ExecuteDynamicFilterQuery(); };

            // Initial startup load data fetch
            LoadUniqueValuesIntoDropdowns();
            ExecuteDynamicFilterQuery();
        }

        // ===================================================================
        // 1. MASTER DYNAMIC FILTER ENGINE: Combines dates, texts, and dropdowns
        // ===================================================================
        private void ExecuteDynamicFilterQuery()
        {
            string fromDate = dtpFromDate.Value.ToString("yyyy-MM-dd") + " 00:00:00";
            string toDate = dtpToDate.Value.ToString("yyyy-MM-dd") + " 23:59:59";

            // Base SQL query string targeting the date boundaries
            string baseQuery = "SELECT * FROM slips WHERE CreatedAt BETWEEN @FromDate AND @ToDate";
            List<SQLiteParameter> paramList = new List<SQLiteParameter>
            {
                new SQLiteParameter("@FromDate", fromDate),
                new SQLiteParameter("@ToDate", toDate)
            };

            // Loop through all 10 pre-allocated fields to append active search inputs dynamically
            var fieldConfigs = DatabaseManager.GetActiveFieldConfigurations();
            for (int i = 1; i <= 10; i++)
            {
                string key = "Field" + i;
                if (dynamicFilterDropdowns.ContainsKey(key))
                {
                    ComboBox cb = dynamicFilterDropdowns[key];
                    // Skip if the user left it blank or chose the wild-card default placeholder
                    if (cb.SelectedItem != null && cb.SelectedItem.ToString() != "[All Data]")
                    {
                        baseQuery += $" AND {key} = @{key}";
                        paramList.Add(new SQLiteParameter($"@{key}", cb.SelectedItem.ToString()));
                    }
                }
            }

            baseQuery += " ORDER BY CreatedAt DESC;";
            RefreshDataGrid(baseQuery, paramList.ToArray());
        }
        // ===================================================================
        // 2. DATA EXTRACTION: Pulls unique existing values into dropdown selectors
        // ===================================================================
        private void LoadUniqueValuesIntoDropdowns()
        {
            string fromDate = dtpFromDate.Value.ToString("yyyy-MM-dd") + " 00:00:00";
            string toDate = dtpToDate.Value.ToString("yyyy-MM-dd") + " 23:59:59";

            var fieldConfigs = DatabaseManager.GetActiveFieldConfigurations();

            foreach (var kvp in dynamicFilterDropdowns)
            {
                string key = kvp.Key;
                ComboBox cb = kvp.Value;

                // Save whatever option the user currently has selected so we don't clear it accidently
                string previousSelection = cb.SelectedItem?.ToString() ?? "[All Data]";

                cb.Items.Clear();
                cb.Items.Add("[All Data]");

                try
                {
                    using (SQLiteConnection conn = new SQLiteConnection(connectionString))
                    {
                        conn.Open();
                        // Distinct query pulls unique occurrences within the current time slice
                        string uniqueQuery = $"SELECT DISTINCT {key} FROM slips WHERE CreatedAt BETWEEN @From AND @To AND {key} IS NOT NULL AND {key} != '';";
                        using (SQLiteCommand cmd = new SQLiteCommand(uniqueQuery, conn))
                        {
                            cmd.Parameters.AddWithValue("@From", fromDate);
                            cmd.Parameters.AddWithValue("@To", toDate);

                            using (SQLiteDataReader reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    cb.Items.Add(reader[key].ToString());
                                }
                            }
                        }
                    }

                    // Restore selection status profile if it still exists inside the new list context
                    if (cb.Items.Contains(previousSelection))
                        cb.SelectedItem = previousSelection;
                    else
                        cb.SelectedItem = "[All Data]";
                }
                catch { /* Quiet fallback handle to prevent empty grid row exceptions */ }
            }
        }

        // ===================================================================
        // 3. UI GENERATION FOR FILTER ROWS: Maps custom headers over selectors
        // ===================================================================
        private void SetupDynamicFilterRow()
        {
            // Position parameters below date controls on panel1
            int startX = 20;
            int startY = 55;
            var fieldConfigs = DatabaseManager.GetActiveFieldConfigurations();

            for (int i = 1; i <= 10; i++)
            {
                string key = "Field" + i;
                if (fieldConfigs.ContainsKey(key))
                {
                    var cfg = fieldConfigs[key];
                    if (cfg.IsHidden) continue; // Skip generating filters for fields marked hidden

                    string cleanTitle = string.IsNullOrWhiteSpace(cfg.CustomName) ? $"Field {i}" : cfg.CustomName.Replace(":", "").Trim();

                    Label lblFilter = new Label() { Text = "Filter " + cleanTitle + ":", Location = new Point(startX, startY), AutoSize = true, Font = new Font("Arial", 8, FontStyle.Bold) };
                    ComboBox cbFilter = new ComboBox() { Location = new Point(startX, startY + 18), Size = new Size(130, 21), DropDownStyle = ComboBoxStyle.DropDownList };

                    // Wire the dropdown choice action directly straight into our automated filtering pipeline query
                    cbFilter.SelectedIndexChanged += (s, e) => ExecuteDynamicFilterQuery();

                    dynamicFilterDropdowns.Add(key, cbFilter);
                    panel1.Controls.Add(lblFilter);
                    panel1.Controls.Add(cbFilter);

                    startX += 145; // Shift rightward for the next column filter box element stack
                    if (startX > this.Width - 300) // Wrap into a second neat line row if too long
                    {
                        startX = 20;
                        startY += 45;
                    }
                }
            }
        }

        private void RefreshDataGrid(string query, SQLiteParameter[] parameters = null)
        {
            try
            {
                using (SQLiteConnection conn = new SQLiteConnection(connectionString))
                {
                    conn.Open();
                    using (SQLiteCommand cmd = new SQLiteCommand(query, conn))
                    {
                        if (parameters != null) cmd.Parameters.AddRange(parameters);
                        using (SQLiteDataAdapter adapter = new SQLiteDataAdapter(cmd))
                        {
                            DataTable dt = new DataTable();
                            adapter.Fill(dt);
                            dataGridView1.DataSource = dt;
                        }
                    }
                }
                dataGridView1.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
                dataGridView1.MultiSelect = false;

                // Remap history column text headers to match active custom names seamlessly
                var fieldConfigs = DatabaseManager.GetActiveFieldConfigurations();
                foreach (var cfg in fieldConfigs)
                {
                    if (dataGridView1.Columns.Contains(cfg.Key))
                    {
                        if (cfg.Value.IsHidden) dataGridView1.Columns[cfg.Key].Visible = false;
                        else
                        {
                            dataGridView1.Columns[cfg.Key].Visible = true;
                            dataGridView1.Columns[cfg.Key].HeaderText = cfg.Value.CustomName;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error reading database archive logs: " + ex.Message);
            }
        }
        // ===================================================================
        // 4. ACTION CONTROLS INTERFACES BUTTON METHODS EXECUTION PIPELINES
        // ===================================================================
        private void dataGridView1_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;

            DataGridViewRow row = dataGridView1.Rows[e.RowIndex];
            currentSelectedBilNumber = row.Cells["BilNumber"].Value.ToString();

            viewLabels["SlipID"].Text = "Slip ID: " + row.Cells["SlipID"].Value.ToString();
            viewLabels["BilNumber"].Text = "Bill Number: " + currentSelectedBilNumber;

            editTextBoxes["SlipID"].Text = row.Cells["SlipID"].Value.ToString();

            for (int i = 1; i <= 10; i++)
            {
                if (row.Cells["Field" + i].Value != null)
                {
                    string val = row.Cells["Field" + i].Value.ToString();
                    viewLabels["Field" + i].Text = $"Field {i}: " + val;
                    editTextBoxes["Field" + i].Text = val;
                }
            }
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(currentSelectedBilNumber))
            {
                MessageBox.Show("Please highlight a slip record inside the grid first!");
                return;
            }

            try
            {
                using (SQLiteConnection conn = new SQLiteConnection(connectionString))
                {
                    conn.Open();
                    string updateQuery = @"
                        UPDATE slips SET 
                            SlipID = @SlipID, Field1 = @F1, Field2 = @F2, Field3 = @F3, Field4 = @F4, 
                            Field5 = @F5, Field6 = @F6, Field7 = @F7, Field8 = @F8, Field9 = @F9, Field10 = @F10 
                        WHERE BilNumber = @BilNumber;";

                    using (SQLiteCommand cmd = new SQLiteCommand(updateQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@BilNumber", currentSelectedBilNumber);
                        cmd.Parameters.AddWithValue("@SlipID", editTextBoxes["SlipID"].Text);
                        for (int i = 1; i <= 10; i++)
                        {
                            cmd.Parameters.AddWithValue($"@F{i}", editTextBoxes["Field" + i].Text);
                        }
                        cmd.ExecuteNonQuery();
                    }
                }
                MessageBox.Show("Slip database modifications updated successfully!", "System Synced");
                ExecuteDynamicFilterQuery();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed updating database row: " + ex.Message);
            }
        }

        private void btnPrint_Click(object sender, EventArgs e)
       
       
        {
            if (dataGridView1.SelectedRows.Count == 0)
            {
                MessageBox.Show("Please highlight an archive log row in the grid first!");
                return;
            }

            DataGridViewRow row = dataGridView1.SelectedRows[0];

            // 1. Pack historical cell columns data directly into the package
            var historyPackage = new Dictionary<string, string>
    {
        { "SlipID", row.Cells["SlipID"].Value?.ToString() ?? "" },
        { "BilNumber", row.Cells["BilNumber"].Value?.ToString() ?? "" }
    };

            for (int i = 1; i <= 10; i++)
            {
                string val = row.Cells["Field" + i].Value?.ToString() ?? "";
                historyPackage.Add("Field" + i, val);
            }

            // 2. EXECUTE THE PRINT JOB USING THE SAME ENGINE LOGIC LAYER
            SlipPrintEngine.ExecutePrintJob(historyPackage);

            MessageBox.Show("Historical slip copy sent to printer successfully!", "Process Complete");
        }



        private void btnExport_Click(object sender, EventArgs e)
        {
            if (dataGridView1.Rows.Count == 0) return;

            using (SaveFileDialog sfd = new SaveFileDialog() { Filter = "CSV File|*.csv", FileName = "Weighbridge_History_Export.csv" })
            {
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        List<string> lines = new List<string> { "BilNumber,SlipID,Field1,Field2,Field3,Field4,Field5,Field6,Field7,Field8,Field9,Field10,CreatedAt" };
                        foreach (DataGridViewRow row in dataGridView1.Rows)
                        {
                            if (row.IsNewRow) continue;
                            string line = $"{row.Cells["BilNumber"].Value},{row.Cells["SlipID"].Value},";
                            for (int i = 1; i <= 10; i++) { line += $"{row.Cells["Field" + i].Value},"; }
                            line += $"{row.Cells["CreatedAt"].Value}";
                            lines.Add(line);
                        }
                        File.WriteAllLines(sfd.FileName, lines);
                        MessageBox.Show("Filtered history logs exported successfully into a clean Excel CSV spreadsheet!", "Export Saved");
                    }
                    catch (Exception ex) { MessageBox.Show("Export process failed: " + ex.Message); }
                }
            }
        }

        private void btnExit_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void SetupTabPages()
        {
            int yPos = 15;
            string[] keys = { "SlipID", "BilNumber", "Field1", "Field2", "Field3", "Field4", "Field5", "Field6", "Field7", "Field8", "Field9", "Field10" };

            // Tab 1 UI View Setup Elements
            TabPage tab1 = tabControl1.TabPages[0];
            foreach (var key in keys)
            {
                Label lbl = new Label() { Text = key + ": ----", Location = new Point(15, yPos), AutoSize = true, Font = new Font("Arial", 9) };
                viewLabels.Add(key, lbl);
                tab1.Controls.Add(lbl);
                yPos += 24;
            }

            // Tab 2 UI Edit Setup Elements
            yPos = 15;
            TabPage tab2 = tabControl1.TabPages[1];
            foreach (var key in keys)
            {
                if (key == "BilNumber") continue; // Bill primary keys are locked from manual modification
                Label lblDesc = new Label() { Text = "Edit " + key + ":", Location = new Point(10, yPos), AutoSize = true, Font = new Font("Arial", 9) };
                TextBox txt = new TextBox() { Location = new Point(110, yPos - 3), Size = new Size(140, 20) };

                editTextBoxes.Add(key, txt);
                tab2.Controls.Add(lblDesc);
                tab2.Controls.Add(txt);
                yPos += 28;
            }
        }
        // Paste these blocks to clear the remaining 3 compiler event errors:
        private void dtpFromDate_ValueChanged(object sender, EventArgs e)
        {
            // The calendar dates updates are already being handled by your dynamic inline wires!
        }

        private void dtpToDate_ValueChanged(object sender, EventArgs e)
        {
            // The calendar dates updates are already being handled by your dynamic inline wires!
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.Close(); // Safely closes the archive window loop
        }

    }
}
