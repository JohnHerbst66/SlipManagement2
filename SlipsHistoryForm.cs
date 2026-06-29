using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using ClosedXML.Excel;

namespace SlipManagement2
{
    public partial class SlipsHistoryForm : Form
    {
        private string currentSelectedBilNumber = "";
        private bool _showVoided = false;

        private Label _gridSummaryLabel;

        private Dictionary<string, Label>    viewLabels             = new Dictionary<string, Label>();
        private Dictionary<string, TextBox>  editTextBoxes          = new Dictionary<string, TextBox>();
        private Dictionary<string, ComboBox> dynamicFilterDropdowns = new Dictionary<string, ComboBox>();
        private Dictionary<string, Label>    _filterLabels          = new Dictionary<string, Label>();
        private Dictionary<string, DatabaseManager.FieldLayoutSettings> _fieldConfigs;
        private bool _suppressSync = false;

        public SlipsHistoryForm()
        {
            InitializeComponent();
            this.Text        = "Slip History";
            this.MinimumSize = new Size(800, 500);

            _fieldConfigs = DatabaseManager.GetActiveFieldConfigurations();

            ApplyResizableLayout();
            SetupViewToggle();
            SetupTabPages();
            SetupDynamicFilterRow();

            // Keep filter boxes aligned when the form, columns, or scroll position change.
            // ColumnWidthChanged fires DURING DataSource reassignment with intermediate column states,
            // so we always defer via BeginInvoke and skip during _suppressSync (active typing).
            this.Resize += (s, e) => SyncFilterWidthsToColumns();
            dataGridView1.ColumnWidthChanged += (s, e) =>
            {
                if (this.IsHandleCreated && !_suppressSync)
                    this.BeginInvoke(new Action(SyncFilterWidthsToColumns));
            };
            dataGridView1.Scroll += (s, e) =>
            {
                if (((ScrollEventArgs)e).ScrollOrientation == ScrollOrientation.HorizontalScroll)
                    SyncFilterWidthsToColumns();
            };

            // After the handle is created, position filter boxes (BeginInvoke is skipped during constructor)
            this.Load += (s, e) => SyncFilterWidthsToColumns();

            dtpFromDate.ValueChanged += (s, e) => { LoadUniqueValuesIntoDropdowns(); ExecuteDynamicFilterQuery(); };
            dtpToDate.ValueChanged   += (s, e) => { LoadUniqueValuesIntoDropdowns(); ExecuteDynamicFilterQuery(); };

            LoadUniqueValuesIntoDropdowns();
            ExecuteDynamicFilterQuery();
        }

        // ===================================================================
        // LAYOUT — resizable, filter row above grid, small scrollable tabs
        // ===================================================================
        private void ApplyResizableLayout()
        {
            const int filterRowH  = 42;   // label (15px) + gap (2px) + combobox (19px) + gap (6px)
            const int tabPanelH   = 195;
            const int topBarH     = 68;   // space above panel1 (buttons toolbar)
            const int sidePad     = 12;

            this.Size = new Size(1150, 750);

            // Export button follows the right edge when the form widens
            btnExport.Anchor = AnchorStyles.Top | AnchorStyles.Right;

            // panel2 (tabs) — fixed height pinned to the bottom
            panel2.Left   = sidePad;
            panel2.Width  = this.ClientSize.Width - sidePad * 2;
            panel2.Height = tabPanelH;
            panel2.Top    = this.ClientSize.Height - tabPanelH - sidePad;
            panel2.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;

            // tabControl1 fills panel2; tab pages get scroll bars
            tabControl1.SetBounds(0, 0, panel2.Width, panel2.Height);
            tabControl1.Anchor = AnchorStyles.Top | AnchorStyles.Bottom
                               | AnchorStyles.Left | AnchorStyles.Right;
            foreach (TabPage tp in tabControl1.TabPages)
                tp.AutoScroll = true;

            // panel1 (date pickers + filter row + grid) — fills the space between toolbar and panel2
            panel1.Left   = sidePad;
            panel1.Top    = topBarH;
            panel1.Width  = this.ClientSize.Width - sidePad * 2;
            panel1.Height = panel2.Top - topBarH - sidePad;
            panel1.Anchor = AnchorStyles.Top | AnchorStyles.Bottom
                          | AnchorStyles.Left | AnchorStyles.Right;

            // DataGridView — fills panel1 below date pickers + single filter row, leaving room for summary
            const int summaryH = 22;
            int gridTop = dtpFromDate.Bottom + filterRowH + 2;
            dataGridView1.Left   = 0;
            dataGridView1.Top    = gridTop;
            dataGridView1.Width  = panel1.Width;
            dataGridView1.Height = panel1.Height - gridTop - summaryH - 2;
            dataGridView1.Anchor = AnchorStyles.Top | AnchorStyles.Bottom
                                 | AnchorStyles.Left | AnchorStyles.Right;
            dataGridView1.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            _gridSummaryLabel = new Label
            {
                Text      = "Total Slips: —   |   Total Tons: —",
                Font      = new Font("Arial", 9, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 60, 110),
                AutoSize  = false,
                Height    = summaryH,
                Left      = 0,
                Width     = panel1.Width,
                Anchor    = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                TextAlign = ContentAlignment.MiddleLeft,
            };
            // Position is set after grid is laid out; anchor keeps it stuck to the bottom
            _gridSummaryLabel.Top = panel1.Height - summaryH;
            panel1.Controls.Add(_gridSummaryLabel);
        }

