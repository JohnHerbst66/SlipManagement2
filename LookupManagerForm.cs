using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace SlipManagement2
{
    public class LookupManagerForm : Form
    {
        // Every list that exists, not the field list. A list is created by use and outlives the
        // label that made it, so this can include names no field carries any more (DEF-030).
        private List<DatabaseManager.LookupListInfo> _lists;

        private ComboBox _cboList;
        private ListBox  _lstValues;
        private TextBox  _txtNew;
        private Label    _lblEmpty;

        private Button _btnDeleteEntry, _btnClearList, _btnDeleteList, _btnAdd;

        public LookupManagerForm()
        {
            Text          = "Manage Lookup Lists";
            Size          = new Size(430, 570);
            StartPosition = FormStartPosition.CenterParent;
            WindowState   = FormWindowState.Maximized;

            BuildUI();
            ReloadLists(null);
        }

        private void BuildUI()
        {
            var lblSelect = new Label
            {
                Text     = "Select list:",
                Location = new Point(12, 14),
                AutoSize = true,
                Font     = new Font("Arial", 9, FontStyle.Bold)
            };

            _cboList = new ComboBox
            {
                Location      = new Point(12, 34),
                Size          = new Size(388, 24),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font          = new Font("Arial", 10),
                Anchor        = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            };
            // Subscribed inside ReloadLists, which detaches it while repopulating the items so
            // clearing the box does not fire a lookup against a stale index.

            var lblEntries = new Label
            {
                Text     = "Current entries — select one to delete:",
                Location = new Point(12, 72),
                AutoSize = true,
                Font     = new Font("Arial", 9, FontStyle.Bold)
            };

            _lstValues = new ListBox
            {
                Location = new Point(12, 92),
                Size     = new Size(388, 240),
                Font     = new Font("Arial", 10),
                Sorted   = true,
                Anchor   = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            };

            // Shown instead of the controls on a database where nothing has been entered yet.
            // Lists are built from use, so an empty screen here is normal rather than a fault.
            _lblEmpty = new Label
            {
                Text      = "No suggestion lists yet.\r\n\r\n"
                          + "A list is created the first time a value is saved on a slip, "
                          + "and is named after the field's label at that moment. Enter and "
                          + "save a slip, then come back here.",
                Location  = new Point(16, 96),
                Size      = new Size(380, 120),
                Font      = new Font("Arial", 9),
                ForeColor = Color.FromArgb(90, 90, 90),
                Visible   = false,
                Anchor    = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            };

            _btnDeleteEntry = new Button
            {
                Text      = "Delete Selected",
                Location  = new Point(12, 342),
                Size      = new Size(128, 36),
                BackColor = Color.Tomato,
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Arial", 9, FontStyle.Bold),
                Anchor    = AnchorStyles.Bottom | AnchorStyles.Left,
            };
            _btnDeleteEntry.Click += BtnDeleteEntry_Click;

            _btnClearList = new Button
            {
                Text      = "Clear List",
                Location  = new Point(148, 342),
                Size      = new Size(118, 36),
                BackColor = Color.Khaki,
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Arial", 9, FontStyle.Bold),
                Anchor    = AnchorStyles.Bottom | AnchorStyles.Left,
            };
            _btnClearList.Click += BtnClearList_Click;

            _btnDeleteList = new Button
            {
                Text      = "Delete List",
                Location  = new Point(274, 342),
                Size      = new Size(126, 36),
                BackColor = Color.IndianRed,
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Arial", 9, FontStyle.Bold),
                Anchor    = AnchorStyles.Bottom | AnchorStyles.Left,
            };
            _btnDeleteList.Click += BtnDeleteList_Click;

            var sep = new Label
            {
                Text      = "──────────────────────────────────────",
                Location  = new Point(12, 390),
                AutoSize  = true,
                ForeColor = Color.Silver,
                Anchor    = AnchorStyles.Bottom | AnchorStyles.Left,
            };

            var lblNew = new Label
            {
                Text     = "Add new entry:",
                Location = new Point(12, 410),
                AutoSize = true,
                Font     = new Font("Arial", 9, FontStyle.Bold),
                Anchor   = AnchorStyles.Bottom | AnchorStyles.Left,
            };

            _txtNew = new TextBox
            {
                Location = new Point(12, 430),
                Size     = new Size(270, 24),
                Font     = new Font("Arial", 10),
                Anchor   = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            };
            _txtNew.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) BtnAdd_Click(s, e); };

            _btnAdd = new Button
            {
                Text      = "Add",
                Location  = new Point(292, 428),
                Size      = new Size(108, 28),
                BackColor = Color.LightGreen,
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Arial", 9, FontStyle.Bold),
                Anchor    = AnchorStyles.Bottom | AnchorStyles.Right,
            };
            _btnAdd.Click += BtnAdd_Click;

            var btnClose = new Button
            {
                Text      = "Close",
                Location  = new Point(292, 470),
                Size      = new Size(108, 32),
                FlatStyle = FlatStyle.Flat,
                Anchor    = AnchorStyles.Bottom | AnchorStyles.Right,
            };
            btnClose.Click += (s, e) => Close();

            Controls.AddRange(new Control[] {
                lblSelect, _cboList,
                lblEntries, _lstValues, _lblEmpty,
                _btnDeleteEntry, _btnClearList, _btnDeleteList,
                sep, lblNew, _txtNew, _btnAdd, btnClose
            });
        }

        private string SelectedList =>
            (_cboList.SelectedIndex >= 0 && _cboList.SelectedIndex < _lists.Count)
                ? _lists[_cboList.SelectedIndex].Name
                : null;

        // Rebuilds the selector from the database, keeping the caller's list selected where it
        // still exists. Entry counts are part of the display text, so this runs after any edit.
        private void ReloadLists(string keepSelected)
        {
            _lists = DatabaseManager.GetAllLookupLists();

            _cboList.SelectedIndexChanged -= CboChanged;
            _cboList.Items.Clear();
            foreach (var l in _lists)
            {
                string suffix = l.InUse ? "" : "   —  no field uses this label";
                _cboList.Items.Add(l.Name + "   (" + l.Count + (l.Count == 1 ? " entry)" : " entries)") + suffix);
            }
            _cboList.SelectedIndexChanged += CboChanged;

            bool any = _lists.Count > 0;
            _cboList.Enabled        = any;
            _lstValues.Visible      = any;
            _lblEmpty.Visible       = !any;
            _btnDeleteEntry.Enabled = any;
            _btnClearList.Enabled   = any;
            _btnDeleteList.Enabled  = any;
            _txtNew.Enabled         = any;
            _btnAdd.Enabled         = any;

            if (!any) { _lstValues.Items.Clear(); return; }

            int idx = 0;
            if (!string.IsNullOrEmpty(keepSelected))
                for (int i = 0; i < _lists.Count; i++)
                    if (string.Equals(_lists[i].Name, keepSelected, StringComparison.OrdinalIgnoreCase))
                    { idx = i; break; }

            _cboList.SelectedIndex = idx;
            LoadValues();
        }

        private void CboChanged(object sender, EventArgs e) => LoadValues();

        private void LoadValues()
        {
            _lstValues.Items.Clear();
            string name = SelectedList;
            if (name == null) return;
            foreach (var v in DatabaseManager.GetLookupValues(name))
                _lstValues.Items.Add(v);
        }

        private void BtnAdd_Click(object sender, EventArgs e)
        {
            string name = SelectedList;
            if (name == null) return;

            string val = _txtNew.Text.Trim();
            if (string.IsNullOrEmpty(val)) return;

            DatabaseManager.SaveLookupValue(name, val);
            _txtNew.Clear();
            ReloadLists(name);

            int idx = _lstValues.Items.IndexOf(val);
            if (idx >= 0) _lstValues.SelectedIndex = idx;
        }

        private void BtnDeleteEntry_Click(object sender, EventArgs e)
        {
            string name = SelectedList;
            if (name == null) return;

            if (_lstValues.SelectedItem == null)
            {
                MessageBox.Show("Select an entry from the list first.",
                    "Nothing Selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string val = _lstValues.SelectedItem.ToString();

            var confirm = MessageBox.Show(
                $"Delete \"{val}\" from {name}?\n\nExisting slips that used this value are NOT affected.",
                "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (confirm != DialogResult.Yes) return;

            DatabaseManager.DeleteLookupValue(name, val);
            ReloadLists(name);
        }

        private void BtnClearList_Click(object sender, EventArgs e)
        {
            string name = SelectedList;
            if (name == null) return;

            int count = _lstValues.Items.Count;
            if (count == 0)
            {
                MessageBox.Show($"{name} is already empty.",
                    "Nothing to Clear", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var confirm = MessageBox.Show(
                $"Remove all {count} entries from {name}?\n\n" +
                "The list itself stays, and starts filling again from the next slip saved " +
                "against this field. Existing slips are NOT affected.",
                "Confirm Clear", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (confirm != DialogResult.Yes) return;

            DatabaseManager.ClearLookupList(name);
            ReloadLists(name);
        }

        private void BtnDeleteList_Click(object sender, EventArgs e)
        {
            string name = SelectedList;
            if (name == null) return;

            bool inUse = _lists[_cboList.SelectedIndex].InUse;
            string note = inUse
                ? "A field still carries this label, so the list will reappear — empty — the " +
                  "next time a slip is saved against it."
                : "No field carries this label any more, so the list will not come back unless " +
                  "a field is renamed to it again.";

            var confirm = MessageBox.Show(
                $"Delete the list {name} and all {_lstValues.Items.Count} of its entries?\n\n" +
                note + "\n\nExisting slips are NOT affected — a slip stores the value that was " +
                "entered, never a reference to a list.",
                "Confirm Delete List", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (confirm != DialogResult.Yes) return;

            DatabaseManager.DeleteLookupList(name);
            ReloadLists(null);
        }
    }
}
