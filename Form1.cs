
using System;
using System.Drawing;
using System.Windows.Forms;

namespace SlipManagement2 // ⚠️ Change this to match your exact project namespace!
{
    public partial class Main : Form
    {
        private Label _emptyStateLabel;

        public Main()
        {
            InitializeComponent();
            DatabaseManager.InitializeDatabase();

            // Hide the old static header label — the empty state label replaces it
            lblSlipsPanel.Visible = false;

            SetupEmptyState();
            SetupManageLookupsButton();
            SetupBackupButton();

            DatabaseManager.LoadUnprintedSlipsToDashboard(this.flpSlips);

            // Keep empty state in sync whenever tiles are added or removed
            flpSlips.ControlAdded   += (s, e) => UpdateEmptyState();
            flpSlips.ControlRemoved += (s, e) => UpdateEmptyState();
            UpdateEmptyState();
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
                Enabled   = false    // mouse clicks pass through to flpSlips
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
                UseVisualStyleBackColor = false
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
                UseVisualStyleBackColor = false
            };
            btn.Click += (s, e) => new LookupManagerForm().ShowDialog(this);
            this.Controls.Add(btn);
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
            options.ShowDialog();
        }
    }
}