        // ===================================================================
        // VIEW TOGGLE — Printed vs Voided
        // ===================================================================
        private void SetupViewToggle()
        {
            var gbView = new GroupBox
            {
                Text     = "View:",
                Location = new Point(550, 6),
                Size     = new Size(240, 52),
                Font     = new Font("Arial", 9, FontStyle.Bold),
                Anchor   = AnchorStyles.Top | AnchorStyles.Left
            };

            var radPrinted = new RadioButton { Text = "Printed Slips", Location = new Point(8, 20),   AutoSize = true, Checked = true, Font = new Font("Arial", 9) };
            var radVoided  = new RadioButton { Text = "Voided Slips",  Location = new Point(120, 20), AutoSize = true,                 Font = new Font("Arial", 9) };

            radPrinted.CheckedChanged += (s, e) =>
            {
                if (!radPrinted.Checked) return;
                _showVoided      = false;
                btnSave.Enabled  = true;
                btnPrint.Enabled = true;
                btnVoid.Enabled  = true;
                LoadUniqueValuesIntoDropdowns();
                ExecuteDynamicFilterQuery();
            };

            radVoided.CheckedChanged += (s, e) =>
            {
                if (!radVoided.Checked) return;
                _showVoided      = true;
                btnSave.Enabled  = false;
                btnPrint.Enabled = false;
                btnVoid.Enabled  = false;
                LoadUniqueValuesIntoDropdowns();
                ExecuteDynamicFilterQuery();
            };

            gbView.Controls.Add(radPrinted);
            gbView.Controls.Add(radVoided);
            this.Controls.Add(gbView);
            gbView.BringToFront();
        }

        // ===================================================================
        // 1. QUERY ENGINE
        // ===================================================================
        private void ExecuteDynamicFilterQuery()
        {
            string status  = _showVoided ? "Voided" : "Printed";
            var    filters = new Dictionary<string, string>();

            foreach (string key in new[] { "SlipID", "BillNumber",
                "Field1","Field2","Field3","Field4","Field5","Field6","Field7","Field8","Field9","Field10" })
            {
                if (!dynamicFilterDropdowns.ContainsKey(key)) continue;
                string text = dynamicFilterDropdowns[key].Text.Trim();
                if (!string.IsNullOrEmpty(text) && text != "[All Data]")
                    filters[key] = text;
            }

            RefreshDataGrid(DatabaseManager.QuerySlips(status, dtpFromDate.Value, dtpToDate.Value, filters));
        }

        // ===================================================================
        // 2. UNIQUE VALUES FOR FILTER DROPDOWNS
        // ===================================================================
        private void LoadUniqueValuesIntoDropdowns()
        {
            string status = _showVoided ? "Voided" : "Printed";

            foreach (var kvp in dynamicFilterDropdowns)
            {
                ComboBox cb           = kvp.Value;
                string   previousText = cb.Text;

                cb.Items.Clear();
                cb.Items.Add("[All Data]");

                foreach (string v in DatabaseManager.GetDistinctFieldValues(kvp.Key, status, dtpFromDate.Value, dtpToDate.Value))
                    cb.Items.Add(v);

                cb.Text = previousText;
            }
        }

