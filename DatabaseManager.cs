using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace SlipManagement2
{
    public static class DatabaseManager
    {
        private static readonly string DbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WeighbridgeData.db");
        private static string ConnStr => $"Data Source={DbPath};Version=3;";

        public static void InitializeDatabase()
        {
            try
            {
                using (var conn = new SQLiteConnection(ConnStr))
                {
                    conn.Open();

                    // Auto-discard the old incompatible schema (test data only, safe to drop per spec)
                    DropLegacySchemaIfPresent(conn);

                    ExecNQ(conn, @"
                        CREATE TABLE IF NOT EXISTS Slips (
                            SlipID      INTEGER PRIMARY KEY AUTOINCREMENT,
                            BillNumber  TEXT    NOT NULL UNIQUE,
                            Status      TEXT    NOT NULL DEFAULT 'Unprinted',
                            Field1  TEXT, Field2  TEXT, Field3  TEXT, Field4  TEXT,  Field5  TEXT,
                            Field6  TEXT, Field7  TEXT, Field8  TEXT, Field9  TEXT,  Field10 TEXT,
                            VoidReason  TEXT,
                            CreatedAt   TEXT    NOT NULL DEFAULT (datetime('now','localtime')),
                            PrintedAt   TEXT,
                            VoidedAt    TEXT
                        );");

                    ExecNQ(conn, @"
                        CREATE TRIGGER IF NOT EXISTS PreventBillNumberChange
                        BEFORE UPDATE OF BillNumber ON Slips
                        WHEN OLD.BillNumber <> NEW.BillNumber
                        BEGIN
                            SELECT RAISE(ABORT, 'BillNumber cannot be changed once set.');
                        END;");

                    ExecNQ(conn, @"
                        CREATE TABLE IF NOT EXISTS FieldConfig (
                            FieldSlot   TEXT    PRIMARY KEY,
                            LabelName   TEXT    NOT NULL,
                            OrderLine   INTEGER NOT NULL,
                            Hidden      INTEGER NOT NULL DEFAULT 0,
                            LookupTable TEXT    NOT NULL DEFAULT '',
                            IsRequired  INTEGER NOT NULL DEFAULT 0
                        );");

                    ExecNQ(conn, @"
                        CREATE TABLE IF NOT EXISTS GlobalSettings (
                            SettingKey   TEXT PRIMARY KEY,
                            SettingValue TEXT DEFAULT ''
                        );");

                    ExecNQ(conn, @"
                        CREATE TABLE IF NOT EXISTS PrinterProfiles (
                            ProfileID      INTEGER PRIMARY KEY AUTOINCREMENT,
                            ProfileName    TEXT    NOT NULL DEFAULT '' UNIQUE,
                            PrinterName    TEXT    NOT NULL,
                            Mode           TEXT    NOT NULL,
                            WidthMM        REAL    NOT NULL,
                            HeightMM       REAL,
                            MarginTopMM    REAL    NOT NULL DEFAULT 0,
                            MarginLeftMM   REAL    NOT NULL DEFAULT 0,
                            MarginRightMM  REAL    NOT NULL DEFAULT 0,
                            MarginBottomMM REAL    NOT NULL DEFAULT 0,
                            NumCopies      INTEGER NOT NULL DEFAULT 1,
                            Orientation    TEXT    NOT NULL DEFAULT 'Portrait',
                            SlipLengthIn   REAL    NOT NULL DEFAULT 5.5,
                            IsActive       INTEGER NOT NULL DEFAULT 0
                        );");

                    ExecNQ(conn, "CREATE TABLE IF NOT EXISTS TruckRegs     (ID INTEGER PRIMARY KEY AUTOINCREMENT, Value TEXT NOT NULL UNIQUE);");
                    ExecNQ(conn, "CREATE TABLE IF NOT EXISTS StockpileRefs (ID INTEGER PRIMARY KEY AUTOINCREMENT, Value TEXT NOT NULL UNIQUE);");
                    ExecNQ(conn, "CREATE TABLE IF NOT EXISTS ROMTypes      (ID INTEGER PRIMARY KEY AUTOINCREMENT, Value TEXT NOT NULL UNIQUE);");
                    ExecNQ(conn, "CREATE TABLE IF NOT EXISTS BlockNrs      (ID INTEGER PRIMARY KEY AUTOINCREMENT, Value TEXT NOT NULL UNIQUE);");
                    ExecNQ(conn, "CREATE TABLE IF NOT EXISTS Sizes         (ID INTEGER PRIMARY KEY AUTOINCREMENT, Value TEXT NOT NULL UNIQUE);");
                    ExecNQ(conn, "CREATE TABLE IF NOT EXISTS Clients       (ID INTEGER PRIMARY KEY AUTOINCREMENT, Value TEXT NOT NULL UNIQUE);");
                    ExecNQ(conn, "CREATE TABLE IF NOT EXISTS Destinations  (ID INTEGER PRIMARY KEY AUTOINCREMENT, Value TEXT NOT NULL UNIQUE);");
                    ExecNQ(conn, "CREATE TABLE IF NOT EXISTS OrderNumbers  (ID INTEGER PRIMARY KEY AUTOINCREMENT, Value TEXT NOT NULL UNIQUE);");
                    ExecNQ(conn, "CREATE TABLE IF NOT EXISTS Slots         (ID INTEGER PRIMARY KEY AUTOINCREMENT, Value TEXT NOT NULL UNIQUE);");

                    MigrateSchemaIfNeeded(conn);
                    SeedFieldConfigIfEmpty(conn);
                    SeedGlobalSettingsIfEmpty(conn);
                    SeedPrinterProfileIfEmpty(conn);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Database initialization failed: " + ex.Message);
            }
        }

        // Detects the old incompatible schema (no Status column) and drops it.
        // The old data is test-only; dropping it is explicitly approved in the spec.
        private static void DropLegacySchemaIfPresent(SQLiteConnection conn)
        {
            bool tableExists = false;
            using (var cmd = new SQLiteCommand("SELECT name FROM sqlite_master WHERE type='table' AND lower(name)='slips';", conn))
            using (var r = cmd.ExecuteReader())
                tableExists = r.Read();

            if (!tableExists) return;

            bool hasStatusCol = false;
            using (var cmd = new SQLiteCommand("PRAGMA table_info(slips);", conn))
            using (var r = cmd.ExecuteReader())
            {
                while (r.Read())
                    if (r["name"].ToString().Equals("Status", StringComparison.OrdinalIgnoreCase))
                        hasStatusCol = true;
            }

            if (!hasStatusCol)
            {
                ExecNQ(conn, "DROP TABLE IF EXISTS slips;");
                ExecNQ(conn, "DROP TABLE IF EXISTS field_config;");
                ExecNQ(conn, "DROP TABLE IF EXISTS global_settings;");
                ExecNQ(conn, "DROP TRIGGER IF EXISTS PreventBillNumberChange;");
            }
        }

        // Adds columns introduced after the initial schema without wiping any data.
        private static void MigrateSchemaIfNeeded(SQLiteConnection conn)
        {
            // Slips.VoidedAt — records when a slip was voided, so the Voided history view can be
            // filtered by the date the void actually happened rather than by the creation date.
            bool hasVoidedAt = false;
            using (var cmd = new SQLiteCommand("PRAGMA table_info(Slips);", conn))
            using (var r = cmd.ExecuteReader())
                while (r.Read())
                    if (r["name"].ToString().Equals("VoidedAt", StringComparison.OrdinalIgnoreCase))
                        hasVoidedAt = true;

            if (!hasVoidedAt)
                ExecNQ(conn, "ALTER TABLE Slips ADD COLUMN VoidedAt TEXT;");

            // Slips voided before this column existed have no recorded void time. Fall back to
            // CreatedAt so they still appear in a date-filtered Voided view rather than vanishing.
            ExecNQ(conn, "UPDATE Slips SET VoidedAt = CreatedAt WHERE Status = 'Voided' AND VoidedAt IS NULL;");

            bool hasLookupTable = false;
            bool hasIsRequired  = false;
            using (var cmd = new SQLiteCommand("PRAGMA table_info(FieldConfig);", conn))
            using (var r = cmd.ExecuteReader())
                while (r.Read())
                {
                    string col = r["name"].ToString();
                    if (col.Equals("LookupTable", StringComparison.OrdinalIgnoreCase)) hasLookupTable = true;
                    if (col.Equals("IsRequired",  StringComparison.OrdinalIgnoreCase)) hasIsRequired  = true;
                }

            if (!hasLookupTable)
            {
                ExecNQ(conn, "ALTER TABLE FieldConfig ADD COLUMN LookupTable TEXT NOT NULL DEFAULT '';");
                var lookups = new[] {
                    ("Field1",  "TruckRegs"),
                    ("Field2",  "StockpileRefs"),
                    ("Field3",  "ROMTypes"),
                    ("Field4",  "BlockNrs"),
                    ("Field5",  "Sizes"),
                    ("Field6",  "Destinations"),
                    ("Field10", "Clients"),
                };
                foreach (var (slot, table) in lookups)
                    using (var cmd = new SQLiteCommand("UPDATE FieldConfig SET LookupTable=@T WHERE FieldSlot=@S;", conn))
                    {
                        cmd.Parameters.AddWithValue("@T", table);
                        cmd.Parameters.AddWithValue("@S", slot);
                        cmd.ExecuteNonQuery();
                    }
            }

            if (!hasIsRequired)
                ExecNQ(conn, "ALTER TABLE FieldConfig ADD COLUMN IsRequired INTEGER NOT NULL DEFAULT 0;");

            // Backfill Field8/9 lookup tables for databases created before this was added
            ExecNQ(conn, "UPDATE FieldConfig SET LookupTable='OrderNumbers' WHERE FieldSlot='Field8' AND (LookupTable IS NULL OR LookupTable='');");
            ExecNQ(conn, "UPDATE FieldConfig SET LookupTable='Slots'        WHERE FieldSlot='Field9' AND (LookupTable IS NULL OR LookupTable='');");

            // Field1 and Field7 must always be IsRequired=1 — enforce on every startup
            // so existing databases are corrected regardless of when they were created.
            ExecNQ(conn, "UPDATE FieldConfig SET IsRequired=1 WHERE FieldSlot='Field1' OR FieldSlot='Field7';");

            // PrinterProfiles migrations — add ProfileName, Orientation, SlipLengthIn, and calibration columns
            bool hasProfileName   = false;
            bool hasOrientation   = false;
            bool hasSlipLengthIn  = false;
            bool hasSlipFontScale = false;
            bool hasSlipOffsetXMm = false;
            bool hasSlipOffsetYMm = false;
            bool hasCopiesPerPage = false;
            using (var cmd = new SQLiteCommand("PRAGMA table_info(PrinterProfiles);", conn))
            using (var r = cmd.ExecuteReader())
                while (r.Read())
                {
                    string col = r["name"].ToString();
                    if (col.Equals("ProfileName",   StringComparison.OrdinalIgnoreCase)) hasProfileName   = true;
                    if (col.Equals("Orientation",   StringComparison.OrdinalIgnoreCase)) hasOrientation   = true;
                    if (col.Equals("SlipLengthIn",  StringComparison.OrdinalIgnoreCase)) hasSlipLengthIn  = true;
                    if (col.Equals("SlipFontScale", StringComparison.OrdinalIgnoreCase)) hasSlipFontScale = true;
                    if (col.Equals("SlipOffsetXMm", StringComparison.OrdinalIgnoreCase)) hasSlipOffsetXMm = true;
                    if (col.Equals("SlipOffsetYMm", StringComparison.OrdinalIgnoreCase)) hasSlipOffsetYMm = true;
                    if (col.Equals("CopiesPerPage", StringComparison.OrdinalIgnoreCase)) hasCopiesPerPage = true;
                }

            if (!hasProfileName)
            {
                ExecNQ(conn, "ALTER TABLE PrinterProfiles ADD COLUMN ProfileName TEXT NOT NULL DEFAULT '';");
                // Give each existing row a unique name derived from its primary key
                ExecNQ(conn, "UPDATE PrinterProfiles SET ProfileName = 'Profile ' || ProfileID WHERE ProfileName = '' OR ProfileName IS NULL;");
                // Rename the active profile to 'Default' when that name isn't already taken
                ExecNQ(conn, @"UPDATE PrinterProfiles SET ProfileName = 'Default'
                    WHERE IsActive = 1
                    AND NOT EXISTS (SELECT 1 FROM PrinterProfiles WHERE ProfileName = 'Default');");
                // ALTER TABLE ADD COLUMN can't declare UNIQUE in SQLite — add an index instead
                ExecNQ(conn, "CREATE UNIQUE INDEX IF NOT EXISTS idx_PrinterProfiles_ProfileName ON PrinterProfiles(ProfileName);");
            }

            if (!hasOrientation)
                ExecNQ(conn, "ALTER TABLE PrinterProfiles ADD COLUMN Orientation TEXT NOT NULL DEFAULT 'Portrait';");

            if (!hasSlipLengthIn)
            {
                ExecNQ(conn, "ALTER TABLE PrinterProfiles ADD COLUMN SlipLengthIn REAL NOT NULL DEFAULT 5.5;");
                // Backfill from stored HeightMM for custom-length profiles
                ExecNQ(conn, "UPDATE PrinterProfiles SET SlipLengthIn = ROUND(HeightMM / 25.4, 2) WHERE Mode = 'Small240x102' AND HeightMM > 0;");
            }

            // Per-preset calibration columns (previously stored as shared GlobalSettings)
            if (!hasSlipFontScale)
                ExecNQ(conn, "ALTER TABLE PrinterProfiles ADD COLUMN SlipFontScale REAL NOT NULL DEFAULT 1.0;");
            if (!hasSlipOffsetXMm)
                ExecNQ(conn, "ALTER TABLE PrinterProfiles ADD COLUMN SlipOffsetXMm REAL NOT NULL DEFAULT 0.0;");
            if (!hasSlipOffsetYMm)
                ExecNQ(conn, "ALTER TABLE PrinterProfiles ADD COLUMN SlipOffsetYMm REAL NOT NULL DEFAULT 0.0;");
            if (!hasCopiesPerPage)
                ExecNQ(conn, "ALTER TABLE PrinterProfiles ADD COLUMN CopiesPerPage INTEGER NOT NULL DEFAULT 1;");

            // 3-copies layout has been removed (asymmetric 2×2 with blank cell). Migrate to 2.
            ExecNQ(conn, "UPDATE PrinterProfiles SET CopiesPerPage = 2 WHERE CopiesPerPage = 3;");
        }

        private static void SeedFieldConfigIfEmpty(SQLiteConnection conn)
        {
            using (var cmd = new SQLiteCommand("SELECT COUNT(*) FROM FieldConfig;", conn))
            {
                if ((long)cmd.ExecuteScalar() > 0) return;
            }

            var defaults = new[]
            {
                ("Field1",  "Truck Reg",      1,  "TruckRegs"),
                ("Field2",  "Stockpile Name", 2,  "StockpileRefs"),
                ("Field3",  "Rom Type",       3,  "ROMTypes"),
                ("Field4",  "Block Nr",       4,  "BlockNrs"),
                ("Field5",  "Size",           5,  "Sizes"),
                ("Field6",  "Destination",    6,  "Destinations"),
                ("Field7",  "Tons",           7,  ""),
                ("Field8",  "Order Number",   8,  "OrderNumbers"),
                ("Field9",  "Slot",           9,  "Slots"),
                ("Field10", "Client",         10, "Clients"),
            };

            foreach (var (slot, label, order, lookup) in defaults)
            {
                // Field1 and Field7 are always required — spec §4.2/4.4
                int isRequired = (slot == "Field1" || slot == "Field7") ? 1 : 0;
                using (var cmd = new SQLiteCommand(
                    "INSERT INTO FieldConfig (FieldSlot, LabelName, OrderLine, Hidden, LookupTable, IsRequired) VALUES (@S, @L, @O, 0, @T, @R);", conn))
                {
                    cmd.Parameters.AddWithValue("@S", slot);
                    cmd.Parameters.AddWithValue("@L", label);
                    cmd.Parameters.AddWithValue("@O", order);
                    cmd.Parameters.AddWithValue("@T", lookup);
                    cmd.Parameters.AddWithValue("@R", isRequired);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private static void SeedGlobalSettingsIfEmpty(SQLiteConnection conn)
        {
            using (var cmd = new SQLiteCommand("SELECT COUNT(*) FROM GlobalSettings;", conn))
            {
                if ((long)cmd.ExecuteScalar() > 0) return;
            }

            var defaults = new[]
            {
                ("HeaderTitle",       "UITVAL GRONDE PTY (LTD)"),
                ("LogoPath",          ""),
                ("SelectedPrinter",   "EPSON LX-350"),
                ("PaperSizeProfile",  "Small240x102"),
                ("PrintOrientation",  "Portrait"),
                ("PrintCopiesCount",  "1"),
                ("SlipCustomLength",  "5.5"),
            };

            foreach (var (key, val) in defaults)
            {
                using (var cmd = new SQLiteCommand("INSERT INTO GlobalSettings (SettingKey, SettingValue) VALUES (@K, @V);", conn))
                {
                    cmd.Parameters.AddWithValue("@K", key);
                    cmd.Parameters.AddWithValue("@V", val);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private static void SeedPrinterProfileIfEmpty(SQLiteConnection conn)
        {
            using (var cmd = new SQLiteCommand("SELECT COUNT(*) FROM PrinterProfiles;", conn))
            {
                if ((long)cmd.ExecuteScalar() > 0) return;
            }
            // 5.5 in × 25.4 = 139.7 mm
            ExecNQ(conn, @"
                INSERT INTO PrinterProfiles
                    (ProfileName, PrinterName, Mode, WidthMM, HeightMM,
                     MarginTopMM, MarginLeftMM, MarginRightMM, MarginBottomMM,
                     NumCopies, Orientation, SlipLengthIn, IsActive,
                     SlipFontScale, SlipOffsetXMm, SlipOffsetYMm, CopiesPerPage)
                VALUES ('Default', 'EPSON LX-350', 'Small240x102', 240, 139.7,
                        10, 10, 10, 10, 1, 'Portrait', 5.5, 1,
                        1.0, 0.0, 0.0, 1);");
        }

        // ===================================================================
        // SLIP CRUD
        // ===================================================================

        // Inserts a new Unprinted slip. Returns the auto-assigned SlipID, or -1 on failure.
        public static int InsertSlip(string billNumber,
            string f1, string f2, string f3, string f4, string f5,
            string f6, string f7, string f8 = "", string f9 = "", string f10 = "")
        {
            try
            {
                using (var conn = new SQLiteConnection(ConnStr))
                {
                    conn.Open();
                    // CreatedAt is set explicitly rather than left to the column default. All
                    // timestamps use the clock of whatever machine the app runs on — SQLite's
                    // datetime('now') is UTC, which would disagree with BillNumber and with the
                    // date printed on the slip, both of which come from C# DateTime.Now. Setting
                    // it here also fixes databases whose table was created before this change,
                    // without rebuilding the table.
                    const string sql = @"
                        INSERT INTO Slips (BillNumber, CreatedAt, Field1, Field2, Field3, Field4, Field5, Field6, Field7, Field8, Field9, Field10)
                        VALUES (@BN, datetime('now','localtime'), @F1, @F2, @F3, @F4, @F5, @F6, @F7, @F8, @F9, @F10);";
                    using (var cmd = new SQLiteCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@BN",  billNumber);
                        cmd.Parameters.AddWithValue("@F1",  f1);
                        cmd.Parameters.AddWithValue("@F2",  f2);
                        cmd.Parameters.AddWithValue("@F3",  f3);
                        cmd.Parameters.AddWithValue("@F4",  f4);
                        cmd.Parameters.AddWithValue("@F5",  f5);
                        cmd.Parameters.AddWithValue("@F6",  f6);
                        cmd.Parameters.AddWithValue("@F7",  f7);
                        cmd.Parameters.AddWithValue("@F8",  f8);
                        cmd.Parameters.AddWithValue("@F9",  f9);
                        cmd.Parameters.AddWithValue("@F10", f10);
                        cmd.ExecuteNonQuery();
                    }
                    using (var idCmd = new SQLiteCommand("SELECT last_insert_rowid();", conn))
                        return Convert.ToInt32(idCmd.ExecuteScalar());
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error saving slip: " + ex.Message);
                return -1;
            }
        }

        // Updates the editable fields on an existing Unprinted slip.
        public static bool UpdateSlipFields(int slipId,
            string f1, string f2, string f3, string f4, string f5,
            string f6, string f7, string f8 = "", string f9 = "", string f10 = "")
        {
            try
            {
                using (var conn = new SQLiteConnection(ConnStr))
                {
                    conn.Open();
                    const string sql = @"
                        UPDATE Slips SET
                            Field1=@F1, Field2=@F2, Field3=@F3, Field4=@F4, Field5=@F5,
                            Field6=@F6, Field7=@F7, Field8=@F8, Field9=@F9, Field10=@F10
                        WHERE SlipID=@ID AND Status='Unprinted';";
                    using (var cmd = new SQLiteCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@ID",  slipId);
                        cmd.Parameters.AddWithValue("@F1",  f1);
                        cmd.Parameters.AddWithValue("@F2",  f2);
                        cmd.Parameters.AddWithValue("@F3",  f3);
                        cmd.Parameters.AddWithValue("@F4",  f4);
                        cmd.Parameters.AddWithValue("@F5",  f5);
                        cmd.Parameters.AddWithValue("@F6",  f6);
                        cmd.Parameters.AddWithValue("@F7",  f7);
                        cmd.Parameters.AddWithValue("@F8",  f8);
                        cmd.Parameters.AddWithValue("@F9",  f9);
                        cmd.Parameters.AddWithValue("@F10", f10);
                        cmd.ExecuteNonQuery();
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error updating slip: " + ex.Message);
                return false;
            }
        }

        // Sets Status='Printed' and records PrintedAt timestamp.
        public static void MarkSlipAsPrinted(int slipId)
        {
            try
            {
                using (var conn = new SQLiteConnection(ConnStr))
                {
                    conn.Open();
                    // COALESCE, not a plain assignment: PrintedAt records the FIRST time this slip
                    // was printed and must never move. A slip can be reprinted any number of times
                    // (spec §5.2) without changing its place in the record. Reprints currently go
                    // through PrintSlipPreview and never reach here, but the guard means no future
                    // code path can overwrite the original print date either.
                    using (var cmd = new SQLiteCommand(
                        "UPDATE Slips SET Status='Printed', PrintedAt=COALESCE(PrintedAt, datetime('now','localtime')) WHERE SlipID=@ID;", conn))
                    {
                        cmd.Parameters.AddWithValue("@ID", slipId);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error marking slip as printed: " + ex.Message);
            }
        }

        // Voids a Printed slip from the history form; returns true if a row was affected.
        public static bool VoidPrintedSlip(int slipId, string reason)
        {
            if (reason != null && reason.Length > 20) reason = reason.Substring(0, 20);
            try
            {
                using (var conn = new SQLiteConnection(ConnStr))
                {
                    conn.Open();
                    using (var cmd = new SQLiteCommand(
                        "UPDATE Slips SET Status='Voided', VoidReason=@R, VoidedAt=datetime('now','localtime') WHERE SlipID=@ID AND Status='Printed';", conn))
                    {
                        cmd.Parameters.AddWithValue("@ID", slipId);
                        cmd.Parameters.AddWithValue("@R",  reason);
                        return cmd.ExecuteNonQuery() > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error voiding slip: " + ex.Message);
                return false;
            }
        }

        // Sets Status='Voided'; only transitions from Unprinted. Reason is mandatory (max 20 chars per spec).
        public static void VoidSlip(int slipId, string reason)
        {
            if (reason != null && reason.Length > 20) reason = reason.Substring(0, 20);
            try
            {
                using (var conn = new SQLiteConnection(ConnStr))
                {
                    conn.Open();
                    using (var cmd = new SQLiteCommand("UPDATE Slips SET Status='Voided', VoidReason=@R, VoidedAt=datetime('now','localtime') WHERE SlipID=@ID AND Status='Unprinted';", conn))
                    {
                        cmd.Parameters.AddWithValue("@ID", slipId);
                        cmd.Parameters.AddWithValue("@R",  reason);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error voiding slip: " + ex.Message);
            }
        }

        // ===================================================================
        // SLIP QUERIES — used by SlipsHistoryForm
        // ===================================================================

        // The date column a history search should filter on, chosen by status.
        //
        // A slip's dates diverge: one created on the 1st can be printed on the 2nd. Filtering
        // everything by CreatedAt meant a "printed this week" search silently missed slips
        // created earlier, so each status is filtered by the date its own event happened.
        // COALESCE guards rows written before the relevant column existed.
        private static string DateColumnFor(string status)
        {
            switch (status)
            {
                case "Printed": return "COALESCE(PrintedAt, CreatedAt)";
                case "Voided":  return "COALESCE(VoidedAt,  CreatedAt)";
                default:        return "CreatedAt";
            }
        }

        // Returns slips matching status + date range + optional per-field equality filters.
        // fieldFilters keys must be "Field1"–"Field10" (controlled by the caller, never user input).
        public static DataTable QuerySlips(string status, DateTime from, DateTime to,
            Dictionary<string, string> fieldFilters = null)
        {
            var dt = new DataTable();
            try
            {
                string fromStr = from.ToString("yyyy-MM-dd") + " 00:00:00";
                string toStr   = to.ToString("yyyy-MM-dd")   + " 23:59:59";
                string dateCol = DateColumnFor(status);

                string sql = $"SELECT * FROM Slips WHERE Status=@Status AND {dateCol} BETWEEN @From AND @To";
                var paramList = new List<SQLiteParameter>
                {
                    new SQLiteParameter("@Status", status),
                    new SQLiteParameter("@From",   fromStr),
                    new SQLiteParameter("@To",     toStr),
                };

                if (fieldFilters != null)
                    foreach (var kv in fieldFilters)
                    {
                        sql += $" AND {kv.Key} LIKE @{kv.Key}";
                        paramList.Add(new SQLiteParameter($"@{kv.Key}", "%" + kv.Value + "%"));
                    }

                sql += $" ORDER BY {dateCol} DESC;";

                using (var conn = new SQLiteConnection(ConnStr))
                {
                    conn.Open();
                    using (var cmd = new SQLiteCommand(sql, conn))
                    {
                        cmd.Parameters.AddRange(paramList.ToArray());
                        using (var adapter = new SQLiteDataAdapter(cmd))
                            adapter.Fill(dt);
                    }
                }
            }
            catch (Exception ex) { MessageBox.Show("Error querying slips: " + ex.Message); }
            return dt;
        }

        // Returns the distinct non-empty values for one field column, used to populate filter dropdowns.
        public static List<string> GetDistinctFieldValues(string fieldKey, string status,
            DateTime from, DateTime to)
        {
            var list = new List<string>();
            try
            {
                string fromStr = from.ToString("yyyy-MM-dd") + " 00:00:00";
                string toStr   = to.ToString("yyyy-MM-dd")   + " 23:59:59";
                // Must use the same date column as QuerySlips, or the filter dropdowns would
                // offer values belonging to rows the grid isn't showing.
                string sql     = $"SELECT DISTINCT {fieldKey} FROM Slips WHERE Status=@Status " +
                                 $"AND {DateColumnFor(status)} BETWEEN @From AND @To " +
                                 $"AND {fieldKey} IS NOT NULL AND {fieldKey} != '' ORDER BY {fieldKey};";
                using (var conn = new SQLiteConnection(ConnStr))
                {
                    conn.Open();
                    using (var cmd = new SQLiteCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@Status", status);
                        cmd.Parameters.AddWithValue("@From",   fromStr);
                        cmd.Parameters.AddWithValue("@To",     toStr);
                        using (var r = cmd.ExecuteReader())
                            while (r.Read()) list.Add(r[fieldKey].ToString());
                    }
                }
            }
            catch { }
            return list;
        }

        // Updates all 10 fields on a Printed slip identified by BillNumber.
        public static bool UpdatePrintedSlipFields(string billNumber, string[] fields)
        {
            try
            {
                using (var conn = new SQLiteConnection(ConnStr))
                {
                    conn.Open();
                    const string sql = @"
                        UPDATE Slips SET
                            Field1=@F1, Field2=@F2, Field3=@F3, Field4=@F4, Field5=@F5,
                            Field6=@F6, Field7=@F7, Field8=@F8, Field9=@F9, Field10=@F10
                        WHERE BillNumber=@BillNumber;";
                    using (var cmd = new SQLiteCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@BillNumber", billNumber);
                        for (int i = 1; i <= 10; i++)
                            cmd.Parameters.AddWithValue($"@F{i}", fields[i - 1]);
                        cmd.ExecuteNonQuery();
                        return true;
                    }
                }
            }
            catch (Exception ex) { MessageBox.Show("Error updating slip: " + ex.Message); return false; }
        }

        // ===================================================================
        // DASHBOARD
        // ===================================================================

        // Reads all Unprinted slips from the DB and builds dashboard tile buttons on the panel.
        public static void LoadUnprintedSlipsToDashboard(FlowLayoutPanel panel)
        {
            panel.Controls.Clear();
            try
            {
                using (var conn = new SQLiteConnection(ConnStr))
                {
                    conn.Open();
                    const string sql = "SELECT * FROM Slips WHERE Status='Unprinted' ORDER BY CreatedAt ASC;";
                    using (var cmd = new SQLiteCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            int    slipId  = Convert.ToInt32(reader["SlipID"]);
                            string billNum = reader["BillNumber"].ToString();
                            string f1      = reader["Field1"]?.ToString() ?? "";
                            string f7      = reader["Field7"]?.ToString() ?? "";

                            var data = new Dictionary<string, string>
                            {
                                ["SlipID"]     = slipId.ToString(),
                                ["BillNumber"] = billNum,
                            };
                            for (int i = 1; i <= 10; i++)
                                data["Field" + i] = reader["Field" + i]?.ToString() ?? "";

                            Button tile = BuildTile(slipId, f1, f7, data);
                            panel.Controls.Add(tile);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading dashboard: " + ex.Message);
            }
        }

        private static Button BuildTile(int slipId, string f1, string f7, Dictionary<string, string> data)
        {
            string reg  = string.IsNullOrWhiteSpace(f1) ? "No Reg" : f1;
            string tons = string.IsNullOrWhiteSpace(f7) ? "0" : f7;

            var tile = new Button
            {
                Size      = new Size(200, 110),
                BackColor = Color.LightYellow,
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Arial", 10, FontStyle.Bold),
                Text      = $"Reg: {reg}\nSlip: {slipId}\nTons: {tons}",
                Tag       = data,
            };

            tile.Click += (s, e) =>
            {
                var saved = (Dictionary<string, string>)((Button)s).Tag;
                var editForm = new CreateSlip();
                editForm.ExistingSlipId    = int.Parse(saved["SlipID"]);
                editForm.txtSlipID.Text    = saved["SlipID"];
                editForm.txtBilNumber.Text = saved["BillNumber"];
                for (int i = 1; i <= 10; i++)
                    editForm.SetFieldValue(i, saved.ContainsKey("Field" + i) ? saved["Field" + i] : "");
                editForm.Tag = (Button)s;
                editForm.ShowDialog();
            };

            return tile;
        }

        // ===================================================================
        // PRINTER PROFILES
        // ===================================================================

        public class PrinterProfile
        {
            public double MarginTopMM    { get; set; } = 10;
            public double MarginLeftMM   { get; set; } = 10;
            public double MarginRightMM  { get; set; } = 10;
            public double MarginBottomMM { get; set; } = 10;
            public int    NumCopies      { get; set; } = 1;
            public float  SlipFontScale  { get; set; } = 1.0f;
            public float  SlipOffsetXMm  { get; set; } = 0f;
            public float  SlipOffsetYMm  { get; set; } = 0f;
            public int    CopiesPerPage  { get; set; } = 1;
        }

        // Returns the active PrinterProfile row, or sensible defaults if none exists yet.
        public static PrinterProfile GetActiveProfile()
        {
            try
            {
                using (var conn = new SQLiteConnection(ConnStr))
                {
                    conn.Open();
                    const string sql = @"SELECT MarginTopMM, MarginLeftMM, MarginRightMM, MarginBottomMM,
                                                NumCopies, SlipFontScale, SlipOffsetXMm, SlipOffsetYMm, CopiesPerPage
                                         FROM PrinterProfiles WHERE IsActive=1 LIMIT 1;";
                    using (var cmd = new SQLiteCommand(sql, conn))
                    using (var r = cmd.ExecuteReader())
                    {
                        if (r.Read())
                        {
                            return new PrinterProfile
                            {
                                MarginTopMM    = Convert.ToDouble(r["MarginTopMM"]),
                                MarginLeftMM   = Convert.ToDouble(r["MarginLeftMM"]),
                                MarginRightMM  = Convert.ToDouble(r["MarginRightMM"]),
                                MarginBottomMM = Convert.ToDouble(r["MarginBottomMM"]),
                                NumCopies      = Convert.ToInt32(r["NumCopies"]),
                                SlipFontScale  = Convert.ToSingle(r["SlipFontScale"]),
                                SlipOffsetXMm  = Convert.ToSingle(r["SlipOffsetXMm"]),
                                SlipOffsetYMm  = Convert.ToSingle(r["SlipOffsetYMm"]),
                                CopiesPerPage  = Convert.ToInt32(r["CopiesPerPage"]),
                            };
                        }
                    }
                }
            }
            catch { }
            return new PrinterProfile();
        }

        // ---- Named preset data class ----
        public class PrinterProfileData
        {
            public int    ProfileID        { get; set; }
            public string ProfileName      { get; set; }
            public string PrinterName      { get; set; }
            public string PaperSizeProfile { get; set; }
            public double WidthMM          { get; set; }
            public double HeightMM         { get; set; }
            public double MarginTopMM      { get; set; }
            public double MarginLeftMM     { get; set; }
            public double MarginRightMM    { get; set; }
            public double MarginBottomMM   { get; set; }
            public int    NumCopies        { get; set; }
            public string Orientation      { get; set; }
            public double SlipLengthIn     { get; set; }
            public bool   IsActive         { get; set; }
            public float  SlipFontScale    { get; set; } = 1.0f;
            public float  SlipOffsetXMm    { get; set; } = 0f;
            public float  SlipOffsetYMm    { get; set; } = 0f;
            public int    CopiesPerPage    { get; set; } = 1;
        }

        private static PrinterProfileData ReadProfileRow(System.Data.SQLite.SQLiteDataReader r)
        {
            return new PrinterProfileData
            {
                ProfileID        = Convert.ToInt32(r["ProfileID"]),
                ProfileName      = r["ProfileName"].ToString(),
                PrinterName      = r["PrinterName"].ToString(),
                PaperSizeProfile = r["Mode"].ToString(),
                WidthMM          = Convert.ToDouble(r["WidthMM"]),
                HeightMM         = r["HeightMM"] == DBNull.Value ? 0 : Convert.ToDouble(r["HeightMM"]),
                MarginTopMM      = Convert.ToDouble(r["MarginTopMM"]),
                MarginLeftMM     = Convert.ToDouble(r["MarginLeftMM"]),
                MarginRightMM    = Convert.ToDouble(r["MarginRightMM"]),
                MarginBottomMM   = Convert.ToDouble(r["MarginBottomMM"]),
                NumCopies        = Convert.ToInt32(r["NumCopies"]),
                Orientation      = r["Orientation"].ToString(),
                SlipLengthIn     = Convert.ToDouble(r["SlipLengthIn"]),
                IsActive         = Convert.ToInt32(r["IsActive"]) == 1,
                SlipFontScale    = Convert.ToSingle(r["SlipFontScale"]),
                SlipOffsetXMm    = Convert.ToSingle(r["SlipOffsetXMm"]),
                SlipOffsetYMm    = Convert.ToSingle(r["SlipOffsetYMm"]),
                CopiesPerPage    = Convert.ToInt32(r["CopiesPerPage"]),
            };
        }

        // Returns all saved presets ordered by name.
        public static List<PrinterProfileData> GetAllPrinterProfiles()
        {
            var list = new List<PrinterProfileData>();
            try
            {
                using (var conn = new SQLiteConnection(ConnStr))
                {
                    conn.Open();
                    using (var cmd = new SQLiteCommand("SELECT * FROM PrinterProfiles ORDER BY ProfileName;", conn))
                    using (var r = cmd.ExecuteReader())
                        while (r.Read()) list.Add(ReadProfileRow(r));
                }
            }
            catch (Exception ex) { MessageBox.Show("Error loading printer profiles: " + ex.Message); }
            return list;
        }

        // Returns one named preset, or null if not found.
        public static PrinterProfileData GetPrinterProfileByName(string name)
        {
            try
            {
                using (var conn = new SQLiteConnection(ConnStr))
                {
                    conn.Open();
                    using (var cmd = new SQLiteCommand("SELECT * FROM PrinterProfiles WHERE ProfileName=@N LIMIT 1;", conn))
                    {
                        cmd.Parameters.AddWithValue("@N", name);
                        using (var r = cmd.ExecuteReader())
                            if (r.Read()) return ReadProfileRow(r);
                    }
                }
            }
            catch { }
            return null;
        }

        // Upserts a preset by name, marks it active, and syncs GlobalSettings so legacy code stays consistent.
        // Calibration columns (SlipFontScale, SlipOffsetXMm, SlipOffsetYMm, CopiesPerPage) are intentionally
        // excluded from the ON CONFLICT UPDATE so they are preserved when editing an existing preset from
        // PrinterSettingsForm.  Use SaveCalibrationToProfile to write calibration values.
        public static void SaveOrUpdatePrinterProfile(
            string profileName, string printerName, string paperSizeProfile,
            double widthMM, double heightMM,
            double marginTop, double marginLeft, double marginRight, double marginBottom,
            int numCopies, string orientation, double slipLengthIn)
        {
            try
            {
                using (var conn = new SQLiteConnection(ConnStr))
                {
                    conn.Open();
                    ExecNQ(conn, "UPDATE PrinterProfiles SET IsActive=0;");
                    const string sql = @"
                        INSERT INTO PrinterProfiles
                            (ProfileName, PrinterName, Mode, WidthMM, HeightMM,
                             MarginTopMM, MarginLeftMM, MarginRightMM, MarginBottomMM,
                             NumCopies, Orientation, SlipLengthIn, IsActive,
                             SlipFontScale, SlipOffsetXMm, SlipOffsetYMm, CopiesPerPage)
                        VALUES (@PN, @P, @M, @W, @H, @T, @L, @R, @B, @C, @O, @SL, 1,
                                1.0, 0.0, 0.0, 1)
                        ON CONFLICT(ProfileName) DO UPDATE SET
                            PrinterName    = excluded.PrinterName,
                            Mode           = excluded.Mode,
                            WidthMM        = excluded.WidthMM,
                            HeightMM       = excluded.HeightMM,
                            MarginTopMM    = excluded.MarginTopMM,
                            MarginLeftMM   = excluded.MarginLeftMM,
                            MarginRightMM  = excluded.MarginRightMM,
                            MarginBottomMM = excluded.MarginBottomMM,
                            NumCopies      = excluded.NumCopies,
                            Orientation    = excluded.Orientation,
                            SlipLengthIn   = excluded.SlipLengthIn,
                            IsActive       = excluded.IsActive;";
                    using (var cmd = new SQLiteCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@PN", profileName);
                        cmd.Parameters.AddWithValue("@P",  printerName);
                        cmd.Parameters.AddWithValue("@M",  paperSizeProfile);
                        cmd.Parameters.AddWithValue("@W",  widthMM);
                        cmd.Parameters.AddWithValue("@H",  heightMM);
                        cmd.Parameters.AddWithValue("@T",  marginTop);
                        cmd.Parameters.AddWithValue("@L",  marginLeft);
                        cmd.Parameters.AddWithValue("@R",  marginRight);
                        cmd.Parameters.AddWithValue("@B",  marginBottom);
                        cmd.Parameters.AddWithValue("@C",  numCopies);
                        cmd.Parameters.AddWithValue("@O",  orientation);
                        cmd.Parameters.AddWithValue("@SL", slipLengthIn);
                        cmd.ExecuteNonQuery();
                    }
                }
                SyncGlobalSettingsFromProfile(printerName, paperSizeProfile, orientation, numCopies, slipLengthIn);
            }
            catch (Exception ex) { MessageBox.Show("Error saving printer profile: " + ex.Message); }
        }

        // Writes only the calibration columns for an existing profile row.
        public static void SaveCalibrationToProfile(string profileName,
            float slipFontScale, float slipOffsetXMm, float slipOffsetYMm, int copiesPerPage)
        {
            try
            {
                using (var conn = new SQLiteConnection(ConnStr))
                {
                    conn.Open();
                    const string sql = @"
                        UPDATE PrinterProfiles SET
                            SlipFontScale = @FS,
                            SlipOffsetXMm = @OX,
                            SlipOffsetYMm = @OY,
                            CopiesPerPage = @CP
                        WHERE ProfileName = @PN;";
                    using (var cmd = new SQLiteCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@PN", profileName);
                        cmd.Parameters.AddWithValue("@FS", slipFontScale);
                        cmd.Parameters.AddWithValue("@OX", slipOffsetXMm);
                        cmd.Parameters.AddWithValue("@OY", slipOffsetYMm);
                        cmd.Parameters.AddWithValue("@CP", copiesPerPage);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex) { MessageBox.Show("Error saving calibration: " + ex.Message); }
        }

        // Flips IsActive to the named preset and syncs GlobalSettings.
        public static void SetActiveProfile(string profileName)
        {
            try
            {
                using (var conn = new SQLiteConnection(ConnStr))
                {
                    conn.Open();
                    ExecNQ(conn, "UPDATE PrinterProfiles SET IsActive=0;");
                    using (var cmd = new SQLiteCommand("UPDATE PrinterProfiles SET IsActive=1 WHERE ProfileName=@N;", conn))
                    {
                        cmd.Parameters.AddWithValue("@N", profileName);
                        cmd.ExecuteNonQuery();
                    }
                }
                var data = GetPrinterProfileByName(profileName);
                if (data != null)
                    SyncGlobalSettingsFromProfile(data.PrinterName, data.PaperSizeProfile, data.Orientation, data.NumCopies, data.SlipLengthIn);
            }
            catch (Exception ex) { MessageBox.Show("Error setting active profile: " + ex.Message); }
        }

        private static void SyncGlobalSettingsFromProfile(string printerName, string paperSizeProfile,
            string orientation, int numCopies, double slipLengthIn)
        {
            SaveGlobalSetting("SelectedPrinter",  printerName);
            SaveGlobalSetting("PaperSizeProfile", paperSizeProfile);
            SaveGlobalSetting("PrintOrientation", orientation);
            SaveGlobalSetting("PrintCopiesCount", numCopies.ToString());
            SaveGlobalSetting("SlipCustomLength", slipLengthIn.ToString("G", System.Globalization.CultureInfo.InvariantCulture));
        }

        // ===================================================================
        // SETTINGS
        // ===================================================================

        public static string GetGlobalSetting(string key, string defaultValue = "")
        {
            try
            {
                using (var conn = new SQLiteConnection(ConnStr))
                {
                    conn.Open();
                    using (var cmd = new SQLiteCommand("SELECT SettingValue FROM GlobalSettings WHERE SettingKey=@K;", conn))
                    {
                        cmd.Parameters.AddWithValue("@K", key);
                        var result = cmd.ExecuteScalar();
                        return result != null ? result.ToString() : defaultValue;
                    }
                }
            }
            catch { return defaultValue; }
        }

        public static void SaveGlobalSetting(string key, string value)
        {
            try
            {
                using (var conn = new SQLiteConnection(ConnStr))
                {
                    conn.Open();
                    using (var cmd = new SQLiteCommand("INSERT OR REPLACE INTO GlobalSettings (SettingKey, SettingValue) VALUES (@K, @V);", conn))
                    {
                        cmd.Parameters.AddWithValue("@K", key);
                        cmd.Parameters.AddWithValue("@V", value);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex) { MessageBox.Show("Error saving setting: " + ex.Message); }
        }

        // ===================================================================
        // LOOKUP TABLES
        // ===================================================================

        // tableName must be one of: TruckRegs, StockpileRefs, ROMTypes, BlockNrs, Sizes, Clients, Destinations
        public static List<string> GetLookupValues(string tableName)
        {
            var list = new List<string>();
            try
            {
                using (var conn = new SQLiteConnection(ConnStr))
                {
                    conn.Open();
                    using (var cmd = new SQLiteCommand($"SELECT Value FROM {tableName} ORDER BY Value;", conn))
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                            list.Add(r["Value"].ToString());
                    }
                }
            }
            catch { }
            return list;
        }

        // Removes an entry by value. Never touches the Slips table — historical text is unaffected.
        public static void DeleteLookupValue(string tableName, string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            try
            {
                using (var conn = new SQLiteConnection(ConnStr))
                {
                    conn.Open();
                    using (var cmd = new SQLiteCommand($"DELETE FROM {tableName} WHERE Value=@V;", conn))
                    {
                        cmd.Parameters.AddWithValue("@V", value.Trim());
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch { }
        }

        // Silently ignores duplicates (INSERT OR IGNORE). tableName same whitelist as above.
        public static void SaveLookupValue(string tableName, string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            try
            {
                using (var conn = new SQLiteConnection(ConnStr))
                {
                    conn.Open();
                    using (var cmd = new SQLiteCommand($"INSERT OR IGNORE INTO {tableName} (Value) VALUES (@V);", conn))
                    {
                        cmd.Parameters.AddWithValue("@V", value.Trim());
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch { }
        }

        // ===================================================================
        // FIELD CONFIG
        // ===================================================================

        public class FieldLayoutSettings
        {
            public string CustomName    { get; set; }
            public int    PositionOrder { get; set; }
            public bool   IsHidden      { get; set; }
            public string LookupTable   { get; set; }
            public bool   IsRequired    { get; set; }
        }

        // Returns FieldConfig as a DataTable with the exact column aliases the grid expects.
        public static DataTable GetFieldConfigAsDataTable()
        {
            var dt = new DataTable();
            try
            {
                using (var conn = new SQLiteConnection(ConnStr))
                {
                    conn.Open();
                    const string sql = @"
                        SELECT FieldSlot  AS [Database Slot],
                               LabelName  AS [Label Name],
                               OrderLine  AS [Order Line],
                               Hidden     AS [Hidden (1=Yes)],
                               IsRequired AS [Required (1=Yes)]
                        FROM FieldConfig
                        ORDER BY OrderLine ASC;";
                    using (var adapter = new SQLiteDataAdapter(sql, conn))
                        adapter.Fill(dt);
                }
            }
            catch (Exception ex) { MessageBox.Show("Failed loading field config: " + ex.Message); }
            return dt;
        }

        // Persists every row of the DataTable back to FieldConfig. Throws on error so callers can surface it.
        public static void SaveFieldConfigFromDataTable(DataTable dt)
        {
            using (var conn = new SQLiteConnection(ConnStr))
            {
                conn.Open();
                foreach (DataRow row in dt.Rows)
                {
                    const string sql = @"
                        UPDATE FieldConfig SET
                            LabelName  = @LabelName,
                            OrderLine  = @OrderLine,
                            Hidden     = @Hidden,
                            IsRequired = @IsRequired
                        WHERE FieldSlot = @FieldSlot;";
                    using (var cmd = new SQLiteCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@FieldSlot",  row["Database Slot"].ToString());
                        cmd.Parameters.AddWithValue("@LabelName",  row["Label Name"].ToString());
                        cmd.Parameters.AddWithValue("@OrderLine",  Convert.ToInt32(row["Order Line"]));
                        cmd.Parameters.AddWithValue("@Hidden",     Convert.ToInt32(row["Hidden (1=Yes)"]));
                        cmd.Parameters.AddWithValue("@IsRequired", Convert.ToInt32(row["Required (1=Yes)"]));
                        cmd.ExecuteNonQuery();
                    }
                }
            }
        }

        // Returns FieldConfig keyed by FieldSlot. Property names kept for backward compatibility.
        public static Dictionary<string, FieldLayoutSettings> GetActiveFieldConfigurations()
        {
            var map = new Dictionary<string, FieldLayoutSettings>();
            try
            {
                using (var conn = new SQLiteConnection(ConnStr))
                {
                    conn.Open();
                    const string sql = "SELECT FieldSlot, LabelName, OrderLine, Hidden, LookupTable, IsRequired FROM FieldConfig ORDER BY OrderLine;";
                    using (var cmd = new SQLiteCommand(sql, conn))
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            map[r["FieldSlot"].ToString()] = new FieldLayoutSettings
                            {
                                CustomName    = r["LabelName"].ToString(),
                                PositionOrder = Convert.ToInt32(r["OrderLine"]),
                                IsHidden      = Convert.ToInt32(r["Hidden"])     == 1,
                                IsRequired    = Convert.ToInt32(r["IsRequired"]) == 1,
                                LookupTable   = r["LookupTable"].ToString(),
                            };
                        }
                    }
                }
            }
            catch (Exception ex) { MessageBox.Show("Error loading field config: " + ex.Message); }
            return map;
        }

        // ===================================================================
        // DAILY SUMMARY
        // ===================================================================

        // Returns total printed loads and total tons for today (local time).
        public static (int loads, double totalTons) GetDailySummary()
        {
            try
            {
                using (var conn = new SQLiteConnection(ConnStr))
                {
                    conn.Open();
                    const string sql = @"
                        SELECT COUNT(*) AS Loads,
                               COALESCE(SUM(CAST(Field7 AS REAL)), 0) AS TotalTons
                        FROM Slips
                        WHERE Status = 'Printed'
                          AND date(PrintedAt) = date('now', 'localtime');";
                    // PrintedAt is already machine-local, so it must NOT be converted again here.
                    using (var cmd = new SQLiteCommand(sql, conn))
                    using (var r = cmd.ExecuteReader())
                        if (r.Read())
                            return (Convert.ToInt32(r["Loads"]), Convert.ToDouble(r["TotalTons"]));
                }
            }
            catch (Exception ex) { MessageBox.Show("Summary error: " + ex.Message); }
            return (0, 0);
        }

        // Returns up to maxRows rows of (fieldValue, tons, loads) for today, grouped by fieldSlot.
        public static List<(string value, double tons, int loads)> GetDailyTonsByField(string fieldSlot, int maxRows = 6)
        {
            var result = new List<(string, double, int)>();
            // fieldSlot is always "Field1"–"Field10" set by our code, never user text
            if (string.IsNullOrWhiteSpace(fieldSlot)) return result;
            try
            {
                using (var conn = new SQLiteConnection(ConnStr))
                {
                    conn.Open();
                    string sql = $@"
                        SELECT {fieldSlot} AS Val,
                               COALESCE(SUM(CAST(Field7 AS REAL)), 0) AS Tons,
                               COUNT(*) AS Loads
                        FROM Slips
                        WHERE Status = 'Printed'
                          AND date(PrintedAt) = date('now', 'localtime')
                          AND {fieldSlot} IS NOT NULL AND {fieldSlot} != ''
                        GROUP BY {fieldSlot}
                        ORDER BY Tons DESC
                        LIMIT {maxRows};";
                    using (var cmd = new SQLiteCommand(sql, conn))
                    using (var r = cmd.ExecuteReader())
                        while (r.Read())
                            result.Add((r["Val"].ToString(),
                                        Convert.ToDouble(r["Tons"]),
                                        Convert.ToInt32(r["Loads"])));
                }
            }
            catch (Exception ex) { MessageBox.Show("Breakdown error: " + ex.Message); }
            return result;
        }

        // ===================================================================
        // BACKUP
        // ===================================================================

        // Copies the DB to a user-chosen path. Returns true on success.
        public static bool PerformManualBackup(string destinationPath)
        {
            try
            {
                File.Copy(DbPath, destinationPath, overwrite: true);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Backup failed: " + ex.Message, "Backup Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        // Copies the DB to Backups/ with a timestamp filename, then prunes files older than 30 days.
        // Called once on startup. Failures are silently swallowed — backup must never block the app.
        public static void PerformStartupBackup()
        {
            try
            {
                string backupDir = Path.Combine(Path.GetDirectoryName(DbPath), "Backups");
                Directory.CreateDirectory(backupDir);

                string stamp      = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string backupFile = Path.Combine(backupDir, $"WeighbridgeData_{stamp}.db");
                File.Copy(DbPath, backupFile, overwrite: true);

                // File.Copy carries the SOURCE file's timestamp across, so a fresh backup of a
                // database that hasn't been written to in a while arrives already "old". Stamp it
                // with the real backup time, otherwise the purge below measures the age of the
                // data instead of the age of the backup and can delete a copy made moments ago.
                File.SetLastWriteTime(backupFile, DateTime.Now);

                // Rolling 30-day purge
                foreach (string f in Directory.GetFiles(backupDir, "WeighbridgeData_*.db"))
                {
                    if (File.GetLastWriteTime(f) < DateTime.Now.AddDays(-30))
                        File.Delete(f);
                }
            }
            catch { }
        }

        // ===================================================================
        // HELPERS
        // ===================================================================

        private static void ExecNQ(SQLiteConnection conn, string sql)
        {
            using (var cmd = new SQLiteCommand(sql, conn))
                cmd.ExecuteNonQuery();
        }
    }
}
