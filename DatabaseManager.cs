using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace SlipManagement2
{
    public static class DatabaseManager
    {
        private static string dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WeighbridgeData.db");
        private static string connectionString = $"Data Source={dbPath};Version=3;";

        // 1. INITIALIZE DATABASE: Builds the updated 10-field table architecture and settings maps
        public static void InitializeDatabase()
        {
            try
            {
                using (SQLiteConnection conn = new SQLiteConnection(connectionString))
                {
                    conn.Open();

                    // 1. Maintain your base tables checks
                    string createSlipsTable = @"
                CREATE TABLE IF NOT EXISTS slips (
                    BilNumber TEXT PRIMARY KEY,
                    SlipID TEXT,
                    Field1 TEXT, Field2 TEXT, Field3 TEXT, Field4 TEXT, Field5 TEXT,
                    Field6 TEXT, Field7 TEXT
                );";

                    string createConfigTable = @"
                CREATE TABLE IF NOT EXISTS field_config (
                    FieldKey TEXT PRIMARY KEY,
                    CustomName TEXT DEFAULT '',
                    PositionOrder INTEGER DEFAULT 1,
                    IsHidden INTEGER DEFAULT 0
                );";

                    string createSettingsTable = @"
                CREATE TABLE IF NOT EXISTS global_settings (
                    SettingKey TEXT PRIMARY KEY,
                    SettingValue TEXT DEFAULT ''
                );";

                    using (SQLiteCommand cmd = new SQLiteCommand(createSlipsTable, conn)) { cmd.ExecuteNonQuery(); }
                    using (SQLiteCommand cmd = new SQLiteCommand(createConfigTable, conn)) { cmd.ExecuteNonQuery(); }
                    using (SQLiteCommand cmd = new SQLiteCommand(createSettingsTable, conn)) { cmd.ExecuteNonQuery(); }

                    // 2. ⭐ THE INSTANT FIX: Scan the schema to inject columns Field8, Field9, and Field10 if missing
                    for (int i = 8; i <= 10; i++)
                    {
                        bool columnExists = false;
                        string columnName = "Field" + i;
                        string checkColumnQuery = "PRAGMA table_info(slips);";

                        using (SQLiteCommand checkCmd = new SQLiteCommand(checkColumnQuery, conn))
                        using (SQLiteDataReader reader = checkCmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                if (reader["name"].ToString().Equals(columnName, StringComparison.OrdinalIgnoreCase))
                                {
                                    columnExists = true;
                                    break;
                                }
                            }
                        }

                        // If your database file is missing Field8, Field9, or Field10, safely append them!
                        if (!columnExists)
                        {
                            string alterQuery = $"ALTER TABLE slips ADD COLUMN {columnName} TEXT DEFAULT '';";
                            using (SQLiteCommand alterCmd = new SQLiteCommand(alterQuery, conn))
                            {
                                alterCmd.ExecuteNonQuery();
                            }
                        }
                    }

                    // 3. Pre-populate the configuration template fields dictionary maps if fresh
                    string checkFresh = "SELECT COUNT(*) FROM field_config;";
                    using (SQLiteCommand checkCmd = new SQLiteCommand(checkFresh, conn))
                    {
                        long count = (long)checkCmd.ExecuteScalar();
                        if (count == 0)
                        {
                            for (int i = 1; i <= 10; i++)
                            {
                                string populateQuery = $"INSERT INTO field_config (FieldKey, CustomName, PositionOrder, IsHidden) VALUES ('Field{i}', 'Field {i}', {i}, 0);";
                                using (SQLiteCommand popCmd = new SQLiteCommand(populateQuery, conn))
                                {
                                    popCmd.ExecuteNonQuery();
                                }
                            }
                        }
                    }

                    // 4. Pre-populate global configuration template defaults if fresh
                    string checkSettings = "SELECT COUNT(*) FROM global_settings;";
                    using (SQLiteCommand checkSetCmd = new SQLiteCommand(checkSettings, conn))
                    {
                        long count = (long)checkSetCmd.ExecuteScalar();
                        if (count == 0)
                        {
                            new SQLiteCommand("INSERT INTO global_settings (SettingKey, SettingValue) VALUES ('HeaderTitle', 'UITVAL GRONDE PTY (LTD)');", conn).ExecuteNonQuery();
                            new SQLiteCommand("INSERT INTO global_settings (SettingKey, SettingValue) VALUES ('LogoPath', '');", conn).ExecuteNonQuery();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Database Schema Upgrade Error: " + ex.Message);
            }
        }


        // 2. SAVE SLIP: Inserts or replaces a transaction row inside your table database tracking maps
        public static bool SaveSlip(string bilNum, string slipId, string f1, string f2, string f3, string f4, string f5, string f6, string f7, string f8 = "", string f9 = "", string f10 = "")
        {
            try
            {
                using (SQLiteConnection conn = new SQLiteConnection(connectionString))
                {
                    conn.Open();
                    string insertQuery = @"
                        INSERT OR REPLACE INTO slips (BilNumber, SlipID, Field1, Field2, Field3, Field4, Field5, Field6, Field7, Field8, Field9, Field10) 
                        VALUES (@BilNumber, @SlipID, @Field1, @Field2, @Field3, @Field4, @Field5, @Field6, @Field7, @Field8, @Field9, @Field10);";

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
                        cmd.Parameters.AddWithValue("@Field8", f8);
                        cmd.Parameters.AddWithValue("@Field9", f9);
                        cmd.Parameters.AddWithValue("@Field10", f10);

                        cmd.ExecuteNonQuery();
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error saving slip transaction rows: " + ex.Message);
                return false;
            }
        }
        // 3. LOAD SAVED SLIPS: Pulls unprinted slips (IsPrinted = 0) and generates dashboard tile buttons
        public static void LoadSavedSlipsToDashboard(FlowLayoutPanel targetPanel)
        {
            targetPanel.Controls.Clear();
            try
            {
                using (SQLiteConnection conn = new SQLiteConnection(connectionString))
                {
                    conn.Open();
                    string selectQuery = "SELECT * FROM slips WHERE IsPrinted = 0 ORDER BY CreatedAt ASC;";

                    using (SQLiteCommand cmd = new SQLiteCommand(selectQuery, conn))
                    using (SQLiteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string bilNumber = reader["BilNumber"].ToString();
                            string slipId = reader["SlipID"].ToString();
                            string f1 = reader["Field1"].ToString();
                            string f2 = reader["Field2"].ToString();
                            string f3 = reader["Field3"].ToString();
                            string f4 = reader["Field4"].ToString();
                            string f5 = reader["Field5"].ToString();
                            string f6 = reader["Field6"].ToString();
                            string f7 = reader["Field7"].ToString();

                            string formattedTileText = $"🚚 Reg: {(string.IsNullOrWhiteSpace(f1) ? "No Reg" : f1)}\n🆔 Slip: {slipId}\n⚖️ Tons: {(string.IsNullOrWhiteSpace(f7) ? "0" : f7)}";

                            Button slipCard = new Button();
                            slipCard.Size = new Size(200, 110);
                            slipCard.BackColor = Color.LightYellow;
                            slipCard.FlatStyle = FlatStyle.Flat;
                            slipCard.Font = new Font("Arial", 10, FontStyle.Bold);
                            slipCard.Text = formattedTileText;

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

                                editForm.Tag = slipCard;
                                editForm.ShowDialog();
                            };

                            slipCard.Tag = new Dictionary<string, string>
                            {
                                { "SlipID", slipId }, { "BilNumber", bilNumber },
                                { "Field1", f1 }, { "Field2", f2 }, { "Field3", f3 },
                                { "Field4", f4 }, { "Field5", f5 }, { "Field6", f6 }, { "Field7", f7 }
                            };

                            targetPanel.Controls.Add(slipCard);

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
                MessageBox.Show("Error rendering dashboard view grids: " + ex.Message);
            }
        }

        // 4. MARK AS PRINTED: Updates status flag to 1 so the record shifts into history archives
        public static void MarkSlipAsPrinted(string bilNumber)
        {
            try
            {
                using (SQLiteConnection conn = new SQLiteConnection(connectionString))
                {
                    conn.Open();
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
                MessageBox.Show("Error executing record archive flip update: " + ex.Message);
            }
        }

        // 5. GET NEXT SEQUENTIAL ID: Scans table rows to continue numbering without duplicates
        public static int GetNextSequentialSlipId()
        {
            int highestId = 0;
            try
            {
                using (SQLiteConnection conn = new SQLiteConnection(connectionString))
                {
                    conn.Open();
                    string query = "SELECT MAX(CAST(SlipID AS INTEGER)) FROM slips;";
                    using (SQLiteCommand cmd = new SQLiteCommand(query, conn))
                    {
                        object result = cmd.ExecuteScalar();
                        if (result != null && result != DBNull.Value)
                        {
                            highestId = Convert.ToInt32(result);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error tracing serial index properties: " + ex.Message);
            }
            return highestId + 1;
        }

        // 6. GLOBAL CONFIG SETTINGS LOGIC HELPERS: Easy loading for company info or logos
        public static void SaveGlobalSetting(string key, string value)
        {
            using (SQLiteConnection conn = new SQLiteConnection(connectionString))
            {
                conn.Open();
                string query = "INSERT OR REPLACE INTO global_settings (SettingKey, SettingValue) VALUES (@Key, @Value);";
                using (SQLiteCommand cmd = new SQLiteCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Key", key);
                    cmd.Parameters.AddWithValue("@Value", value);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public static string GetGlobalSetting(string key, string defaultValue)
        {
            using (SQLiteConnection conn = new SQLiteConnection(connectionString))
            {
                conn.Open();
                string query = "SELECT SettingValue FROM global_settings WHERE SettingKey = @Key;";
                using (SQLiteCommand cmd = new SQLiteCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Key", key);
                    object result = cmd.ExecuteScalar();
                    return result != null ? result.ToString() : defaultValue;
                }
            }
        } // 🟢 This closes GetGlobalSetting cleanly

        // ===================================================================
        // HELPER MODULES & CONFIGURATION DICTIONARIES
        // ===================================================================

        // Clean container class to store field state attributes
        public class FieldLayoutSettings
        {
            public string CustomName { get; set; }
            public int PositionOrder { get; set; }
            public bool IsHidden { get; set; }
        }

        // Pulls dynamic label definitions directly from your SQLite configurations
        public static Dictionary<string, FieldLayoutSettings> GetActiveFieldConfigurations()
        {
            var configMap = new Dictionary<string, FieldLayoutSettings>();
            string dbPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WeighbridgeData.db");
            string connectionString = $"Data Source={dbPath};Version=3;";

            try
            {
                using (SQLiteConnection conn = new SQLiteConnection(connectionString))
                {
                    conn.Open();
                    string query = "SELECT FieldKey, CustomName, PositionOrder, IsHidden FROM field_config;";
                    using (SQLiteCommand cmd = new SQLiteCommand(query, conn))
                    using (SQLiteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            configMap.Add(reader["FieldKey"].ToString(), new FieldLayoutSettings
                            {
                                CustomName = reader["CustomName"].ToString(),
                                PositionOrder = Convert.ToInt32(reader["PositionOrder"]),
                                IsHidden = Convert.ToInt32(reader["IsHidden"]) == 1
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error mapping system layout properties: " + ex.Message);
            }
            return configMap;
        }
    } // This closes the DatabaseManager class
} // This closes the SlipManagement2 namespace
