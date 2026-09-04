using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace BMWIRomPatcher
{
    partial class BMWPatcherForm
    {
        private IContainer components = null;

        private Button btnAbout;
        private Label lblFooter;
        private Button btnLoadBin;
        private Label lblDetect;
        private Button btnPatchBin;
        private Button btnPatchWatermarks;
        private Button btnSwsigStatusFix;
        private Button btnSaveBin;
        private Label lblConvertHeader;
        private Panel panelGen1;
        private Button btnOriginal;
        private Button btnTuned;
        private Button btnConvert;
        private Button btnRevert;
        private RichTextBox txtOutput;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.btnAbout = new System.Windows.Forms.Button();
            this.lblFooter = new System.Windows.Forms.Label();
            this.btnLoadBin = new System.Windows.Forms.Button();
            this.lblDetect = new System.Windows.Forms.Label();
            this.btnPatchBin = new System.Windows.Forms.Button();
            this.btnPatchWatermarks = new System.Windows.Forms.Button();
            this.btnSwsigStatusFix = new System.Windows.Forms.Button();
            this.btnSaveBin = new System.Windows.Forms.Button();
            this.lblConvertHeader = new System.Windows.Forms.Label();
            this.panelGen1 = new System.Windows.Forms.Panel();
            this.btnOriginal = new System.Windows.Forms.Button();
            this.btnTuned = new System.Windows.Forms.Button();
            this.btnConvert = new System.Windows.Forms.Button();
            this.btnRevert = new System.Windows.Forms.Button();
            this.txtOutput = new System.Windows.Forms.RichTextBox();
            this.panelGen1.SuspendLayout();
            this.SuspendLayout();
            // 
            // btnAbout
            // 
            this.btnAbout.Location = new System.Drawing.Point(10, 370);
            this.btnAbout.Name = "btnAbout";
            this.btnAbout.Size = new System.Drawing.Size(120, 23);
            this.btnAbout.TabIndex = 6;
            this.btnAbout.Text = "About";
            this.btnAbout.Click += new System.EventHandler(this.BtnAbout_Click);
            // 
            // lblFooter
            // 
            this.lblFooter.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.lblFooter.Font = new System.Drawing.Font("Arial", 10F, System.Drawing.FontStyle.Bold);
            this.lblFooter.ForeColor = System.Drawing.Color.Red;
            this.lblFooter.Location = new System.Drawing.Point(0, 370);
            this.lblFooter.Name = "lblFooter";
            this.lblFooter.Size = new System.Drawing.Size(638, 30);
            this.lblFooter.TabIndex = 8;
            this.lblFooter.Text = "THIS PROGRAM IS FREEWARE, DO NOT PAY FOR IT!";
            this.lblFooter.TextAlign = System.Drawing.ContentAlignment.BottomRight;
            // 
            // btnLoadBin
            // 
            this.btnLoadBin.Location = new System.Drawing.Point(10, 12);
            this.btnLoadBin.Name = "btnLoadBin";
            this.btnLoadBin.Size = new System.Drawing.Size(120, 23);
            this.btnLoadBin.TabIndex = 0;
            this.btnLoadBin.Text = "Load BIN";
            this.btnLoadBin.Click += new System.EventHandler(this.BtnLoadBin_Click);
            // 
            // lblDetect
            // 
            this.lblDetect.AutoSize = true;
            this.lblDetect.Location = new System.Drawing.Point(10, 60);
            this.lblDetect.Name = "lblDetect";
            this.lblDetect.Size = new System.Drawing.Size(91, 16);
            this.lblDetect.TabIndex = 1;
            this.lblDetect.Text = "No file loaded";
            // 
            // btnPatchBin
            // 
            this.btnPatchBin.Enabled = false;
            this.btnPatchBin.Location = new System.Drawing.Point(10, 90);
            this.btnPatchBin.Name = "btnPatchBin";
            this.btnPatchBin.Size = new System.Drawing.Size(120, 23);
            this.btnPatchBin.TabIndex = 2;
            this.btnPatchBin.Text = "Patch BIN";
            this.btnPatchBin.Click += new System.EventHandler(this.BtnPatchBin_Click);
            // 
            // btnPatchWatermarks
            // 
            this.btnPatchWatermarks.Enabled = false;
            this.btnPatchWatermarks.Location = new System.Drawing.Point(142, 12);
            this.btnPatchWatermarks.Name = "btnPatchWatermarks";
            this.btnPatchWatermarks.Size = new System.Drawing.Size(125, 42);
            this.btnPatchWatermarks.TabIndex = 9;
            this.btnPatchWatermarks.Text = "Patch AT Watermarks";
            this.btnPatchWatermarks.Click += new System.EventHandler(this.BtnPatchWatermarks_Click);
            // 
            // btnSwsigStatusFix
            // 
            this.btnSwsigStatusFix.Enabled = false;
            this.btnSwsigStatusFix.Location = new System.Drawing.Point(142, 90);
            this.btnSwsigStatusFix.Name = "btnSwsigStatusFix";
            this.btnSwsigStatusFix.Size = new System.Drawing.Size(125, 42);
            this.btnSwsigStatusFix.TabIndex = 10;
            this.btnSwsigStatusFix.Text = "SWSIGSTATUS Fix";
            this.btnSwsigStatusFix.Click += new System.EventHandler(this.BtnSwsigStatusFix_Click);
            // 
            // btnSaveBin
            // 
            this.btnSaveBin.Enabled = false;
            this.btnSaveBin.Location = new System.Drawing.Point(10, 130);
            this.btnSaveBin.Name = "btnSaveBin";
            this.btnSaveBin.Size = new System.Drawing.Size(120, 23);
            this.btnSaveBin.TabIndex = 3;
            this.btnSaveBin.Text = "Save BIN As...";
            this.btnSaveBin.Click += new System.EventHandler(this.BtnSaveBin_Click);
            // 
            // lblConvertHeader
            // 
            this.lblConvertHeader.AutoSize = true;
            this.lblConvertHeader.Font = new System.Drawing.Font("Arial", 8F, System.Drawing.FontStyle.Italic);
            this.lblConvertHeader.Location = new System.Drawing.Point(5, 170);
            this.lblConvertHeader.Name = "lblConvertHeader";
            this.lblConvertHeader.Size = new System.Drawing.Size(129, 16);
            this.lblConvertHeader.TabIndex = 4;
            this.lblConvertHeader.Text = "For Gen 1 use only";
            this.lblConvertHeader.Click += new System.EventHandler(this.lblConvertHeader_Click);
            // 
            // panelGen1
            // 
            this.panelGen1.Controls.Add(this.btnOriginal);
            this.panelGen1.Controls.Add(this.btnTuned);
            this.panelGen1.Controls.Add(this.btnConvert);
            this.panelGen1.Controls.Add(this.btnRevert);
            this.panelGen1.Location = new System.Drawing.Point(10, 200);
            this.panelGen1.Name = "panelGen1";
            this.panelGen1.Size = new System.Drawing.Size(120, 160);
            this.panelGen1.TabIndex = 5;
            // 
            // btnOriginal
            // 
            this.btnOriginal.Enabled = false;
            this.btnOriginal.Location = new System.Drawing.Point(0, 0);
            this.btnOriginal.Name = "btnOriginal";
            this.btnOriginal.Size = new System.Drawing.Size(120, 23);
            this.btnOriginal.TabIndex = 0;
            this.btnOriginal.Text = "Original bin";
            this.btnOriginal.Click += new System.EventHandler(this.BtnOriginal_Click);
            // 
            // btnTuned
            // 
            this.btnTuned.Enabled = false;
            this.btnTuned.Location = new System.Drawing.Point(0, 40);
            this.btnTuned.Name = "btnTuned";
            this.btnTuned.Size = new System.Drawing.Size(120, 23);
            this.btnTuned.TabIndex = 1;
            this.btnTuned.Text = "Tuned bin";
            this.btnTuned.Click += new System.EventHandler(this.BtnTuned_Click);
            // 
            // btnConvert
            // 
            this.btnConvert.Enabled = false;
            this.btnConvert.Location = new System.Drawing.Point(0, 80);
            this.btnConvert.Name = "btnConvert";
            this.btnConvert.Size = new System.Drawing.Size(120, 23);
            this.btnConvert.TabIndex = 2;
            this.btnConvert.Text = "Convert";
            this.btnConvert.Click += new System.EventHandler(this.BtnConvert_Click);
            // 
            // btnRevert
            // 
            this.btnRevert.Enabled = false;
            this.btnRevert.Location = new System.Drawing.Point(0, 120);
            this.btnRevert.Name = "btnRevert";
            this.btnRevert.Size = new System.Drawing.Size(120, 23);
            this.btnRevert.TabIndex = 3;
            this.btnRevert.Text = "Revert";
            this.btnRevert.Click += new System.EventHandler(this.BtnRevert_Click);
            // 
            // txtOutput
            // 
            this.txtOutput.Location = new System.Drawing.Point(280, 12);
            this.txtOutput.Name = "txtOutput";
            this.txtOutput.ReadOnly = true;
            this.txtOutput.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.Vertical;
            this.txtOutput.Size = new System.Drawing.Size(334, 250);
            this.txtOutput.TabIndex = 7;
            this.txtOutput.Text = "";
            // 
            // BMWPatcherForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(638, 400);
            this.Controls.Add(this.btnLoadBin);
            this.Controls.Add(this.lblDetect);
            this.Controls.Add(this.btnPatchBin);
            this.Controls.Add(this.btnPatchWatermarks);
            this.Controls.Add(this.btnSwsigStatusFix);
            this.Controls.Add(this.btnSaveBin);
            this.Controls.Add(this.lblConvertHeader);
            this.Controls.Add(this.panelGen1);
            this.Controls.Add(this.btnAbout);
            this.Controls.Add(this.txtOutput);
            this.Controls.Add(this.lblFooter);
            this.Name = "BMWPatcherForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "BMW F/G Series OBD Patcher";
            this.Load += new System.EventHandler(this.BMWPatcherForm_Load);
            this.panelGen1.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
    }
}
