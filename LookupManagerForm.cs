using System;
using System.Drawing;
using System.Windows.Forms;

namespace SlipManagement2
{
    public class LookupManagerForm : Form
    {
        private (string TableName, string DisplayName)[] _lists;

        private ComboBox _cboList;
        private ListBox  _lstValues;
        private TextBox  _txtNew;

        public LookupManagerForm()
        {
            Text          = "Manage Lookup Lists";
            Size          = new Size(430, 530);
            StartPosition = FormStartPosition.CenterParent;
            WindowState   = FormWindowState.Maximized;

            // Build the list from FieldConfig so it stays in sync with field renames
            var seen = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var built = new System.Collections.Generic.List<(string, string)>();
            foreach (var kvp in DatabaseManager.GetActiveFieldConfigurations())
            {
                string table = kvp.Value.LookupTable;
                if (!string.IsNullOrEmpty(table) && seen.Add(table))
                    built.Add((table, kvp.Value.CustomName));
            }
            _lists = built.ToArray();

            BuildUI();
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
            foreach (var list in _lists)
                _cboList.Items.Add(list.DisplayName);
            _cboList.SelectedIndex = 0;
            _cboList.SelectedIndexChanged += (s, e) => LoadList();

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

            var btnDelete = new Button
            {
                Text      = "Delete Selected",
                Location  = new Point(12, 342),
                Size      = new Size(180, 36),
                BackColor = Color.Tomato,
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Arial", 9, FontStyle.Bold),
                Anchor    = AnchorStyles.Bottom | AnchorStyles.Left,
            };
            btnDelete.Click += BtnDelete_Click;

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

            var btnAdd = new Button
            {
                Text      = "Add",
                Location  = new Point(292, 428),
                Size      = new Size(108, 28),
                BackColor = Color.LightGreen,
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Arial", 9, FontStyle.Bold),
                Anchor    = AnchorStyles.Bottom | AnchorStyles.Right,
            };
            btnAdd.Click += BtnAdd_Click;

            var btnClose = new Button
            {
                Text      = "Close",
                Location  = new Point(314, 342),
                Size      = new Size(86, 36),
                FlatStyle = FlatStyle.Flat,
                Anchor    = AnchorStyles.Bottom | AnchorStyles.Right,
            };
            btnClose.Click += (s, e) => Close();

            Controls.AddRange(new Control[] {
                lblSelect, _cboList,
                lblEntries, _lstValues,
                btnDelete, btnClose,
                sep, lblNew, _txtNew, btnAdd
            });

            LoadList();
        }

        private string SelectedTable => _lists[_cboList.SelectedIndex].TableName;

        private void LoadList()
        {
            _lstValues.Items.Clear();
            foreach (var v in DatabaseManager.GetLookupValues(SelectedTable))
                _lstValues.Items.Add(v);
        }

        private void BtnAdd_Click(object sender, EventArgs e)
        {
            string val = _txtNew.Text.Trim();
            if (string.IsNullOrEmpty(val)) return;
            DatabaseManager.SaveLookupValue(SelectedTable, val);
            _txtNew.Clear();
            LoadList();
            // Highlight the just-added item
            int idx = _lstValues.Items.IndexOf(val);
            if (idx >= 0) _lstValues.SelectedIndex = idx;
        }

        private void BtnDelete_Click(object sender, EventArgs e)
        {
            if (_lstValues.SelectedItem == null)
            {
                MessageBox.Show("Select an entry from the list first.",
                    "Nothing Selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string val = _lstValues.SelectedItem.ToString();
            string listName = _lists[_cboList.SelectedIndex].DisplayName;

            var confirm = MessageBox.Show(
                $"Delete \"{val}\" from {listName}?\n\nExisting slips that used this value are NOT affected.",
                "Confirm Delete",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (confirm != DialogResult.Yes) return;

            DatabaseManager.DeleteLookupValue(SelectedTable, val);
            LoadList();
        }
    }
}
