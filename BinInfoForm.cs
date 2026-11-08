using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace BMWIRomPatcher
{
    internal sealed class BinInfoForm : Form
    {
        public BinInfoForm(BinMetadataReport report)
        {
            Text = "BIN Info";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(1040, 720);
            MinimumSize = new Size(700, 480);
            Font = new Font("Segoe UI", 10F);
            AutoScaleMode = AutoScaleMode.Dpi;
            ShowInTaskbar = false;
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 1, RowCount = 4 };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
            layout.Controls.Add(new Label { Text = "BIN information — current loaded data", Dock = DockStyle.Fill, Font = new Font(Font, FontStyle.Bold) }, 0, 0);
            var grid = new DataGridView
            {
                Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false, RowHeadersVisible = false, AutoGenerateColumns = false,
                AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells, BackgroundColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle, SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                ColumnHeadersHeight = 38,
                ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableAlwaysIncludeHeaderText
            };
            grid.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            grid.DefaultCellStyle.Padding = new Padding(4);
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Field", DataPropertyName = "Field", Width = 215, SortMode = DataGridViewColumnSortMode.NotSortable });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Value", DataPropertyName = "Value", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, MinimumWidth = 200, SortMode = DataGridViewColumnSortMode.NotSortable });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Source", DataPropertyName = "Source", Width = 245, SortMode = DataGridViewColumnSortMode.NotSortable });
            grid.DataSource = report.Rows;
            layout.Controls.Add(grid, 0, 1);
            layout.Controls.Add(new TextBox { Text = BinMetadataReport.Notice, Multiline = true, ReadOnly = true, BorderStyle = BorderStyle.None, BackColor = SystemColors.Control, Dock = DockStyle.Fill, Height = 90, ScrollBars = ScrollBars.Vertical, Margin = new Padding(0, 10, 0, 4) }, 0, 2);
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            var close = new Button { Text = "Close", DialogResult = DialogResult.OK, AutoSize = true, Height = 34 };
            var save = new Button { Text = "Save report…", AutoSize = true, Height = 34 };
            var copy = new Button { Text = "Copy report", AutoSize = true, Height = 34 };
            copy.Click += (s, e) => { try { Clipboard.SetText(report.ToText()); } catch (Exception ex) { MessageBox.Show(this, "Could not copy report: " + ex.Message); } };
            save.Click += (s, e) =>
            {
                using (var dialog = new SaveFileDialog { Filter = "Text report (*.txt)|*.txt", DefaultExt = "txt", FileName = "BIN-info.txt" })
                    if (dialog.ShowDialog(this) == DialogResult.OK)
                        try { File.WriteAllText(dialog.FileName, report.ToText()); } catch (Exception ex) { MessageBox.Show(this, "Could not save report: " + ex.Message); }
            };
            buttons.Controls.Add(close); buttons.Controls.Add(save); buttons.Controls.Add(copy);
            layout.Controls.Add(buttons, 0, 3);
            Controls.Add(layout);
            CancelButton = close;
        }
    }
}