        // ===================================================================
        // 3. DYNAMIC FILTER ROW — one row of labels + comboboxes above grid
        // ===================================================================
        private void SetupDynamicFilterRow()
        {
            // Y positions inside panel1, directly above dataGridView1
            int filterLabelY = dataGridView1.Top - 41;
            int filterComboY = dataGridView1.Top - 24;

            var fieldConfigs = DatabaseManager.GetActiveFieldConfigurations();

            for (int i = 1; i <= 10; i++)
            {
                string key = "Field" + i;
                if (!fieldConfigs.ContainsKey(key)) continue;
                var cfg = fieldConfigs[key];
                if (cfg.IsHidden) continue;

                string title = string.IsNullOrWhiteSpace(cfg.CustomName)
                    ? $"Field {i}" : cfg.CustomName.Replace(":", "").Trim();

                var lbl = new Label
                {
                    Text      = title + ":",
                    Location  = new Point(0, filterLabelY),
                    AutoSize  = false,
                    Size      = new Size(100, 15),
                    Font      = new Font("Arial", 7),
                    TextAlign = ContentAlignment.BottomLeft
                };
                var cb = new ComboBox
                {
                    Location           = new Point(0, filterComboY),
                    Size               = new Size(100, 19),
                    DropDownStyle      = ComboBoxStyle.DropDown,
                    AutoCompleteMode   = AutoCompleteMode.SuggestAppend,
                    AutoCompleteSource = AutoCompleteSource.ListItems,
                    Font               = new Font("Arial", 7)
                };
                cb.TextChanged += (s, e) => { _suppressSync = true; ExecuteDynamicFilterQuery(); _suppressSync = false; };

                _filterLabels.Add(key, lbl);
                dynamicFilterDropdowns.Add(key, cb);
                panel1.Controls.Add(lbl);
                panel1.Controls.Add(cb);
            }

            // SlipID filter
            var lblSid = new Label { Text = "Slip ID:", Location = new Point(0, filterLabelY),
                AutoSize = false, Size = new Size(100, 15), Font = new Font("Arial", 7),
                TextAlign = ContentAlignment.BottomLeft };
            var cbSid = new ComboBox { Location = new Point(0, filterComboY), Size = new Size(100, 19),
                DropDownStyle = ComboBoxStyle.DropDown, AutoCompleteMode = AutoCompleteMode.SuggestAppend,
                AutoCompleteSource = AutoCompleteSource.ListItems, Font = new Font("Arial", 7) };
            cbSid.TextChanged += (s, e) => { _suppressSync = true; ExecuteDynamicFilterQuery(); _suppressSync = false; };
            _filterLabels["SlipID"]          = lblSid;
            dynamicFilterDropdowns["SlipID"] = cbSid;
            panel1.Controls.Add(lblSid);
            panel1.Controls.Add(cbSid);

            // BillNumber filter
            var lblBn = new Label { Text = "Bill No:", Location = new Point(0, filterLabelY),
                AutoSize = false, Size = new Size(100, 15), Font = new Font("Arial", 7),
                TextAlign = ContentAlignment.BottomLeft };
            var cbBn = new ComboBox { Location = new Point(0, filterComboY), Size = new Size(100, 19),
                DropDownStyle = ComboBoxStyle.DropDown, AutoCompleteMode = AutoCompleteMode.SuggestAppend,
                AutoCompleteSource = AutoCompleteSource.ListItems, Font = new Font("Arial", 7) };
            cbBn.TextChanged += (s, e) => { _suppressSync = true; ExecuteDynamicFilterQuery(); _suppressSync = false; };
            _filterLabels["BillNumber"]          = lblBn;
            dynamicFilterDropdowns["BillNumber"] = cbBn;
            panel1.Controls.Add(lblBn);
            panel1.Controls.Add(cbBn);
        }

