using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace SlipManagement2
{
    // Puts a backup back in place. Deliberately blunt about what will be lost, because the
    // operator reaching for this screen is usually having a bad morning and should not have to
    // work out the consequences for themselves.
    public class RestoreBackupForm : Form
    {
        private List<DatabaseManager.BackupInfo> _backups;
        private ListView _list;
        private Button   _btnRestore;
        private Label    _lblCurrent;

        public RestoreBackupForm()
        {
            Text            = "Restore From Backup";
            ClientSize      = new Size(720, 520);
            StartPosition   = FormStartPosition.CenterParent;
            MinimizeBox     = false;
            MaximizeBox     = false;

            BuildUI();
            LoadBackups();
        }

        private void BuildUI()
        {
            Controls.Add(new Label
            {
                Text     = "Restore the slip database from an earlier copy",
                Location = new Point(16, 14),
                AutoSize = true,
                Font     = new Font("Arial", 12, FontStyle.Bold),
            });

            Controls.Add(new Label
            {
                Text      = "Use this only if the database is damaged, or the program will not open your slips.\r\n"
                          + "Restoring rewinds the records to the moment the chosen copy was taken. Anything "
                          + "entered after that time will not be in the restored database.",
                Location  = new Point(16, 44),
                Size      = new Size(690, 50),
                AutoSize  = false,
                Font      = new Font("Arial", 9),
                ForeColor = Color.FromArgb(150, 40, 40),
                Anchor    = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            });

            _lblCurrent = new Label
            {
                Location = new Point(16, 100),
                Size     = new Size(690, 20),
                AutoSize = false,
                Font     = new Font("Arial", 9, FontStyle.Bold),
                Anchor   = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            };
            Controls.Add(_lblCurrent);

            _list = new ListView
            {
                Location      = new Point(16, 128),
                Size          = new Size(690, 300),
                View          = View.Details,
                FullRowSelect = true,
                MultiSelect   = false,
                HideSelection = false,
                GridLines     = true,
                Font          = new Font("Arial", 9),
                Anchor        = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            };
            _list.Columns.Add("Taken",     170);
            _list.Columns.Add("Age",       120);
            _list.Columns.Add("Slips",      70);
            _list.Columns.Add("Printed",    70);
            _list.Columns.Add("Size",       80);
            _list.Columns.Add("Condition", 160);
            _list.SelectedIndexChanged += (s, e) => UpdateButtonState();
            Controls.Add(_list);

            _btnRestore = new Button
            {
                Text      = "Restore Selected",
                Location  = new Point(16, 444),
                Size      = new Size(180, 40),
                BackColor = Color.Khaki,
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Arial", 10, FontStyle.Bold),
                Enabled   = false,
                Anchor    = AnchorStyles.Bottom | AnchorStyles.Left,
            };
            _btnRestore.Click += BtnRestore_Click;
            Controls.Add(_btnRestore);

            var btnFolder = new Button
            {
                Text      = "Open Backup Folder",
                Location  = new Point(206, 444),
                Size      = new Size(170, 40),
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Arial", 9),
                Anchor    = AnchorStyles.Bottom | AnchorStyles.Left,
            };
            btnFolder.Click += BtnFolder_Click;
            Controls.Add(btnFolder);

            var btnClose = new Button
            {
                Text      = "Close",
                Location  = new Point(596, 444),
                Size      = new Size(110, 40),
                FlatStyle = FlatStyle.Flat,
                Anchor    = AnchorStyles.Bottom | AnchorStyles.Right,
            };
            btnClose.Click += (s, e) => Close();
            Controls.Add(btnClose);
        }

        private void BtnFolder_Click(object sender, EventArgs e)
        {
            try { System.Diagnostics.Process.Start("explorer.exe", DatabaseManager.BackupFolder); }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Could not open the folder"); }
        }

        private void LoadBackups()
        {
            _backups = DatabaseManager.GetAvailableBackups();
            _list.Items.Clear();

            foreach (var b in _backups)
            {
                var it = new ListViewItem(b.TakenAt.ToString("yyyy-MM-dd  HH:mm:ss"));
                it.SubItems.Add(DescribeAge(DateTime.Now - b.TakenAt));
                it.SubItems.Add(b.IsUsable ? b.SlipCount.ToString()    : "-");
                it.SubItems.Add(b.IsUsable ? b.PrintedCount.ToString() : "-");
                it.SubItems.Add((b.SizeBytes / 1024) + " KB");
                it.SubItems.Add(b.IsUsable ? "Readable" : "DAMAGED - cannot be used");
                if (!b.IsUsable) it.ForeColor = Color.Firebrick;
                _list.Items.Add(it);
            }

            if (_list.Items.Count == 0)
            {
                _lblCurrent.Text = "There are no backups yet.";
                return;
            }

            _list.Items[0].Selected = true;
            UpdateButtonState();
        }

        private void UpdateButtonState()
        {
            _btnRestore.Enabled = _list.SelectedIndices.Count == 1
                               && _backups[_list.SelectedIndices[0]].IsUsable;

            if (_list.SelectedIndices.Count != 1) { _lblCurrent.Text = ""; return; }

            var b = _backups[_list.SelectedIndices[0]];
            _lblCurrent.Text = b.IsUsable
                ? "Selected copy holds " + b.SlipCount + " slip(s), taken " + DescribeAge(DateTime.Now - b.TakenAt) + "."
                : "This copy is damaged and cannot be restored. Choose an earlier one.";
        }

        private static string DescribeAge(TimeSpan age)
        {
            if (age.TotalMinutes <  1) return "moments ago";
            if (age.TotalMinutes < 60) return (int)age.TotalMinutes + " minutes ago";
            if (age.TotalHours   < 24) return (int)age.TotalHours   + " hours ago";
            return (int)age.TotalDays + " days ago";
        }

        private void BtnRestore_Click(object sender, EventArgs e)
        {
            if (_list.SelectedIndices.Count != 1) return;
            var b = _backups[_list.SelectedIndices[0]];
            if (!b.IsUsable) return;

            string when = b.TakenAt.ToString("yyyy-MM-dd") + " at " + b.TakenAt.ToString("HH:mm");

            var confirm = MessageBox.Show(
                "Restore the database as it was on " + when + "?\r\n\r\n" +
                "That copy holds " + b.SlipCount + " slip(s), " + b.PrintedCount + " of them printed.\r\n\r\n" +
                "Any slip entered after that time will NOT be in the restored database.\r\n\r\n" +
                "The database you have now will not be deleted. It is kept alongside so it can still " +
                "be examined, or put back, if this turns out to be the wrong copy.\r\n\r\n" +
                "The program closes once this is done. Start it again as normal.",
                "Confirm Restore", MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);

            if (confirm != DialogResult.Yes) return;

            string error;
            string setAside = DatabaseManager.RestoreFromBackup(b.FilePath, out error);

            if (setAside == null)
            {
                MessageBox.Show(
                    "The restore did not go ahead.\r\n\r\n" + error + "\r\n\r\n" +
                    "Your database has not been changed.",
                    "Restore Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string kept = string.IsNullOrEmpty(setAside)
                ? ""
                : "\r\n\r\nThe database you had before was kept as:\r\n" +
                  System.IO.Path.GetFileName(setAside);

            MessageBox.Show(
                "Restored to the copy taken on " + when + "." + kept +
                "\r\n\r\nThe program will now close. Start it again to carry on.",
                "Restore Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);

            // Close rather than carry on. Every open form is showing data from the database that
            // has just been swapped out, and refreshing them all correctly is far more places to
            // get it wrong than simply starting again from a known state.
            Application.Exit();
        }
    }
}
