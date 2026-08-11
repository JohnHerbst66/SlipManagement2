 
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace SlipManagement2 // ⚠️ Change this to match your exact project namespace!
{
    public partial class Main : Form
    {
        private Label _emptyStateLabel;
        private Label _summaryStatsLabel;
        private Label _summaryBreakdownLabel;
        private Label _summaryFieldLabel;

        public Main()
        {
            InitializeComponent();
            DatabaseManager.InitializeDatabase();

            // Hide the old static header label — the empty state label replaces it
            lblSlipsPanel.Visible = false;

            SetupEmptyState();
            SetupManageLookupsButton();
            SetupBackupButton();
            SetupTileDisplayButton();
            SetupSummaryPanel();

            DatabaseManager.LoadUnprintedSlipsToDashboard(this.flpSlips);

            // Keep empty state and summary in sync whenever tiles are added or removed
            flpSlips.ControlAdded   += (s, e) => { UpdateEmptyState(); RefreshSummary(); };
            flpSlips.ControlRemoved += (s, e) => { UpdateEmptyState(); RefreshSummary(); };
            UpdateEmptyState();

            this.Activated += (s, e) => RefreshSummary();
        }

        private void SetupEmptyState()
        {
            // flpSlips is at (12, 78) size (1141, 421) — centre the label inside it
            const int w = 600, h = 110;
            int x = flpSlips.Left + (flpSlips.Width  - w) / 2;
            int y = flpSlips.Top  + (flpSlips.Height - h) / 2;

            _emptyStateLabel = new Label
            {
                Text      = "Welcome!\n\nYour slips will appear here.\nClick \"New Slip\" to get started.",
                AutoSize  = false,
                Size      = new Size(w, h),
                Location  = new Point(x, y),
                TextAlign = ContentAlignment.MiddleCenter,
                Font      = new Font("Arial", 13, FontStyle.Regular),
                ForeColor = Color.Gray,
                BackColor = Color.Transparent,
                Enabled   = false,   // mouse clicks pass through to flpSlips
                Anchor    = AnchorStyles.None,
            };

            this.Controls.Add(_emptyStateLabel);
            _emptyStateLabel.BringToFront();
        }

        private void SetupBackupButton()
        {
            var btn = new Button
            {
                Text      = "Backup Now",
                Size      = btnCustomizeSlips.Size,
                Location  = new System.Drawing.Point(btnCustomizeSlips.Left - btnCustomizeSlips.Width - 6, btnCustomizeSlips.Top),
                BackColor = Color.LightSteelBlue,
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false,
                Anchor    = AnchorStyles.Top | AnchorStyles.Right,
            };
            btn.Click += BtnBackupNow_Click;
            this.Controls.Add(btn);
        }

        private void BtnBackupNow_Click(object sender, EventArgs e)
        {
            string defaultName = $"WeighbridgeData_backup_{DateTime.Now:yyyyMMdd}.db";
            using (var sfd = new SaveFileDialog
            {
                Title      = "Save Database Backup",
                FileName   = defaultName,
                Filter     = "SQLite Database (*.db)|*.db|All Files (*.*)|*.*",
                DefaultExt = "db"
            })
            {
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    if (DatabaseManager.PerformManualBackup(sfd.FileName))
                        MessageBox.Show($"Backup saved to:\n{sfd.FileName}", "Backup Complete",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private void SetupManageLookupsButton()
        {
            var btn = new Button
            {
                Text      = "Manage Lookups",
                Size      = btnCustomizeSlips.Size,
                Location  = new Point(btnCustomizeSlips.Left - btnCustomizeSlips.Width * 2 - 12, btnCustomizeSlips.Top),
                BackColor = Color.Gainsboro,
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false,
                Anchor    = AnchorStyles.Top | AnchorStyles.Right,
            };
            btn.Click += (s, e) => new LookupManagerForm().ShowDialog(this);
            this.Controls.Add(btn);
        }

        private void SetupTileDisplayButton()
        {
            var btn = new Button
            {
                Text      = "Tile Display",
                Size      = btnCustomizeSlips.Size,
                Location  = new Point(btnCustomizeSlips.Left - btnCustomizeSlips.Width * 3 - 18, btnCustomizeSlips.Top),
                BackColor = Color.Gainsboro,
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false,
                Anchor    = AnchorStyles.Top | AnchorStyles.Right,
            };
            btn.Click += (s, e) =>
            {
                using (var dlg = new TileDisplayForm())
                {
                    // Redraw the tiles immediately so the change is visible without a restart
                    if (dlg.ShowDialog(this) == DialogResult.OK)
                    {
                        DatabaseManager.LoadUnprintedSlipsToDashboard(this.flpSlips);
                        UpdateEmptyState();
                    }
                }
            };
            this.Controls.Add(btn);
        }

        private void SetupSummaryPanel()
        {
            const int panelH = 90;
            const int btnW   = 50;
            const int gap    = 8;

            // Use flpSlips.Top (already DPI-scaled by InitializeComponent) so the
            // summary always sits flush above the tiles regardless of display scaling.
            int panelTop  = flpSlips.Top - gap - panelH;
            int panelLeft = flpSlips.Left;
            int panelW    = flpSlips.Width;

            var panel = new Panel
            {
                Location  = new Point(panelLeft, panelTop),
                Size      = new Size(panelW, panelH),
                BackColor = Color.FromArgb(220, 235, 245),
                Anchor    = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            };

            var titleLabel = new Label
            {
                Text      = "TODAY'S SUMMARY",
                Font      = new Font("Arial", 9, FontStyle.Bold),
                Location  = new Point(10, 8),
                AutoSize  = true,
                ForeColor = Color.FromArgb(50, 80, 120),
            };

            _summaryStatsLabel = new Label
            {
                Text      = "Loads: —  |  Total Tons: —",
                Font      = new Font("Arial", 13, FontStyle.Bold),
                Location  = new Point(10, 28),
                AutoSize  = true,
                ForeColor = Color.FromArgb(20, 50, 90),
            };

            _summaryFieldLabel = new Label
            {
                Text      = "",
                Font      = new Font("Arial", 8, FontStyle.Bold),
                Location  = new Point(10, 62),
                AutoSize  = true,
                ForeColor = Color.FromArgb(50, 80, 120),
            };

            _summaryBreakdownLabel = new Label
            {
                Text      = "",
                Font      = new Font("Arial", 8, FontStyle.Regular),
                Location  = new Point(10, 62),
                AutoSize  = true,
                ForeColor = Color.FromArgb(30, 60, 100),
            };

            panel.Controls.Add(titleLabel);
            panel.Controls.Add(_summaryStatsLabel);
            panel.Controls.Add(_summaryFieldLabel);
            panel.Controls.Add(_summaryBreakdownLabel);

            this.Controls.Add(panel);
            panel.BringToFront();

            // ⚙ button lives on the FORM (not inside the panel) so it is never
            // clipped or hidden by the panel's bounds — anchored Top|Right so it
            // tracks the right edge as the window grows.
            var btnSettings = new Button
            {
                Text      = "⚙",
                Font      = new Font("Arial", 11, FontStyle.Bold),
                Size      = new Size(btnW, panelH),
                Location  = new Point(panelLeft + panelW + 2, panelTop),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(180, 210, 230),
                Cursor    = Cursors.Hand,
                Anchor    = AnchorStyles.Top | AnchorStyles.Right,
            };
            btnSettings.FlatAppearance.BorderSize = 0;
            btnSettings.Click += BtnSummarySettings_Click;

            this.Controls.Add(btnSettings);
            btnSettings.BringToFront();

            RefreshSummary();
        }

        private void RefreshSummary()
        {
            var (loads, totalTons) = DatabaseManager.GetDailySummary();
            _summaryStatsLabel.Text = $"Loads: {loads}  |  Total Tons: {totalTons:F3} t";

            string groupSlot = DatabaseManager.GetGlobalSetting("SummaryGroupByField", "Field1");
            var fieldConfigs  = DatabaseManager.GetActiveFieldConfigurations();
            string fieldLabel = fieldConfigs.ContainsKey(groupSlot)
                ? fieldConfigs[groupSlot].CustomName : groupSlot;

            var rows = DatabaseManager.GetDailyTonsByField(groupSlot);
            if (rows.Count == 0)
            {
                _summaryFieldLabel.Text     = $"By {fieldLabel}:";
                _summaryBreakdownLabel.Text = "";
                _summaryBreakdownLabel.Left = _summaryFieldLabel.Left + _summaryFieldLabel.Width + 6;
                _summaryBreakdownLabel.Top  = _summaryFieldLabel.Top;
                _summaryBreakdownLabel.Text = "No data yet";
            }
            else
            {
                var parts = new List<string>();
                foreach (var (value, tons, _) in rows)
                    parts.Add($"{value}: {tons:F3} t");

                _summaryFieldLabel.Text     = $"By {fieldLabel}:";
                _summaryBreakdownLabel.Left = _summaryFieldLabel.Left + _summaryFieldLabel.Width + 6;
                _summaryBreakdownLabel.Top  = _summaryFieldLabel.Top;
                _summaryBreakdownLabel.Text = string.Join("   |   ", parts);
            }
        }

        private void BtnSummarySettings_Click(object sender, EventArgs e)
        {
            var fieldConfigs = DatabaseManager.GetActiveFieldConfigurations();
            string current   = DatabaseManager.GetGlobalSetting("SummaryGroupByField", "Field1");

            using (var dlg = new Form())
            {
                dlg.Text            = "Summary Settings";
                dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                dlg.StartPosition   = FormStartPosition.CenterParent;
                dlg.ClientSize      = new Size(340, 110);
                dlg.MaximizeBox     = false;
                dlg.MinimizeBox     = false;

                var lbl = new Label
                {
                    Text     = "Show tons breakdown by:",
                    Location = new Point(12, 14),
                    AutoSize = true,
                    Font     = new Font("Arial", 9, FontStyle.Bold),
                };

                var combo = new ComboBox
                {
                    Location      = new Point(12, 36),
                    Size          = new Size(314, 24),
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    Font          = new Font("Arial", 9),
                };

                foreach (var kvp in fieldConfigs)
                    if (!kvp.Value.IsHidden && kvp.Key != "Field7")
                        combo.Items.Add(new { Slot = kvp.Key, Label = kvp.Value.CustomName });

                combo.DisplayMember = "Label";
                for (int i = 0; i < combo.Items.Count; i++)
                {
                    dynamic item = combo.Items[i];
                    if (item.Slot == current) { combo.SelectedIndex = i; break; }
                }
                if (combo.SelectedIndex < 0 && combo.Items.Count > 0)
                    combo.SelectedIndex = 0;

                var btnOk     = new Button { Text = "Save",   DialogResult = DialogResult.OK,
                                             Location = new Point(148, 72), Size = new Size(80, 28) };
                var btnCancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel,
                                             Location = new Point(246, 72), Size = new Size(80, 28) };

                dlg.Controls.AddRange(new Control[] { lbl, combo, btnOk, btnCancel });
                dlg.AcceptButton = btnOk;
                dlg.CancelButton = btnCancel;

                if (dlg.ShowDialog(this) == DialogResult.OK && combo.SelectedItem != null)
                {
                    dynamic selected = combo.SelectedItem;
                    DatabaseManager.SaveGlobalSetting("SummaryGroupByField", selected.Slot);
                    RefreshSummary();
                }
            }
        }

        private void UpdateEmptyState()
        {
            bool hasSlips = false;
            foreach (Control c in flpSlips.Controls)
                if (c is Button) { hasSlips = true; break; }
            _emptyStateLabel.Visible = !hasSlips;
        }

        private void Main_Load(object sender, EventArgs e) { }



        // Open the creation input screen when the user clicks btnCreate
        private void btnCreate_Click(object sender, EventArgs e)
        {
            CreateSlip popUp = new CreateSlip();
            popUp.ShowDialog();
        }

        private void btnOpenSettings_Click(object sender, EventArgs e)
        {
           PrinterSettingsForm options = new PrinterSettingsForm();
           options.ShowDialog();

        }

        private void btnSlipHistory_Click(object sender, EventArgs e)
        
        {
            // Opens your freshly unified, error-free history vault form
            SlipsHistoryForm historyPage = new SlipsHistoryForm();
            historyPage.ShowDialog();
        }

        

        private void btnExit_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void btnCustomizeSlips_Click(object sender, EventArgs e)
        {
            CustomizeSlipsForm options = new CustomizeSlipsForm();

            // Field labels feed the tile captions, so a rename has to redraw the dashboard.
            // Without this the tiles keep their old headings until something else happens to
            // rebuild them, which made a saved rename look like it had not taken effect.
            if (options.ShowDialog() == DialogResult.OK)
            {
                DatabaseManager.LoadUnprintedSlipsToDashboard(this.flpSlips);
                UpdateEmptyState();
                RefreshSummary();
            }
        }
    }
}