        // ===================================================================
        // 3b. SYNC filter box X positions and widths to DataGrid columns
        // ===================================================================
        private void SyncFilterWidthsToColumns()
        {
            if (dataGridView1.Columns.Count == 0) return;

            int filterLabelY = dataGridView1.Top - 41;
            int filterComboY = dataGridView1.Top - 24;

            // Starting X accounts for the row-header gutter and current scroll offset
            int accX = dataGridView1.Left
                     + dataGridView1.RowHeadersWidth
                     - dataGridView1.HorizontalScrollingOffset;

            foreach (DataGridViewColumn col in dataGridView1.Columns)
            {
                if (!col.Visible) continue;

                string key  = col.Name;
                int    colW = col.Width;

                if (_filterLabels.ContainsKey(key))
                {
                    _filterLabels[key].Left  = accX;
                    _filterLabels[key].Top   = filterLabelY;
                    _filterLabels[key].Width = colW;
                }
                if (dynamicFilterDropdowns.ContainsKey(key))
                {
                    dynamicFilterDropdowns[key].Left  = accX;
                    dynamicFilterDropdowns[key].Top   = filterComboY;
                    dynamicFilterDropdowns[key].Width = Math.Max(colW - 1, 20);
                }

                accX += colW;
            }
        }

        // ===================================================================
        // 4. GRID REFRESH
        // ===================================================================
        private void RefreshDataGrid(DataTable dt)
        {
            try
            {
                dataGridView1.DataSource    = dt;
                dataGridView1.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
                dataGridView1.MultiSelect   = false;

                var fieldConfigs = DatabaseManager.GetActiveFieldConfigurations();
                foreach (var cfg in fieldConfigs)
                {
                    if (!dataGridView1.Columns.Contains(cfg.Key)) continue;
                    if (cfg.Value.IsHidden)
                        dataGridView1.Columns[cfg.Key].Visible = false;
                    else
                    {
                        dataGridView1.Columns[cfg.Key].Visible    = true;
                        dataGridView1.Columns[cfg.Key].HeaderText = cfg.Value.CustomName;
                    }
                }

                if (dataGridView1.Columns.Contains("Status"))
                    dataGridView1.Columns["Status"].Visible = false;

                if (dataGridView1.Columns.Contains("VoidReason"))
                {
                    dataGridView1.Columns["VoidReason"].Visible    = _showVoided;
                    dataGridView1.Columns["VoidReason"].HeaderText = "Void Reason";
                }

                if (dataGridView1.Columns.Contains("PrintedAt"))
                    dataGridView1.Columns["PrintedAt"].Visible = !_showVoided;

                UpdateGridSummary(dt);

                // Re-align filter boxes after column widths are finalised.
                // Guard: BeginInvoke requires a live window handle (not available during constructor).
                if (this.IsHandleCreated && !_suppressSync)
                    this.BeginInvoke(new Action(SyncFilterWidthsToColumns));
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading history: " + ex.Message);
            }
        }

        private void UpdateGridSummary(DataTable dt)
        {
            if (_gridSummaryLabel == null) return;

            int slipCount = dt?.Rows.Count ?? 0;
            double totalTons = 0;
            if (dt != null && dt.Columns.Contains("Field7"))
            {
                foreach (DataRow row in dt.Rows)
                    if (double.TryParse(row["Field7"]?.ToString(),
                            System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture, out double t))
                        totalTons += t;
            }

            string tonsField = GetDisplayName("Field7");
            _gridSummaryLabel.Text = $"Total Slips: {slipCount}   |   Total {tonsField}: {totalTons:F3} t";
        }

        // ===================================================================
        // 5. ROW SELECTION — populate View + Edit tabs
        // ===================================================================
        private void dataGridView1_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;

            DataGridViewRow row = dataGridView1.Rows[e.RowIndex];
            currentSelectedBilNumber = row.Cells["BillNumber"].Value?.ToString() ?? "";

            viewLabels["SlipID"].Text     = "Slip ID: "     + (row.Cells["SlipID"].Value?.ToString() ?? "");
            viewLabels["BillNumber"].Text = "Bill Number: " + currentSelectedBilNumber;

            for (int i = 1; i <= 10; i++)
            {
                string key = "Field" + i;
                string val = row.Cells[key].Value?.ToString() ?? "";
                viewLabels[key].Text = GetDisplayName(key) + ": " + val;
                if (editTextBoxes.ContainsKey(key))
                    editTextBoxes[key].Text = val;
            }

            if (viewLabels.ContainsKey("VoidReason"))
            {
                string vr = dataGridView1.Columns.Contains("VoidReason")
                    ? row.Cells["VoidReason"].Value?.ToString() ?? ""
                    : "";
                viewLabels["VoidReason"].Text = "Void Reason: " + vr;
            }
        }

