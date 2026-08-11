using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace SlipManagement2
{
    // Lets the operator choose which slip fields appear on the Main Page tiles.
    //
    // Selection ORDER is preserved, not field order: ticking Truck Reg, then Client, then
    // Destination, then Tons produces a tile reading in exactly that sequence. That matters
    // because the tile is scanned at a glance while a truck waits, so the operator decides
    // what belongs at the top rather than the field numbering deciding for them.
    public class TileDisplayForm : Form
    {
        private readonly List<CheckBox>  _boxes    = new List<CheckBox>();
        private readonly List<string>    _selected = new List<string>();
        private readonly Dictionary<string, DatabaseManager.FieldLayoutSettings> _cfgs;

        private Label _countLabel;
        private Label _previewLabel;
        private bool  _loading;

        public TileDisplayForm()
        {
            Text            = "Tile Display";
            ClientSize      = new Size(560, 520);
            StartPosition   = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox     = false;
            MinimizeBox     = false;
            BackColor       = Color.White;

            _cfgs = DatabaseManager.GetActiveFieldConfigurations();
            _selected.AddRange(DatabaseManager.GetTileFieldSlots());

            BuildLayout();
            RefreshState();
        }

        private void BuildLayout()
        {
            Controls.Add(new Label
            {
                Text     = "Choose what appears on a slip tile",
                Font     = new Font("Arial", 14, FontStyle.Bold),
                Location = new Point(20, 16),
                AutoSize = true,
            });

            Controls.Add(new Label
            {
                Text      = "Pick up to " + DatabaseManager.MaxTileFields + " fields. They appear on the tile "
                          + "in the order you tick them, each shown as its current label followed by that "
                          + "slip's value. The slip number is always shown.",
                Font      = new Font("Arial", 9),
                ForeColor = Color.DimGray,
                Location  = new Point(20, 48),
                Size      = new Size(520, 44),
                AutoSize  = false,
            });

            var list = new Panel
            {
                Location    = new Point(20, 100),
                Size        = new Size(320, 300),
                AutoScroll  = true,
                BorderStyle = BorderStyle.Fixed3D,
                BackColor   = SystemColors.Window,
            };

            int y = 8;
            _loading = true;
            for (int i = 1; i <= 10; i++)
            {
                string slot = "Field" + i;
                if (!_cfgs.TryGetValue(slot, out var cfg)) continue;

                string label = string.IsNullOrWhiteSpace(cfg.CustomName)
                    ? "Field " + i : cfg.CustomName.TrimEnd(':').Trim();

                var box = new CheckBox
                {
                    Text     = slot + "  :  " + label + (cfg.IsHidden ? "   (hidden on the slip)" : ""),
                    Location = new Point(10, y),
                    AutoSize = true,
                    Font     = new Font("Arial", 9),
                    Tag      = slot,
                    Checked  = _selected.Contains(slot),
                };
                box.CheckedChanged += Box_CheckedChanged;
                _boxes.Add(box);
                list.Controls.Add(box);
                y += 26;
            }
            _loading = false;
            Controls.Add(list);

            _countLabel = new Label
            {
                Location  = new Point(20, 408),
                Size      = new Size(320, 20),
                Font      = new Font("Arial", 9, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 60, 110),
            };
            Controls.Add(_countLabel);

            // Live preview so the effect is obvious before saving
            Controls.Add(new Label
            {
                Text     = "Preview",
                Font     = new Font("Arial", 9, FontStyle.Bold),
                Location = new Point(360, 100),
                AutoSize = true,
            });

            _previewLabel = new Label
            {
                Location    = new Point(360, 122),
                Size        = new Size(180, 150),
                BackColor   = Color.LightYellow,
                BorderStyle = BorderStyle.FixedSingle,
                Font        = new Font("Arial", 9, FontStyle.Bold),
                TextAlign   = ContentAlignment.MiddleCenter,
            };
            Controls.Add(_previewLabel);

            var btnSave = new Button
            {
                Text      = "Save",
                Location  = new Point(340, 440),
                Size      = new Size(100, 34),
                BackColor = Color.PaleGreen,
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Arial", 9, FontStyle.Bold),
            };
            btnSave.Click += BtnSave_Click;
            Controls.Add(btnSave);

            var btnCancel = new Button
            {
                Text      = "Cancel",
                Location  = new Point(450, 440),
                Size      = new Size(90, 34),
                BackColor = Color.LightCoral,
                FlatStyle = FlatStyle.Flat,
            };
            btnCancel.Click += (s, e) => Close();
            Controls.Add(btnCancel);
        }

        private void Box_CheckedChanged(object sender, EventArgs e)
        {
            if (_loading) return;
            var box     = (CheckBox)sender;
            string slot = box.Tag.ToString();

            if (box.Checked)
            {
                if (_selected.Count >= DatabaseManager.MaxTileFields)
                {
                    MessageBox.Show(
                        "You can show at most " + DatabaseManager.MaxTileFields + " fields on a tile.\n\n" +
                        "Untick one first, then tick this field.",
                        "Limit Reached", MessageBoxButtons.OK, MessageBoxIcon.Information);

                    _loading = true;
                    box.Checked = false;      // revert without re-entering this handler
                    _loading = false;
                    return;
                }
                _selected.Add(slot);          // appended, so ticking order becomes tile order
            }
            else
            {
                _selected.Remove(slot);
            }
            RefreshState();
        }

        private void RefreshState()
        {
            _countLabel.Text = _selected.Count + " of " + DatabaseManager.MaxTileFields + " selected"
                             + (_selected.Count == 0 ? "  —  the tile will show only the slip number" : "");

            // Sample values so the preview reads like a real tile
            var sample = new Dictionary<string, string>();
            for (int i = 1; i <= 10; i++)
            {
                string slot = "Field" + i;
                _cfgs.TryGetValue(slot, out var cfg);
                sample[slot] = (cfg != null && !string.IsNullOrWhiteSpace(cfg.CustomName))
                    ? "(" + cfg.CustomName.TrimEnd(':').Trim() + ")"
                    : "(value)";
            }

            var sb = new System.Text.StringBuilder();
            sb.Append("Slip: 42");
            foreach (string slot in _selected)
            {
                if (!_cfgs.TryGetValue(slot, out var cfg)) continue;
                string label = string.IsNullOrWhiteSpace(cfg.CustomName)
                    ? "Field " + slot.Replace("Field", "")
                    : cfg.CustomName.TrimEnd(':').Trim();
                sb.AppendLine().Append(label).Append(": ").Append(sample[slot]);
            }
            _previewLabel.Text = sb.ToString();
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            DatabaseManager.SaveTileFieldSlots(_selected);
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
