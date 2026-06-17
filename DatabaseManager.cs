using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace SlipManagement2 // ⚠️ Verify this matches your project namespace!
{
    public static class DatabaseManager
    {
        // The database file will sit right in the same folder as your app exe
        private static string dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WeighbridgeData.db");
        private static string connectionString = $"Data Source={dbPath};Version=3;";

        public static void InitializeDatabase()
        {
            try
            {
                // Create the table structure if the database file doesn't exist yet
                using (SQLiteConnection conn = new SQLiteConnection(connectionString))
                {
                    conn.Open();

                    string createTableQuery = @"
                        CREATE TABLE IF NOT EXISTS slips (
                            BilNumber TEXT PRIMARY KEY,
                            SlipID TEXT,
                            Field1 TEXT,
                            Field2 TEXT,
                            Field3 TEXT,
                            Field4 TEXT,
                            Field5 TEXT,
                            Field6 TEXT,
                            Field7 TEXT,
                            IsPrinted INTEGER DEFAULT 0, -- 0 means No/Pending, 1 means Yes/Printed
                            CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
                        );";

                    using (SQLiteCommand cmd = new SQLiteCommand(createTableQuery, conn))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Database Initialization Error: " + ex.Message);
            }
        }

        // Master function to save a brand new slip down into the SQLite file
        public static bool SaveSlip(string bilNum, string slipId, string f1, string f2, string f3, string f4, string f5, string f6, string f7)
        {
            try
            {
                using (SQLiteConnection conn = new SQLiteConnection(connectionString))
                {
                    conn.Open();
                    string insertQuery = @"
                        INSERT OR REPLACE INTO slips (BilNumber, SlipID, Field1, Field2, Field3, Field4, Field5, Field6, Field7) 
                        VALUES (@BilNumber, @SlipID, @Field1, @Field2, @Field3, @Field4, @Field5, @Field6, @Field7);";

                    using (SQLiteCommand cmd = new SQLiteCommand(insertQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@BilNumber", bilNum);
                        cmd.Parameters.AddWithValue("@SlipID", slipId);
                        cmd.Parameters.AddWithValue("@Field1", f1);
                        cmd.Parameters.AddWithValue("@Field2", f2);
                        cmd.Parameters.AddWithValue("@Field3", f3);
                        cmd.Parameters.AddWithValue("@Field4", f4);
                        cmd.Parameters.AddWithValue("@Field5", f5);
                        cmd.Parameters.AddWithValue("@Field6", f6);
                        cmd.Parameters.AddWithValue("@Field7", f7);

                        cmd.ExecuteNonQuery();
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error saving data to SQLite: " + ex.Message);
                return false;
            }
        }
        public static void MarkSlipAsPrinted(string bilNumber)
        {
            try
            {
                string dbPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WeighbridgeData.db");
                string connectionString = $"Data Source={dbPath};Version=3;";

                using (SQLiteConnection conn = new SQLiteConnection(connectionString))
                {
                    conn.Open();
                    // This flips the status flag to 1 instead of deleting the data record row!
                    string updateQuery = "UPDATE slips SET IsPrinted = 1 WHERE BilNumber = @BilNumber;";
                    using (SQLiteCommand cmd = new SQLiteCommand(updateQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@BilNumber", bilNumber);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error updating slip status: " + ex.Message);
            }
        }
        public static void LoadSavedSlipsToDashboard(FlowLayoutPanel targetPanel)
        {
            // Clear out any temporary tiles from layout memory before loading database rows
            targetPanel.Controls.Clear();

            // Reconstruct connection properties safely matching your app assembly environment
            string dbPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WeighbridgeData.db");
            string connectionString = $"Data Source={dbPath};Version=3;";

            try
            {
                using (SQLiteConnection conn = new SQLiteConnection(connectionString))
                {
                    conn.Open();

                    // ⭐ CRITICAL INTERACTIVE RULE: Only extract rows where IsPrinted is equal to 0!
                    string selectQuery = "SELECT * FROM slips WHERE IsPrinted = 0 ORDER BY CreatedAt ASC;";

                    using (SQLiteCommand cmd = new SQLiteCommand(selectQuery, conn))
                    using (SQLiteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            // 1. Gather all string values out of each data row column cell
                            string bilNumber = reader["BilNumber"].ToString();
                            string slipId = reader["SlipID"].ToString();
                            string f1 = reader["Field1"].ToString();
                            string f2 = reader["Field2"].ToString();
                            string f3 = reader["Field3"].ToString();
                            string f4 = reader["Field4"].ToString();
                            string f5 = reader["Field5"].ToString();
                            string f6 = reader["Field6"].ToString();
                            string f7 = reader["Field7"].ToString();

                            // 2. Format the layout text to display tracking info on the tile face
                            string formattedTileText = $"🚚 Reg: {(string.IsNullOrWhiteSpace(f1) ? "No Reg" : f1)}\n🆔 Slip: {slipId}\n⚖️ Tons: {(string.IsNullOrWhiteSpace(f7) ? "0" : f7)}";

                            // 3. Rebuild the visual clickable dashboard tracking tile button
                            Button slipCard = new Button();
                            slipCard.Size = new Size(200, 110);
                            slipCard.BackColor = Color.LightYellow;
                            slipCard.FlatStyle = FlatStyle.Flat;
                            slipCard.Font = new Font("Arial", 10, FontStyle.Bold);
                            slipCard.Text = formattedTileText;

                            // 4. Wire the click event to open the edit pop-up with all data populated
                            slipCard.Click += (sender, e) =>
                            {
                                CreateSlip editForm = new CreateSlip();
                                editForm.txtSlipID.Text = slipId;
                                editForm.txtBilNumber.Text = bilNumber;
                                editForm.txtField1.Text = f1;
                                editForm.txtField2.Text = f2;
                                editForm.txtField3.Text = f3;
                                editForm.txtField4.Text = f4;
                                editForm.txtField5.Text = f5;
                                editForm.txtField6.Text = f6;
                                editForm.txtField7.Text = f7;

                                editForm.Tag = slipCard; // Lock the card reference onto the form Tag
                                editForm.ShowDialog();
                            };

                            // 5. Store the variables dictionary inside the tile's tag property data container
                            slipCard.Tag = new Dictionary<string, string>
                    {
                        { "SlipID", slipId }, { "BilNumber", bilNumber },
                        { "Field1", f1 }, { "Field2", f2 }, { "Field3", f3 },
                        { "Field4", f4 }, { "Field5", f5 }, { "Field6", f6 }, { "Field7", f7 }
                    };

                            // 6. Push the tile directly onto the open flow layout panel workspace shelf
                            targetPanel.Controls.Add(slipCard);

                            // 7. Update the automated sequential ID tracker to always match the next higher integer 
                            if (int.TryParse(slipId, out int parsedId))
                            {
                                if (parsedId >= Main.NextSlipId)
                                {
                                    Main.NextSlipId = parsedId + 1;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading slips to dashboard shelf: " + ex.Message);
            }
        }


    }
}
