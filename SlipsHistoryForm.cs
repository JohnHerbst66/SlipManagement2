using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SQLite;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SlipManagement2
{
    public partial class SlipsHistoryForm : Form
    {
        private string dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WeighbridgeData.db");
        private string connectionString;
        private string currentSelectedBilNumber = "";

        // UI Controls for Tab Pages (Will be generated programmatically to avoid designer glitches)
        private Dictionary<string, Label> viewLabels = new Dictionary<string, Label>();
        private Dictionary<string, TextBox> editTextBoxes = new Dictionary<string, TextBox>();

        
        public SlipsHistoryForm()
        {
            InitializeComponent();
            connectionString = $"Data Source={dbPath};Version=3;";
            this.Text = "Weighbridge Archive & Management Center";

            // Wire up the calendar inputs dynamically to filter instantly when changed
            dtpFromDate.ValueChanged += FilterByDateRange;
            dtpToDate.ValueChanged += FilterByDateRange;

            SetupTabPages();
            RefreshDataGrid("SELECT * FROM slips ORDER BY CreatedAt DESC;");
        }
        // 1. REFRESH DATA GRID: Fetches data rows into the spreadsheet grid view
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
                // Custom grid visual properties for clear selection
                dataGridView1.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
                dataGridView1.MultiSelect = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error reading database archive: " + ex.Message);
            }
        }
        private void FilterByDateRange(object sender, EventArgs e)
        {
            string fromDate = dtpFromDate.Value.ToString("yyyy-MM-dd") + " 00:00:00";
            string toDate = dtpToDate.Value.ToString("yyyy-MM-dd") + " 23:59:59";

            string query = "SELECT * FROM slips WHERE CreatedAt BETWEEN @FromDate AND @ToDate ORDER BY CreatedAt DESC;";
            SQLiteParameter[] p = new SQLiteParameter[]
            {
                new SQLiteParameter("@FromDate", fromDate),
                new SQLiteParameter("@ToDate", toDate)
            };

            RefreshDataGrid(query, p);
        }
        // 3. GRID CELL CLICK: Populates Tab Page 1 (View Only) and Tab Page 2 (Edit Input Boxes)
        private void dataGridView1_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return; // Prevent crashing on header clicks

            DataGridViewRow row = dataGridView1.Rows[e.RowIndex];
            currentSelectedBilNumber = row.Cells["BilNumber"].Value.ToString();

            // Populate Tab 1 Labels (View Mode)
            viewLabels["SlipID"].Text = "Slip ID: " + row.Cells["SlipID"].Value.ToString();
            viewLabels["BilNumber"].Text = "Bill Number: " + currentSelectedBilNumber;
            for (int i = 1; i <= 7; i++)
            {
                viewLabels["Field" + i].Text = $"Field {i}: " + row.Cells["Field" + i].Value.ToString();
            }

            // Populate Tab 2 TextBoxes (Edit Mode)
            editTextBoxes["SlipID"].Text = row.Cells["SlipID"].Value.ToString();
            for (int i = 1; i <= 7; i++)
            {
                editTextBoxes["Field" + i].Text = row.Cells["Field" + i].Value.ToString();
            }
        }


        private void btnPrint_Click(object sender, EventArgs e)
        {
            if (dataGridView1.SelectedRows.Count == 0)
            {
                MessageBox.Show("Please select a completed row to reprint!");
                return;
            }

            DataGridViewRow row = dataGridView1.SelectedRows[0];
            PrintSlipPreview reprint = new PrintSlipPreview();

            // Inject the selected historical row data straight into the print engine text fields
            reprint.lblSlipIdOutput.Text = row.Cells["SlipID"].Value.ToString();
            reprint.lblBilNumberOutput.Text = row.Cells["BilNumber"].Value.ToString();
            reprint.lblOutput1.Text = row.Cells["Field1"].Value.ToString();
            reprint.lblOutput2.Text = row.Cells["Field2"].Value.ToString();
            reprint.lblOutput3.Text = row.Cells["Field3"].Value.ToString();
            reprint.lblOutput4.Text = row.Cells["Field4"].Value.ToString();
            reprint.lblOutput5.Text = row.Cells["Field5"].Value.ToString();
            reprint.lblOutput6.Text = row.Cells["Field6"].Value.ToString();
            reprint.lblOutput7.Text = row.Cells["Field7"].Value.ToString();

            // Overwrite the printer routine to NOT wipe the card since it's already an archive row
            reprint.ShowDialog();

        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(currentSelectedBilNumber))
            {
                MessageBox.Show("Please highlight a slip record in the grid view list first!");
                return;
            }

            try
            {
                using (SQLiteConnection conn = new SQLiteConnection(connectionString))
                {
                    conn.Open();
                    string updateQuery = @"
                        UPDATE slips SET 
                            SlipID = @SlipID, Field1 = @F1, Field2 = @F2, Field3 = @F3, 
                            Field4 = @F4, Field5 = @F5, Field6 = @F6, Field7 = @F7 
                        WHERE BilNumber = @BilNumber;";

                    using (SQLiteCommand cmd = new SQLiteCommand(updateQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@BilNumber", currentSelectedBilNumber);
                        cmd.Parameters.AddWithValue("@SlipID", editTextBoxes["SlipID"].Text);
                        cmd.Parameters.AddWithValue("@F1", editTextBoxes["Field1"].Text);
                        cmd.Parameters.AddWithValue("@F2", editTextBoxes["Field2"].Text);
                        cmd.Parameters.AddWithValue("@F3", editTextBoxes["Field3"].Text);
                        cmd.Parameters.AddWithValue("@F4", editTextBoxes["Field4"].Text);
                        cmd.Parameters.AddWithValue("@F5", editTextBoxes["Field5"].Text);
                        cmd.Parameters.AddWithValue("@F6", editTextBoxes["Field6"].Text);
                        cmd.Parameters.AddWithValue("@F7", editTextBoxes["Field7"].Text);

                        cmd.ExecuteNonQuery();
                    }
                }
                MessageBox.Show("Slip modifications updated successfully!", "Database Synced");
                FilterByDateRange(null, null); // Refresh the active spreadsheet view grid
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed updating database table: " + ex.Message);
            }

        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void btnExport_Click(object sender, EventArgs e)
        {

            if (dataGridView1.Rows.Count == 0)
            {
                MessageBox.Show("No active filtered records available to export!");
                return;
            }

            using (SaveFileDialog sfd = new SaveFileDialog() { Filter = "CSV File|*.csv", FileName = "Weighbridge_Export.csv" })
            {
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        List<string> lines = new List<string>();

                        // Gather Excel spreadsheet columns header tokens
                        string headers = "BilNumber,SlipID,Field1,Field2,Field3,Field4,Field5,Field6,Field7,IsPrinted,CreatedAt";
                        lines.Add(headers);

                        // Extract data text string arrays loops
                        foreach (DataGridViewRow row in dataGridView1.Rows)
                        {
                            if (row.IsNewRow) continue;
                            string line = $"{row.Cells["BilNumber"].Value},{row.Cells["SlipID"].Value}," +
                                         $"{row.Cells["Field1"].Value},{row.Cells["Field2"].Value},{row.Cells["Field3"].Value}," +
                                         $"{row.Cells["Field4"].Value},{row.Cells["Field5"].Value},{row.Cells["Field6"].Value}," +
                                         $"{row.Cells["Field7"].Value},{row.Cells["IsPrinted"].Value},{row.Cells["CreatedAt"].Value}";
                            lines.Add(line);
                        }

                        File.WriteAllLines(sfd.FileName, lines);
                        MessageBox.Show("Data exported successfully into a clean Excel CSV file structure!", "Export Complete");
                    }
                    catch (Exception ex) { MessageBox.Show("Export process failed: " + ex.Message); }
                }
            }
        }

                    

        private void dtpFromDate_ValueChanged(object sender, EventArgs e)
        {

        }

        private void dtpToDate_ValueChanged(object sender, EventArgs e)
        {

        }

        private void DataGridView1_CellClick(object sender, DataGridViewCellEventArgs e)
        {

        }
        // PROGRAMMATIC TAB FIELDS SETUP ENGINE (Guarantees zero UI panel alignment corruption)
        private void SetupTabPages(){
            // Tab 1 Setup (View Fields Mode)
          int yPos = 20;
            string[] keys = { "SlipID", "BilNumber", "Field1", "Field2", "Field3", "Field4", "Field5", "Field6", "Field7" };
            foreach (var key in keys){Label lbl = new Label() 
            { 
                Text = key + ": ----", Location = new System.Drawing.Point(20, yPos), AutoSize = true, Font = new System.Drawing.Font("Arial", 10)
            };
                viewLabels.Add(key, lbl);
                tabControl1.TabPages[0].Controls.Add(lbl);yPos += 30;
            }//
            // Tab 2 Setup (Edit Input Boxes Mode)
            yPos = 20;
            foreach (var key in keys)
            {
                if (key == "BilNumber") continue; // Bill Number is the unique key and must never change
                Label lblDesc = new Label() { Text = "Edit " + key + ":", Location = new System.Drawing.Point(15, yPos), AutoSize = true };
                TextBox txt = new TextBox() { Location = new System.Drawing.Point(120, yPos - 3), Size = new System.Drawing.Size(130, 20) };
                editTextBoxes.Add(key, txt);
                tabControl1.TabPages[1].Controls.Add(lblDesc);
                tabControl1.TabPages[1].Controls.Add(txt);
                yPos += 35;
            }
        }
    }
}
