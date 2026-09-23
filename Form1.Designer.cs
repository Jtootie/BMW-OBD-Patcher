using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace BMWIRomPatcher
{
    partial class BMWPatcherForm
    {
        private IContainer components = null;
        private Button btnAbout, btnLoadBin, btnPatchBin, btnPatchWatermarks, btnCrcCheck, btnSwsigStatusFix, btnSaveBin;
        private Label lblDetect;
        private TextBox loadedFile;
        private Button btnOriginal, btnTuned, btnConvert, btnRevert;
        private RichTextBox txtOutput;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        private Button MakeButton(string text, EventHandler action, bool enabled = false, bool primary = false)
        {
            var button = new Button
            {
                Text = text,
                Dock = DockStyle.Fill,
                Margin = new Padding(4),
                Enabled = enabled,
                FlatStyle = FlatStyle.Flat,
                BackColor = primary ? Color.FromArgb(30, 102, 154) : Color.FromArgb(247, 249, 251),
                ForeColor = primary ? Color.White : Color.FromArgb(45, 64, 78)
            };
            button.FlatAppearance.BorderColor = primary ? button.BackColor : Color.FromArgb(207, 216, 223);
            button.Click += action;
            return button;
        }

        private TableLayoutPanel Card(string heading, string description, int columns)
        {
            var card = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(14),
                Margin = new Padding(0, 0, 0, 10),
                ColumnCount = columns,
                RowCount = 3
            };
            for (int i = 0; i < columns; i++)
                card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / columns));
            card.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            card.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            card.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var title = new Label { Text = heading, AutoSize = true, Font = new Font(Font, FontStyle.Bold) };
            var detail = new Label { Text = description, Dock = DockStyle.Fill, AutoEllipsis = true };
            card.Controls.Add(title, 0, 0);
            card.SetColumnSpan(title, columns);
            card.Controls.Add(detail, 0, 1);
            card.SetColumnSpan(detail, columns);
            return card;
        }

        private void InitializeComponent()
        {
            SuspendLayout();
            Font = new Font("Segoe UI", 10F);
            BackColor = Color.FromArgb(240, 245, 248);
            ForeColor = Color.FromArgb(45, 64, 78);
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, Padding = new Padding(20, 12, 20, 12), ColumnCount = 1,
                RowCount = 7, MinimumSize = new Size(0, 680)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            foreach (int height in new[] { 55, 130, 138, 138 })
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
            layout.Controls.Add(new Label
            {
                Text = "BMW F/G Series OBD Unlock", Font = new Font(Font, FontStyle.Bold), BackColor = Color.White,
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(0, 0, 0, 8)
            }, 0, 0);

            var load = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(14),
                Margin = new Padding(0, 0, 0, 10), ColumnCount = 2, RowCount = 3
            };
            load.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            load.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
            load.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));
            load.RowStyles.Add(new RowStyle(SizeType.Absolute, 37));
            load.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            load.Controls.Add(new Label { Text = "BIN file", AutoSize = true, Font = new Font(Font, FontStyle.Bold) }, 0, 0);
            loadedFile = new TextBox { ReadOnly = true, Dock = DockStyle.Fill };
            load.Controls.Add(loadedFile, 0, 1);
            btnLoadBin = MakeButton("Load BIN...", BtnLoadBin_Click, true);
            load.Controls.Add(btnLoadBin, 1, 1);
            lblDetect = new Label { Text = "No file loaded", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, BackColor = Color.FromArgb(232, 247, 240) };
            load.Controls.Add(lblDetect, 0, 2);
            load.SetColumnSpan(lblDetect, 2);
            layout.Controls.Add(load, 0, 1);

            var patch = Card("BIN patching", "Load a supported BIN to enable the available patch actions. Save the result when finished.", 4);
            patch.RowCount = 3;
            patch.RowStyles.Clear();
            patch.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            patch.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            patch.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            patch.ColumnStyles.Clear();
            for (int i = 0; i < 4; i++)
                patch.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            btnPatchBin = MakeButton("Patch BIN", BtnPatchBin_Click);
            btnPatchWatermarks = MakeButton("Patch AT Watermarks", BtnPatchWatermarks_Click);
            btnCrcCheck = MakeButton("Check CRC", BtnCrcCheck_Click);
            btnSwsigStatusFix = MakeButton("SWSIGSTATUS Fix", BtnSwsigStatusFix_Click);
            patch.Controls.Add(btnPatchBin, 0, 2);
            patch.Controls.Add(btnPatchWatermarks, 1, 2);
            patch.Controls.Add(btnSwsigStatusFix, 2, 2);
            patch.Controls.Add(btnCrcCheck, 3, 2);
            layout.Controls.Add(patch, 0, 2);

            var conversion = Card("Gen1 conversion", "For Gen1 only. Load Original and Tuned BIN files before converting; Revert also uses the main loaded BIN.", 4);
            btnOriginal = MakeButton("Load Original BIN...", BtnOriginal_Click);
            btnTuned = MakeButton("Load Tuned BIN...", BtnTuned_Click);
            btnConvert = MakeButton("Convert...", BtnConvert_Click);
            btnRevert = MakeButton("Revert...", BtnRevert_Click);
            conversion.Controls.Add(btnOriginal, 0, 2);
            conversion.Controls.Add(btnTuned, 1, 2);
            conversion.Controls.Add(btnConvert, 2, 2);
            conversion.Controls.Add(btnRevert, 3, 2);
            layout.Controls.Add(conversion, 0, 3);

            var log = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(14), ColumnCount = 1, RowCount = 2, Margin = new Padding(0, 0, 0, 10) };
            log.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            log.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            log.Controls.Add(new Label { Text = "Activity log", AutoSize = true, Font = new Font(Font, FontStyle.Bold) }, 0, 0);
            txtOutput = new RichTextBox { Dock = DockStyle.Fill, ReadOnly = true, BackColor = Color.White, BorderStyle = BorderStyle.FixedSingle, ScrollBars = RichTextBoxScrollBars.Vertical };
            log.Controls.Add(txtOutput, 0, 1);
            layout.Controls.Add(log, 0, 4);

            var actions = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(10), ColumnCount = 3, RowCount = 1, Margin = Padding.Empty };
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
            btnAbout = MakeButton("About", BtnAbout_Click, true);
            btnSaveBin = MakeButton("Save BIN As...", BtnSaveBin_Click, false, true);
            actions.Controls.Add(btnSaveBin, 0, 0);
            actions.Controls.Add(new Label { Text = "Save BIN As recalculates and verifies the configured CRCs.", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true }, 1, 0);
            actions.Controls.Add(btnAbout, 2, 0);
            layout.Controls.Add(actions, 0, 5);
            layout.Controls.Add(new Label { Text = "V2.6 Freeware - do not pay for it!", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, Font = new Font("Segoe UI", 9F) }, 0, 6);
            scroll.Controls.Add(layout);
            Controls.Add(scroll);
            Name = "BMWPatcherForm";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "BMW F/G Series OBD Unlock";
            ClientSize = new Size(980, 820);
            MinimumSize = new Size(860, 780);
            ResumeLayout(false);
        }
    }
}