        // ===================================================================
        // 6. ACTION BUTTONS
        // ===================================================================
        private void btnSave_Click(object sender, EventArgs e)
        {
            if (_showVoided) return;

            if (string.IsNullOrEmpty(currentSelectedBilNumber))
            {
                MessageBox.Show("Please select a slip from the grid first.");
                return;
            }

            string[] fields = new string[10];
            for (int i = 1; i <= 10; i++)
                fields[i - 1] = editTextBoxes["Field" + i].Text;

            if (DatabaseManager.UpdatePrintedSlipFields(currentSelectedBilNumber, fields))
            {
                MessageBox.Show("Slip updated successfully.", "Saved");
                ExecuteDynamicFilterQuery();
            }
        }

        private void btnPrint_Click(object sender, EventArgs e)
        {
            if (_showVoided) return;

            if (dataGridView1.SelectedRows.Count == 0)
            {
                MessageBox.Show("Please select a row in the grid first.");
                return;
            }

            DataGridViewRow row = dataGridView1.SelectedRows[0];
            var pkg = new Dictionary<string, string>
            {
                { "SlipID",     row.Cells["SlipID"].Value?.ToString()     ?? "" },
                { "BillNumber", row.Cells["BillNumber"].Value?.ToString() ?? "" },
            };
            for (int i = 1; i <= 10; i++)
                pkg.Add("Field" + i, row.Cells["Field" + i].Value?.ToString() ?? "");

            using (var preview = new PrintSlipPreview(pkg))
                preview.ShowDialog(this);
        }

