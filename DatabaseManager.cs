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
        // The data deliberately lives OUTSIDE the program folder. Two reasons, both of which
        // this system cannot afford to get wrong:
        //
        //   1. An upgrade replaces the program folder. With the database sitting inside it,
        //      installing a new version would delete every slip the operator has ever entered.
        //   2. Installed under Program Files, a standard user cannot write there at all.
        //      Windows does not fail the write; it silently redirects it into a per-user
        //      VirtualStore copy that nobody can find and no backup covers. Slips would
        //      appear to save and then simply not be there.
        private static readonly string DataFolder = ResolveDataFolder();

        public  static readonly string DbPath  = Path.Combine(DataFolder, "WeighbridgeData.db");
        private static string ConnStr => $"Data Source={DbPath};Version=3;";

        // Exposed so the installer notes, the backup screen and any future diagnostics can all
        // name the same folder rather than each recomputing it.
        public static string DataDirectory => DataFolder;

        private static string ResolveDataFolder()
        {
            string legacy = AppDomain.CurrentDomain.BaseDirectory;

            string folder;
            try
            {
                folder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "UitvalSlips");
                Directory.CreateDirectory(folder);
            }
            catch
            {
                // ProgramData unreachable. Keep working beside the exe rather than refusing to
                // start: worse for upgrades, but it never costs the operator a day's slips.
                return legacy;
            }

            // A failed migration must NOT send us back to the program folder, which may be
            // read-only. The new folder is usable either way; at worst the old data is still
            // sitting where it was and can be moved by hand.
            try { MigrateFromProgramFolder(legacy, folder); }
            catch { }

            return folder;
        }

        // Moves a database left in the program folder by an earlier version, once.
        // Move rather than copy on purpose: two databases with the same name and diverging
        // contents is exactly the ambiguity this system exists to prevent.
        private static void MigrateFromProgramFolder(string legacyFolder, string newFolder)
        {
            string oldDb = Path.Combine(legacyFolder, "WeighbridgeData.db");
            string newDb = Path.Combine(newFolder,    "WeighbridgeData.db");

            if (!File.Exists(oldDb) || File.Exists(newDb)) return;

            File.Move(oldDb, newDb);

            string oldBackups = Path.Combine(legacyFolder, "Backups");
            string newBackups = Path.Combine(newFolder,    "Backups");
            if (Directory.Exists(oldBackups) && !Directory.Exists(newBackups))
                Directory.Move(oldBackups, newBackups);
        }

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

                    // Dropdown suggestion lists (DEF-030). A list belongs to a field's LABEL,
                    // not to its slot, so renaming a field moves it to a different list and
                    // renaming it back returns the original one. Nothing is created here: a
                    // fresh database has no lists at all until values are actually entered.
                    ExecNQ(conn, @"
                        CREATE TABLE IF NOT EXISTS LookupLists (
                            ListID    INTEGER PRIMARY KEY AUTOINCREMENT,
                            ListName  TEXT    NOT NULL UNIQUE COLLATE NOCASE,
                            CreatedAt TEXT    NOT NULL DEFAULT (datetime('now','localtime'))
                        );");

                    // Kept separate from LookupLists so an emptied list still exists and stays
                    // attached to its label — clearing a list and deleting it are different acts.
                    ExecNQ(conn, @"
                        CREATE TABLE IF NOT EXISTS LookupEntries (
                            EntryID INTEGER PRIMARY KEY AUTOINCREMENT,
                            ListID  INTEGER NOT NULL REFERENCES LookupLists(ListID),
                            Value   TEXT    NOT NULL,
                            UNIQUE (ListID, Value)
                        );");

                    // The company logo lives in the database rather than as a path to a file on
                    // disk. A path only means something on the machine that chose it, so it never
                    // survives being handed to a customer, and it breaks the moment the picture is
                    // moved or deleted. Held here it travels with the database, every backup
                    // captures it, and nothing outside the file can invalidate it. One row only.
                    ExecNQ(conn, @"
                        CREATE TABLE IF NOT EXISTS CompanyLogo (
                            LogoID     INTEGER PRIMARY KEY CHECK (LogoID = 1),
                            ImageBytes BLOB NOT NULL,
                            FileName   TEXT NOT NULL DEFAULT '',
                            SavedAt    TEXT NOT NULL DEFAULT (datetime('now','localtime'))
                        );");

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

            // DEF-030: suggestion lists used to be nine fixed tables, each bound to a field
            // SLOT through FieldConfig.LookupTable. A rename left the field pointing at the
            // old domain, and newly typed values were written into the mismatched list, so
            // the two progressively interleaved. Lists are now keyed by the field's label
            // (LookupLists / LookupEntries) and the binding column is gone.
            //
            // The old tables and their contents are dropped rather than migrated — agreed
            // with the project owner on 2026-08-11, since lists rebuild themselves from use
            // within a day of operation. The column goes too: leaving a dead column behind
            // is exactly what DEF-021 already records against the last refactor.
            foreach (string legacy in new[] { "TruckRegs", "StockpileRefs", "ROMTypes",
                                              "BlockNrs", "Sizes", "Clients",
                                              "Destinations", "OrderNumbers", "Slots" })
                ExecNQ(conn, "DROP TABLE IF EXISTS " + legacy + ";");

            if (hasLookupTable)
            {
                // DROP COLUMN needs SQLite 3.35+. If the bundled engine is older the column
                // simply stays behind unused — nothing reads it any more either way.
                try { ExecNQ(conn, "ALTER TABLE FieldConfig DROP COLUMN LookupTable;"); }
                catch { }
            }

            if (!hasIsRequired)
                ExecNQ(conn, "ALTER TABLE FieldConfig ADD COLUMN IsRequired INTEGER NOT NULL DEFAULT 0;");

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
            bool hasMultiSlipLayout = false;
            using (var cmd = new SQLiteCommand("PRAGMA table_info(PrinterProfiles);", conn))
            using (var r = cmd.ExecuteReader())
                while (r.Read())
                {
                    string col = r["name"].ToString();
                    if (col.Equals("ProfileName",     StringComparison.OrdinalIgnoreCase)) hasProfileName     = true;
                    if (col.Equals("Orientation",     StringComparison.OrdinalIgnoreCase)) hasOrientation     = true;
                    if (col.Equals("SlipLengthIn",    StringComparison.OrdinalIgnoreCase)) hasSlipLengthIn    = true;
                    if (col.Equals("SlipFontScale",   StringComparison.OrdinalIgnoreCase)) hasSlipFontScale   = true;
                    if (col.Equals("SlipOffsetXMm",   StringComparison.OrdinalIgnoreCase)) hasSlipOffsetXMm   = true;
                    if (col.Equals("SlipOffsetYMm",   StringComparison.OrdinalIgnoreCase)) hasSlipOffsetYMm   = true;
                    if (col.Equals("CopiesPerPage",   StringComparison.OrdinalIgnoreCase)) hasCopiesPerPage   = true;
                    if (col.Equals("MultiSlipLayout", StringComparison.OrdinalIgnoreCase)) hasMultiSlipLayout = true;
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

            // How multiple copies sit on the page. 'Columns' matches what the engine did before
            // this column existed, so an upgraded database keeps printing exactly as it did.
            if (!hasMultiSlipLayout)
                ExecNQ(conn, "ALTER TABLE PrinterProfiles ADD COLUMN MultiSlipLayout TEXT NOT NULL DEFAULT 'Columns';");

            // DEF-021: the three columns above superseded GlobalSettings rows of the same name,
            // but the 3 July migration left those rows in place. Nothing has read them since, so
            // there is no runtime symptom — the harm is that they still hold their pre-refactor
            // values and read as authoritative. A database created on 7 August reported
            // SlipFontScale 2.75 while the active profile was printing at 1.95, and offsets of
            // 0 and -1.5 against a live -1 and 0. Removing them here, alongside the columns that
            // replaced them, is what the original migration should have done.
            //
            // The three keys are named rather than swept by a whitelist of live settings: an
            // unrecognised key is far likelier to be one added after this code was written than
            // one this refactor stranded, and deleting it would be the worse mistake.
            ExecNQ(conn, @"
                DELETE FROM GlobalSettings
                WHERE SettingKey IN ('SlipFontScale', 'SlipOffsetXMm', 'SlipOffsetYMm');");

            // 3-copies layout has been removed (asymmetric 2×2 with blank cell). Migrate to 2.
            ExecNQ(conn, "UPDATE PrinterProfiles SET CopiesPerPage = 2 WHERE CopiesPerPage = 3;");

            // LogoPath held an absolute path to a picture elsewhere on the machine; the image now
            // lives in CompanyLogo. Read the old path once and, if it still resolves, pull the
            // picture in so a configured logo survives the upgrade. Then drop the key either way
            // rather than leave it behind reading as though it still meant something (DEF-021).
            string oldLogoPath = null;
            using (var cmd = new SQLiteCommand(
                "SELECT SettingValue FROM GlobalSettings WHERE SettingKey = 'LogoPath';", conn))
            {
                object v = cmd.ExecuteScalar();
                if (v != null && v != DBNull.Value) oldLogoPath = v.ToString();
            }

            if (oldLogoPath != null)
            {
                bool haveLogo;
                using (var cmd = new SQLiteCommand("SELECT COUNT(*) FROM CompanyLogo;", conn))
                    haveLogo = Convert.ToInt64(cmd.ExecuteScalar()) > 0;

                if (!haveLogo && oldLogoPath.Length > 0 && File.Exists(oldLogoPath))
                {
                    try
                    {
                        byte[] bytes = File.ReadAllBytes(oldLogoPath);
                        using (var cmd = new SQLiteCommand(
                            "INSERT INTO CompanyLogo (LogoID, ImageBytes, FileName) VALUES (1, @B, @N);", conn))
                        {
                            cmd.Parameters.Add("@B", DbType.Binary).Value = bytes;
                            cmd.Parameters.AddWithValue("@N", Path.GetFileName(oldLogoPath));
                            cmd.ExecuteNonQuery();
                        }
                    }
                    catch { }   // unreadable file: nothing to carry over, the operator re-picks it
                }

                ExecNQ(conn, "DELETE FROM GlobalSettings WHERE SettingKey = 'LogoPath';");
            }
        }

        private static void SeedFieldConfigIfEmpty(SQLiteConnection conn)
        {
            using (var cmd = new SQLiteCommand("SELECT COUNT(*) FROM FieldConfig;", conn))
            {
                if ((long)cmd.ExecuteScalar() > 0) return;
            }

            // No suggestion list is named here. A list comes into being the first time a value
            // is saved under a label, so these defaults start with empty dropdowns and fill
            // themselves as the operator works (DEF-030).
            var defaults = new[]
            {
                ("Field1",  "Truck Reg",      1),
                ("Field2",  "Stockpile Name", 2),
                ("Field3",  "Rom Type",       3),
                ("Field4",  "Block Nr",       4),
                ("Field5",  "Size",           5),
                ("Field6",  "Destination",    6),
                ("Field7",  "Tons",           7),
                ("Field8",  "Order Number",   8),
                ("Field9",  "Slot",           9),
                ("Field10", "Client",         10),
            };

            foreach (var (slot, label, order) in defaults)
            {
                // Field1 and Field7 are always required — spec §4.2/4.4
                int isRequired = (slot == "Field1" || slot == "Field7") ? 1 : 0;
                using (var cmd = new SQLiteCommand(
                    "INSERT INTO FieldConfig (FieldSlot, LabelName, OrderLine, Hidden, IsRequired) VALUES (@S, @L, @O, 0, @R);", conn))
                {
                    cmd.Parameters.AddWithValue("@S", slot);
                    cmd.Parameters.AddWithValue("@L", label);
                    cmd.Parameters.AddWithValue("@O", order);
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
                ("LogoPlacement",     "Above"),
                ("LogoHeightMm",      "15"),
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
                     SlipFontScale, SlipOffsetXMm, SlipOffsetYMm, CopiesPerPage,
                     MultiSlipLayout)
                VALUES ('Default', 'EPSON LX-350', 'Small240x102', 240, 139.7,
                        10, 10, 10, 10, 1, 'Portrait', 5.5, 1,
                        1.0, 0.0, 0.0, 1, 'Columns');");
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

                            var data = new Dictionary<string, string>
                            {
                                ["SlipID"]     = slipId.ToString(),
                                ["BillNumber"] = billNum,
                            };
                            for (int i = 1; i <= 10; i++)
                                data["Field" + i] = reader["Field" + i]?.ToString() ?? "";

                            panel.Controls.Add(BuildTile(slipId, data));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading dashboard: " + ex.Message);
            }
        }

        // ===================================================================
        // MAIN PAGE TILE DISPLAY
        // ===================================================================

        // A tile shows the slip number plus up to this many operator-chosen fields.
        public const int MaxTileFields = 4;

        // The field slots shown on a tile, in the order the operator picked them.
        public static List<string> GetTileFieldSlots()
        {
            var slots = new List<string>();
            foreach (string part in GetGlobalSetting("TileFields", "Field1,Field7").Split(','))
            {
                string s = part.Trim();
                if (s.Length == 0 || slots.Contains(s)) continue;
                slots.Add(s);
                if (slots.Count >= MaxTileFields) break;
            }
            if (slots.Count == 0) slots.Add("Field1");
            return slots;
        }

        public static void SaveTileFieldSlots(IEnumerable<string> slots)
        {
            var list = new List<string>();
            foreach (string s in slots)
            {
                if (string.IsNullOrWhiteSpace(s) || list.Contains(s.Trim())) continue;
                list.Add(s.Trim());
                if (list.Count >= MaxTileFields) break;
            }
            SaveGlobalSetting("TileFields", string.Join(",", list));
        }

        // Builds a tile's caption: the slip number, then one "Label: value" line per configured
        // field, using whatever the field is currently called. Both the dashboard loader and
        // CreateSlip call this, so a tile never renders differently depending on which code
        // path produced it.
        public static string BuildTileText(int slipId, Dictionary<string, string> data)
        {
            var cfgs = GetActiveFieldConfigurations();
            var sb   = new System.Text.StringBuilder();
            sb.Append("Slip: ").Append(slipId);

            foreach (string slot in GetTileFieldSlots())
            {
                if (!cfgs.TryGetValue(slot, out var cfg)) continue;

                string label = string.IsNullOrWhiteSpace(cfg.CustomName)
                    ? "Field " + slot.Replace("Field", "")
                    : cfg.CustomName.TrimEnd(':').Trim();

                string value = (data.TryGetValue(slot, out string v) && !string.IsNullOrWhiteSpace(v))
                    ? v : "-";

                sb.AppendLine().Append(label).Append(": ").Append(value);
            }
            return sb.ToString();
        }

        private static Button BuildTile(int slipId, Dictionary<string, string> data)
        {
            var tile = new Button
            {
                Size      = new Size(215, 132),
                BackColor = Color.LightYellow,
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Arial", 9, FontStyle.Bold),
                Text      = BuildTileText(slipId, data),
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
            public string MultiSlipLayout { get; set; } = SlipPrintEngine.LayoutColumns;
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
                                                NumCopies, SlipFontScale, SlipOffsetXMm, SlipOffsetYMm,
                                                CopiesPerPage, MultiSlipLayout
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
                                MultiSlipLayout = r["MultiSlipLayout"] == DBNull.Value
                                    ? SlipPrintEngine.LayoutColumns
                                    : r["MultiSlipLayout"].ToString(),
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
            public string MultiSlipLayout  { get; set; } = SlipPrintEngine.LayoutColumns;
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
                MultiSlipLayout  = r["MultiSlipLayout"] == DBNull.Value
                    ? SlipPrintEngine.LayoutColumns
                    : r["MultiSlipLayout"].ToString(),
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
                             SlipFontScale, SlipOffsetXMm, SlipOffsetYMm, CopiesPerPage,
                             MultiSlipLayout)
                        VALUES (@PN, @P, @M, @W, @H, @T, @L, @R, @B, @C, @O, @SL, 1,
                                1.0, 0.0, 0.0, 1, 'Columns')
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
            float slipFontScale, float slipOffsetXMm, float slipOffsetYMm, int copiesPerPage,
            string multiSlipLayout = SlipPrintEngine.LayoutColumns)
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
                            CopiesPerPage = @CP,
                            MultiSlipLayout = @ML
                        WHERE ProfileName = @PN;";
                    using (var cmd = new SQLiteCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@PN", profileName);
                        cmd.Parameters.AddWithValue("@FS", slipFontScale);
                        cmd.Parameters.AddWithValue("@OX", slipOffsetXMm);
                        cmd.Parameters.AddWithValue("@OY", slipOffsetYMm);
                        cmd.Parameters.AddWithValue("@CP", copiesPerPage);
                        cmd.Parameters.AddWithValue("@ML", multiSlipLayout ?? SlipPrintEngine.LayoutColumns);
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

        // A suggestion list is identified by the field's LABEL rather than by its slot, so a
        // rename carries the field to a different list and renaming back returns the original
        // one intact. Because the name is now a value and not a table identifier, every
        // statement below is fully parameterised — the old design had to interpolate it.
        //
        // Matching ignores surrounding space, a trailing colon and case, so tidying a label
        // ("Truck Reg:" to "Truck Reg") does not strand the values already collected under it.
        public static string NormalizeListName(string label)
        {
            if (string.IsNullOrWhiteSpace(label)) return "";
            return label.Trim().TrimEnd(':').Trim();
        }

        // The list name for a field slot. Falls back to the same "Field 10" wording the tiles,
        // the export and the entry form use, so a field left unlabelled still collects values.
        public static string ListNameFor(string fieldSlot, string label)
        {
            string name = NormalizeListName(label);
            return name.Length > 0 ? name : "Field " + fieldSlot.Replace("Field", "");
        }

        // Empty for a name that has never been used — a list is created by use, not up front.
        public static List<string> GetLookupValues(string listName)
        {
            var list = new List<string>();
            string name = NormalizeListName(listName);
            if (name.Length == 0) return list;
            try
            {
                using (var conn = new SQLiteConnection(ConnStr))
                {
                    conn.Open();
                    using (var cmd = new SQLiteCommand(@"
                        SELECT e.Value
                        FROM   LookupEntries e
                        JOIN   LookupLists   l ON l.ListID = e.ListID
                        WHERE  l.ListName = @N
                        ORDER BY e.Value;", conn))
                    {
                        cmd.Parameters.AddWithValue("@N", name);
                        using (var r = cmd.ExecuteReader())
                            while (r.Read())
                                list.Add(r["Value"].ToString());
                    }
                }
            }
            catch { }
            return list;
        }

        // Creates the list on first use. Duplicates within a list are ignored.
        public static void SaveLookupValue(string listName, string value)
        {
            string name = NormalizeListName(listName);
            if (name.Length == 0 || string.IsNullOrWhiteSpace(value)) return;
            try
            {
                using (var conn = new SQLiteConnection(ConnStr))
                {
                    conn.Open();
                    using (var cmd = new SQLiteCommand(
                        "INSERT OR IGNORE INTO LookupLists (ListName) VALUES (@N);", conn))
                    {
                        cmd.Parameters.AddWithValue("@N", name);
                        cmd.ExecuteNonQuery();
                    }
                    using (var cmd = new SQLiteCommand(@"
                        INSERT OR IGNORE INTO LookupEntries (ListID, Value)
                        SELECT ListID, @V FROM LookupLists WHERE ListName = @N;", conn))
                    {
                        cmd.Parameters.AddWithValue("@N", name);
                        cmd.Parameters.AddWithValue("@V", value.Trim());
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch { }
        }

        // Removes one entry. Never touches the Slips table — a slip stores the text it was
        // given and holds no reference to any list, so historical records are unaffected.
        public static void DeleteLookupValue(string listName, string value)
        {
            string name = NormalizeListName(listName);
            if (name.Length == 0 || string.IsNullOrWhiteSpace(value)) return;
            try
            {
                using (var conn = new SQLiteConnection(ConnStr))
                {
                    conn.Open();
                    using (var cmd = new SQLiteCommand(@"
                        DELETE FROM LookupEntries
                        WHERE  Value  = @V
                          AND  ListID = (SELECT ListID FROM LookupLists WHERE ListName = @N);", conn))
                    {
                        cmd.Parameters.AddWithValue("@N", name);
                        cmd.Parameters.AddWithValue("@V", value.Trim());
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch { }
        }

        // Empties a list but keeps it, so it stays attached to its label and starts refilling
        // from the next slip. Deleting the list itself is DeleteLookupList.
        public static void ClearLookupList(string listName)
        {
            string name = NormalizeListName(listName);
            if (name.Length == 0) return;
            try
            {
                using (var conn = new SQLiteConnection(ConnStr))
                {
                    conn.Open();
                    using (var cmd = new SQLiteCommand(@"
                        DELETE FROM LookupEntries
                        WHERE ListID = (SELECT ListID FROM LookupLists WHERE ListName = @N);", conn))
                    {
                        cmd.Parameters.AddWithValue("@N", name);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch { }
        }

        // Removes the list and everything in it. If a field still carries this label the list
        // simply reappears, empty, the next time a value is saved against it.
        public static void DeleteLookupList(string listName)
        {
            string name = NormalizeListName(listName);
            if (name.Length == 0) return;
            try
            {
                using (var conn = new SQLiteConnection(ConnStr))
                {
                    conn.Open();
                    using (var cmd = new SQLiteCommand(@"
                        DELETE FROM LookupEntries
                        WHERE ListID = (SELECT ListID FROM LookupLists WHERE ListName = @N);", conn))
                    {
                        cmd.Parameters.AddWithValue("@N", name);
                        cmd.ExecuteNonQuery();
                    }
                    using (var cmd = new SQLiteCommand(
                        "DELETE FROM LookupLists WHERE ListName = @N;", conn))
                    {
                        cmd.Parameters.AddWithValue("@N", name);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch { }
        }

        public class LookupListInfo
        {
            public string Name  { get; set; }
            public int    Count { get; set; }
            // False once no field carries this label any more. Such a list is kept, not purged:
            // renaming a field back to its old label is meant to bring its suggestions with it.
            public bool   InUse { get; set; }
        }

        // Every list that exists, including ones no field currently points at, so the operator
        // can see and clear up what earlier labels left behind.
        public static List<LookupListInfo> GetAllLookupLists()
        {
            var lists = new List<LookupListInfo>();

            var inUse = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in GetActiveFieldConfigurations())
                if (kvp.Key != "Field7")
                    inUse.Add(ListNameFor(kvp.Key, kvp.Value.CustomName));

            try
            {
                using (var conn = new SQLiteConnection(ConnStr))
                {
                    conn.Open();
                    using (var cmd = new SQLiteCommand(@"
                        SELECT l.ListName AS Name, COUNT(e.EntryID) AS N
                        FROM   LookupLists   l
                        LEFT JOIN LookupEntries e ON e.ListID = l.ListID
                        GROUP BY l.ListID, l.ListName
                        ORDER BY l.ListName;", conn))
                    using (var r = cmd.ExecuteReader())
                        while (r.Read())
                        {
                            string nm = r["Name"].ToString();
                            lists.Add(new LookupListInfo
                            {
                                Name  = nm,
                                Count = Convert.ToInt32(r["N"]),
                                InUse = inUse.Contains(nm),
                            });
                        }
                }
            }
            catch { }
            return lists;
        }

        // ===================================================================
        // COMPANY LOGO
        // ===================================================================
        // The picture printed above the company name. Held as bytes in the database so it goes
        // wherever the database goes and no file on disk can invalidate it.

        // The renderer asks for this once per printed copy, so a 2x2 sheet would otherwise be
        // four reads of the same blob. Cached here and dropped whenever the logo is written.
        private static byte[] _logoCache;
        private static bool   _logoCacheValid;

        public static byte[] GetCompanyLogo()
        {
            if (_logoCacheValid) return _logoCache;
            byte[] bytes = null;
            try
            {
                using (var conn = new SQLiteConnection(ConnStr))
                {
                    conn.Open();
                    using (var cmd = new SQLiteCommand("SELECT ImageBytes FROM CompanyLogo WHERE LogoID = 1;", conn))
                    {
                        object v = cmd.ExecuteScalar();
                        if (v != null && v != DBNull.Value) bytes = (byte[])v;
                    }
                }
            }
            catch { }
            _logoCache      = bytes;
            _logoCacheValid = true;
            return bytes;
        }

        public static bool HasCompanyLogo() => GetCompanyLogo() != null;

        // The name of the file it came from, kept only so the operator can recognise which
        // picture is currently set. Nothing reads it back off disk.
        public static string GetCompanyLogoName()
        {
            try
            {
                using (var conn = new SQLiteConnection(ConnStr))
                {
                    conn.Open();
                    using (var cmd = new SQLiteCommand("SELECT FileName FROM CompanyLogo WHERE LogoID = 1;", conn))
                    {
                        object v = cmd.ExecuteScalar();
                        if (v != null && v != DBNull.Value) return v.ToString();
                    }
                }
            }
            catch { }
            return "";
        }

        // Throws on failure so the caller can tell the operator. A logo that silently fails to
        // save is one they only discover on a printed slip, which by then cannot be reprinted
        // differently without a reprint of the whole record.
        public static void SaveCompanyLogo(byte[] bytes, string fileName)
        {
            if (bytes == null || bytes.Length == 0)
                throw new ArgumentException("The image is empty.", nameof(bytes));

            using (var conn = new SQLiteConnection(ConnStr))
            {
                conn.Open();
                using (var cmd = new SQLiteCommand(@"
                    INSERT INTO CompanyLogo (LogoID, ImageBytes, FileName, SavedAt)
                    VALUES (1, @B, @N, datetime('now','localtime'))
                    ON CONFLICT(LogoID) DO UPDATE SET
                        ImageBytes = excluded.ImageBytes,
                        FileName   = excluded.FileName,
                        SavedAt    = excluded.SavedAt;", conn))
                {
                    cmd.Parameters.Add("@B", DbType.Binary).Value = bytes;
                    cmd.Parameters.AddWithValue("@N", fileName ?? "");
                    cmd.ExecuteNonQuery();
                }
            }
            _logoCacheValid = false;
        }

        public static void ClearCompanyLogo()
        {
            try
            {
                using (var conn = new SQLiteConnection(ConnStr))
                {
                    conn.Open();
                    ExecNQ(conn, "DELETE FROM CompanyLogo;");
                }
            }
            catch { }
            _logoCacheValid = false;
        }

        // ===================================================================
        // FIELD CONFIG
        // ===================================================================

        public class FieldLayoutSettings
        {
            public string CustomName    { get; set; }
            public int    PositionOrder { get; set; }
            public bool   IsHidden      { get; set; }
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
                    const string sql = "SELECT FieldSlot, LabelName, OrderLine, Hidden, IsRequired FROM FieldConfig ORDER BY OrderLine;";
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

        // Removes the database file.
        //
        // Intended for one situation only: abandoning First-Time Setup after something created
        // the database early. At that point the file can only hold seeded defaults, never
        // operator data, because Program.cs shows setup only when no database exists. Do NOT
        // call this from anywhere a real record could be lost.
        public static bool DeleteDatabaseFile()
        {
            try
            {
                // Release pooled handles, or the file stays locked and the delete silently fails
                SQLiteConnection.ClearAllPools();
                GC.Collect();
                GC.WaitForPendingFinalizers();

                if (File.Exists(DbPath)) File.Delete(DbPath);
                return !File.Exists(DbPath);
            }
            catch { return false; }
        }

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

        // How close together automatic backups are allowed to be. Startup always takes one;
        // between then and closing time, a backup follows a print at most this often. It bounds
        // what a database problem can cost to a few minutes of work rather than the whole day,
        // which is what a once-per-launch backup used to risk.
        private static readonly TimeSpan BackupInterval = TimeSpan.FromMinutes(15);

        // Called once on startup. Always takes a copy, whatever the interval says.
        public static void PerformStartupBackup() => TakeAutomaticBackup(force: true);

        // Called after a slip is created, edited, printed or voided. Skips quietly if the newest
        // backup is recent, so a busy morning does not fill the folder with near-identical copies.
        public static void PerformChangeBackup() => TakeAutomaticBackup(force: false);

        // Copies the DB into Backups/ under a timestamped name, then prunes.
        // Failures are swallowed on purpose: a backup problem must never stop the operator
        // printing a slip, and the print is the thing the business actually needs.
        private static void TakeAutomaticBackup(bool force)
        {
            try
            {
                string backupDir = BackupFolder;
                Directory.CreateDirectory(backupDir);

                if (!force)
                {
                    DateTime newest = DateTime.MinValue;
                    foreach (string f in Directory.GetFiles(backupDir, "WeighbridgeData_*.db"))
                    {
                        DateTime t = File.GetLastWriteTime(f);
                        if (t > newest) newest = t;
                    }
                    if (newest != DateTime.MinValue && DateTime.Now - newest < BackupInterval) return;
                }

                string stamp      = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string backupFile = Path.Combine(backupDir, $"WeighbridgeData_{stamp}.db");
                File.Copy(DbPath, backupFile, overwrite: true);

                // File.Copy carries the SOURCE file's timestamp across, so a fresh backup of a
                // database that hasn't been written to in a while arrives already "old". Stamp it
                // with the real backup time, otherwise the purge below measures the age of the
                // data instead of the age of the backup and can delete a copy made moments ago.
                File.SetLastWriteTime(backupFile, DateTime.Now);

                PruneBackups(backupDir);
            }
            catch { }
        }

        // Recent history stays detailed; older history thins out to one copy a day. Backing up
        // every quarter of an hour would otherwise leave hundreds of near-identical files, and
        // the point of an old backup is "the state on that day", not the exact minute.
        //   - under 3 days old : every copy kept
        //   - 3 to 30 days old : the first copy of each day kept, the rest removed
        //   - over 30 days old : removed
        private static void PruneBackups(string backupDir)
        {
            var keptDays = new HashSet<string>();
            var files    = new List<string>(Directory.GetFiles(backupDir, "WeighbridgeData_*.db"));

            // Oldest first, so the copy kept for a day is that day's earliest.
            files.Sort((a, b) => File.GetLastWriteTime(a).CompareTo(File.GetLastWriteTime(b)));

            foreach (string f in files)
            {
                DateTime taken = File.GetLastWriteTime(f);
                double   ageDays = (DateTime.Now - taken).TotalDays;

                if (ageDays > 30) { try { File.Delete(f); } catch { } continue; }
                if (ageDays <= 3) continue;

                string day = taken.ToString("yyyyMMdd");
                if (keptDays.Add(day)) continue;   // first of that day - keep it
                try { File.Delete(f); } catch { }
            }
        }

        // ===================================================================
        // RESTORE
        // ===================================================================

        public static string BackupFolder => Path.Combine(Path.GetDirectoryName(DbPath), "Backups");

        public class BackupInfo
        {
            public string   FilePath  { get; set; }
            public DateTime TakenAt   { get; set; }
            public long     SizeBytes { get; set; }
            // Counted by opening the file, not guessed from its size. An operator choosing which
            // copy to go back to needs to know what is actually in each one.
            public int      SlipCount { get; set; }
            public int      PrintedCount { get; set; }
            // False when the file cannot be opened or has no Slips table. Such a file is listed
            // but refused, so a corrupt backup can never be swapped in over a working database.
            public bool     IsUsable  { get; set; }
        }

        // Newest first. Reads every backup to report what it holds, so this is deliberately not
        // called on a hot path.
        public static List<BackupInfo> GetAvailableBackups()
        {
            var list = new List<BackupInfo>();
            try
            {
                if (!Directory.Exists(BackupFolder)) return list;

                foreach (string f in Directory.GetFiles(BackupFolder, "WeighbridgeData_*.db"))
                {
                    var info = new BackupInfo
                    {
                        FilePath  = f,
                        TakenAt   = File.GetLastWriteTime(f),
                        SizeBytes = new FileInfo(f).Length,
                        IsUsable  = false,
                    };

                    try
                    {
                        using (var conn = new SQLiteConnection($"Data Source={f};Version=3;Read Only=True;"))
                        {
                            conn.Open();
                            using (var cmd = new SQLiteCommand(
                                "SELECT COUNT(*), COALESCE(SUM(CASE WHEN Status='Printed' THEN 1 ELSE 0 END),0) FROM Slips;", conn))
                            using (var r = cmd.ExecuteReader())
                            {
                                if (r.Read())
                                {
                                    info.SlipCount    = Convert.ToInt32(r.GetValue(0));
                                    info.PrintedCount = Convert.ToInt32(r.GetValue(1));
                                    info.IsUsable     = true;
                                }
                            }
                        }
                    }
                    catch { info.IsUsable = false; }

                    list.Add(info);
                }
            }
            catch { }

            list.Sort((a, b) => b.TakenAt.CompareTo(a.TakenAt));
            return list;
        }

        // Swaps a backup in as the live database. The current file is set ASIDE, never deleted:
        // it may hold slips entered since the backup was taken, and on a proof-of-record system
        // the operator does not get to lose those silently just because they clicked Restore.
        // Returns the path the previous database was moved to, or null on failure.
        public static string RestoreFromBackup(string backupPath, out string error)
        {
            error = null;
            try
            {
                if (!File.Exists(backupPath)) { error = "That backup file no longer exists."; return null; }

                // Refuse an unreadable backup outright rather than discovering it after the swap.
                try
                {
                    using (var conn = new SQLiteConnection($"Data Source={backupPath};Version=3;Read Only=True;"))
                    {
                        conn.Open();
                        using (var cmd = new SQLiteCommand("SELECT COUNT(*) FROM Slips;", conn))
                            cmd.ExecuteScalar();
                    }
                }
                catch
                {
                    error = "That backup cannot be opened and may itself be damaged. "
                          + "Nothing has been changed. Try an earlier one.";
                    return null;
                }

                // Release any pooled handle, or the file moves below will fail on a lock.
                SQLiteConnection.ClearAllPools();
                GC.Collect();
                GC.WaitForPendingFinalizers();

                string stamp   = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string setAside = Path.Combine(Path.GetDirectoryName(DbPath),
                                               $"WeighbridgeData_replaced_{stamp}.db");

                if (File.Exists(DbPath)) File.Move(DbPath, setAside);
                else setAside = null;

                try
                {
                    File.Copy(backupPath, DbPath, overwrite: false);
                }
                catch (Exception ex)
                {
                    // Put the original back rather than leaving the operator with no database.
                    if (setAside != null && File.Exists(setAside) && !File.Exists(DbPath))
                        File.Move(setAside, DbPath);
                    error = "Could not put the backup in place: " + ex.Message;
                    return null;
                }

                // These belong to the database just moved aside. Left behind, SQLite would try to
                // apply them to the restored file and could corrupt it.
                foreach (string ext in new[] { "-journal", "-wal", "-shm" })
                {
                    string stray = DbPath + ext;
                    try { if (File.Exists(stray)) File.Delete(stray); } catch { }
                }

                return setAside ?? "";
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return null;
            }
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