        private void btnVoid_Click(object sender, EventArgs e)
        {
            if (_showVoided) return;

            if (dataGridView1.SelectedRows.Count == 0)
            {
                MessageBox.Show("Please select a slip to void.", "Void Slip");
                return;
            }

            DataGridViewRow row    = dataGridView1.SelectedRows[0];
            string          slipId = row.Cells["SlipID"].Value?.ToString() ?? "";
            string          billNo = row.Cells["BillNumber"].Value?.ToString() ?? "";

            string reason = PromptForReason($"Void slip #{slipId} (Bill {billNo})?\n\nEnter a reason for voiding:");
            if (reason == null) return;

            if (string.IsNullOrWhiteSpace(reason))
            {
                MessageBox.Show("A reason is required to void a slip.", "Void Slip");
                return;
            }

            if (!int.TryParse(slipId, out int id)) return;

            if (DatabaseManager.VoidPrintedSlip(id, reason))
            {
                MessageBox.Show($"Slip #{slipId} has been voided.", "Voided", MessageBoxButtons.OK, MessageBoxIcon.Information);
                ExecuteDynamicFilterQuery();
            }
            else
            {
                MessageBox.Show("Slip could not be voided (it may have already been voided).", "Void Slip", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private static string PromptForReason(string prompt)
        {
            using (var dlg = new Form())
            {
                dlg.Text            = "Void Slip";
                dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                dlg.StartPosition   = FormStartPosition.CenterParent;
                dlg.ClientSize      = new Size(380, 140);
                dlg.MaximizeBox     = false;
                dlg.MinimizeBox     = false;

                var lbl = new Label  { Text = prompt, Location = new Point(12, 12), AutoSize = false,
                                       Size = new Size(356, 40), Font = new Font("Arial", 9) };
                var txt = new TextBox { Location = new Point(12, 58), Size = new Size(356, 22),
                                        Font = new Font("Arial", 9), MaxLength = 20 };
                var btnOk     = new Button { Text = "OK",     DialogResult = DialogResult.OK,
                                             Location = new Point(196, 100), Size = new Size(80, 28) };
                var btnCancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel,
                                             Location = new Point(288, 100), Size = new Size(80, 28) };

                dlg.Controls.AddRange(new Control[] { lbl, txt, btnOk, btnCancel });
                dlg.AcceptButton = btnOk;
                dlg.CancelButton = btnCancel;

                return dlg.ShowDialog() == DialogResult.OK ? txt.Text.Trim() : null;
            }
        }

        private void btnExport_Click(object sender, EventArgs e)
        {
            string label = _showVoided ? "Voided" : "Printed";
            using (var sfd = new SaveFileDialog
            {
                Filter   = "Excel Workbook|*.xlsx",
                FileName = $"Slips_{label}_Export_{DateTime.Now:yyyyMMdd}.xlsx"
            })
            {
                if (sfd.ShowDialog() != DialogResult.OK) return;
                try
                {
                    ExportToXlsx(sfd.FileName, label);
                    MessageBox.Show($"{label} slips exported successfully.\n\n{sfd.FileName}", "Export Saved",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Export failed: " + ex.Message, "Export Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void ExportToXlsx(string filePath, string statusLabel)
        {
            // Export exactly what the user sees: filtered rows and visible columns only
            var dt = dataGridView1.DataSource as DataTable;
            if (dt == null || dt.Rows.Count == 0)
            {
                MessageBox.Show("No data to export.", "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var visibleCols = new List<(string Key, string Header)>();
            foreach (DataGridViewColumn col in dataGridView1.Columns)
            {
                if (col.Visible)
                    visibleCols.Add((col.Name, col.HeaderText));
            }

            using (var wb = new XLWorkbook())
            {
                var ws = wb.Worksheets.Add($"{statusLabel} Slips");

                for (int c = 1; c <= visibleCols.Count; c++)
                {
                    var cell = ws.Cell(1, c);
                    cell.Value                      = visibleCols[c - 1].Header;
                    cell.Style.Font.Bold            = true;
                    cell.Style.Fill.BackgroundColor = XLColor.LightGray;
                    cell.Style.Border.BottomBorder  = XLBorderStyleValues.Thin;
                }

                for (int row = 0; row < dt.Rows.Count; row++)
                {
                    DataRow dr = dt.Rows[row];
                    for (int c = 1; c <= visibleCols.Count; c++)
                    {
                        string key = visibleCols[c - 1].Key;
                        ws.Cell(row + 2, c).Value = dt.Columns.Contains(key) ? dr[key]?.ToString() ?? "" : "";
                    }
                }

                ws.Columns().AdjustToContents();
                ws.SheetView.FreezeRows(1);
                wb.SaveAs(filePath);
            }
        }

        private void btnExit_Click(object sender, EventArgs e) => this.Close();

        // ===================================================================
        // 7. TAB PAGE SETUP — View (read-only) + Edit, smaller font
        // ===================================================================
        private void SetupTabPages()
        {
            string[] viewKeys = { "SlipID", "BillNumber", "Field1", "Field2", "Field3", "Field4",
                                  "Field5", "Field6", "Field7", "Field8", "Field9", "Field10", "VoidReason" };
            string[] editKeys = { "Field1", "Field2", "Field3", "Field4", "Field5",
                                  "Field6", "Field7", "Field8", "Field9", "Field10" };

            var smallFont = new Font("Arial", 8);

            int yPos  = 8;
            TabPage tab1 = tabControl1.TabPages[0];
            foreach (var key in viewKeys)
            {
                var lbl = new Label { Text = GetDisplayName(key) + ": ----", Location = new Point(10, yPos), AutoSize = true, Font = smallFont };
                viewLabels.Add(key, lbl);
                tab1.Controls.Add(lbl);
                yPos += 19;
            }

            yPos = 8;
            TabPage tab2 = tabControl1.TabPages[1];
            foreach (var key in editKeys)
            {
                var lblDesc = new Label  { Text = GetDisplayName(key) + ":", Location = new Point(10, yPos), AutoSize = true, Font = smallFont };
                var txt     = new TextBox { Location = new Point(120, yPos - 2), Size = new Size(200, 19), Font = smallFont };
                editTextBoxes.Add(key, txt);
                tab2.Controls.Add(lblDesc);
                tab2.Controls.Add(txt);
                yPos += 22;
            }
        }

        private string GetDisplayName(string key)
        {
            switch (key)
            {
                case "SlipID":     return "Slip ID";
                case "BillNumber": return "Bill Number";
                case "VoidReason": return "Void Reason";
            }
            if (_fieldConfigs != null && _fieldConfigs.TryGetValue(key, out var cfg)
                && !string.IsNullOrWhiteSpace(cfg.CustomName))
                return cfg.CustomName.TrimEnd(':').Trim();
            return "Field " + key.Replace("Field", "");
        }

        // Designer-wired event stubs (logic handled by lambda wires above)
        private void dtpFromDate_ValueChanged(object sender, EventArgs e) { }
        private void dtpToDate_ValueChanged(object sender, EventArgs e) { }
        private void btnCancel_Click(object sender, EventArgs e) => this.Close();
    }
}
