using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace BMWIRomPatcher
{
    public partial class BMWPatcherForm : Form
    {
        private string _filePath;
        private byte[] _binData;
        private string _detection;
        private PatchInfo _patchInfo;

        private byte[] _originalBinData;
        private byte[] _tunedBinData;

        // Gen2 BTLD checksum layout. File offset 0 maps to CPU address 0x80000000.
        private const uint IromBase = 0x80000000u;
        private const uint BtldHeaderAddr = 0x80028000u;
        private const int BtldCountOff = 0x113;
        private const int BtldTableOff = 0x150;
        private const int BtldEntrySize = 16;

        // Only descriptors that self-verify against the currently loaded BIN are trusted.
        private readonly List<BtldDescriptor> _btldDescriptors = new List<BtldDescriptor>();

        public BMWPatcherForm()
        {
            InitializeComponent();
            // Load icon from embedded resource
            var asm = System.Reflection.Assembly.GetExecutingAssembly();
            using (var stream = asm.GetManifestResourceStream("bmw_obd_unlock.bmw.ico"))
            {
                if (stream != null)
                    this.Icon = new Icon(stream);
                else
                    MessageBox.Show("Could not load embedded icon.", "Icon Error");
            }
        }

        private void BtnAbout_Click(object sender, EventArgs e)
        {
            using (var about = new Form())
            {
                about.Text = "About BMW F/G Series OBD Unlock";
                about.StartPosition = FormStartPosition.CenterParent;
                about.ClientSize = new Size(720, 560);
                about.MinimumSize = new Size(560, 420);
                about.MinimizeBox = false;
                about.MaximizeBox = false;
                about.ShowInTaskbar = false;
                about.Font = new Font("Segoe UI", 10F);

                var layout = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    Padding = new Padding(18),
                    ColumnCount = 1,
                    RowCount = 3
                };
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
                layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

                layout.Controls.Add(new Label
                {
                    Text = "BMW F/G Series OBD Unlock",
                    Dock = DockStyle.Fill,
                    Font = new Font(about.Font, FontStyle.Bold),
                    TextAlign = ContentAlignment.MiddleLeft
                }, 0, 0);
                layout.Controls.Add(new Label
                {
                    Text = "Created by Jtooties Garage, O.S. Automotives, and tlovenspclsauce",
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleLeft
                }, 0, 1);

                var changelog = new TextBox
                {
                    Dock = DockStyle.Fill,
                    Multiline = true,
                    ReadOnly = true,
                    WordWrap = true,
                    ScrollBars = ScrollBars.Vertical,
                    BackColor = SystemColors.Window,
                    Text =
                        "Change log:\r\n\r\n" +
                        "Version 2.6:\r\n" +
                        "Complete interface change, added the ability to check the CRC regions, \"save bin as\" will correct any CRC mismatches, and saves an output of the log window.\r\n\r\n" +
                        "Version 2.5:\r\n" +
                        "Added checksum correction for Gen1 B58 bin files.\r\n\r\n" +
                        "Version 2.4:\r\n" +
                        "Added SWSIGSTATUS button. Used for the post June DMEs where unlocking and/or flashing leads to the DME rejecting the FSCs, or the ability to add new ones.\r\n\r\n" +
                        "Version 2.3:\r\n" +
                        "Added AT watermark and CRC correction for June 2020+ vehicles.\r\n\r\n" +
                        "Version 2.2:\r\n" +
                        "Added the ability to create a BIN file from the bench read that can be properly read by TunerPro for editing (Gen1 only).\r\n\r\n" +
                        "Version 1.0:\r\n" +
                        "Initial release of the iRom patcher, patches the bench read file of F and select G series up until DME production date of June 2020."
                };
                layout.Controls.Add(changelog, 0, 2);
                about.Controls.Add(layout);
                about.AcceptButton = new Button { Text = "OK", DialogResult = DialogResult.OK, Visible = false };
                about.ShowDialog(this);
            }
        }

        private void Log(string text)
        {
            txtOutput.AppendText(text + Environment.NewLine);
            txtOutput.ScrollToCaret();
        }

        private void BtnLoadBin_Click(object sender, EventArgs e)
        {
            if (!TryReadBinaryFile("Binary files (*.bin)|*.bin", "Load BIN", out var data, out var path))
                return;

            txtOutput.Clear();
            _binData = data;
            _filePath = path;
            loadedFile.Text = _filePath;
            DetectAndSetup();
        }

        private static bool TryReadBinaryFile(string filter, string title, out byte[] data, out string path)
        {
            using (var ofd = new OpenFileDialog())
            {
                ofd.Filter = filter;
                ofd.Title = title;
                if (ofd.ShowDialog() != DialogResult.OK)
                {
                    data = null;
                    path = null;
                    return false;
                }

                try
                {
                    data = File.ReadAllBytes(ofd.FileName);
                    path = ofd.FileName;
                    return true;
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Failed to load file: " + ex.Message, "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    data = null;
                    path = null;
                    return false;
                }
            }
        }

        private void DetectAndSetup()
        {
            _detection = null;
            _patchInfo = null;
            _btldDescriptors.Clear();
            DisableActionButtons();
            if (_binData == null)
                return;

            byte[] gen1Pattern1 = { 0x80, 0x2A, 0x03, 0xE2, 0x07 };
            byte[] gen1Pattern2 = { 0x80, 0x48, 0x03, 0x44, 0x00 };
            byte[] gen2Pattern1 = { 0x91, 0x10, 0x00, 0x26, 0xF6, 0x27 };
            byte[] gen2Pattern2 = { 0x91, 0x10, 0x00, 0x26, 0x82, 0x02 };

            if (ContainsPattern(_binData, gen1Pattern1) || ContainsPattern(_binData, gen1Pattern2))
            {
                _detection = "Gen1 ORI/IROM";
                _patchInfo = new PatchInfo
                {
                    UnlockOffset = 0x40260,
                    UnlockPatch = new byte[] { 0x39, 0x7E, 0xB6, 0x88 },
                    UnlockCheck = new byte[] { 0x00, 0x00, 0x00, 0x00 },
                    UnlockApplied = new byte[] { 0x39, 0x7E, 0xB6, 0x88 },
                    ObdFind = gen1Pattern1,
                    ObdPatch = gen1Pattern2
                };

                btnOriginal.Enabled = true;
                btnTuned.Enabled = true;
                btnConvert.Enabled = true;
                btnRevert.Enabled = true;
                btnPatchWatermarks.Enabled = false;
                btnSwsigStatusFix.Enabled = true;
                _btldDescriptors.Clear();
            }
            else if (ContainsPattern(_binData, gen2Pattern1) || ContainsPattern(_binData, gen2Pattern2))
            {
                _detection = "Gen2 ORI/IROM";
                _patchInfo = new PatchInfo
                {
                    // Preserve the Visual Studio program's existing Gen2 unlock value.
                    UnlockOffset = 0x5F7DC,
                    UnlockPatch = new byte[] { 0x38, 0xD1, 0xBF, 0xDC },
                    UnlockCheck = new byte[] { 0x00, 0x00, 0x00, 0x00 },
                    UnlockApplied = new byte[] { 0x38, 0xD1, 0xBF, 0xDC },
                    ObdFind = gen2Pattern1,
                    ObdPatch = gen2Pattern2,

                    // AutoTuner-style Gen2 watermarks ported from the Python patcher.
                    Watermark1Offset = 0x5FEA9,
                    Watermark1Check = new byte[] { 0x5F, 0x5F, 0x5F, 0x5F },
                    Watermark1Patch = new byte[] { 0x41, 0x54, 0x41, 0x54 },

                    Watermark2Offset = 0x5FEFA,
                    Watermark2Check = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF },
                    Watermark2CheckAlt = new byte[] { 0x5F, 0x5F, 0x5F, 0x5F, 0x5F },
                    Watermark2Patch = new byte[] { 0x00, 0x41, 0x54, 0x41, 0x54 }
                };

                btnOriginal.Enabled = false;
                btnTuned.Enabled = false;
                btnConvert.Enabled = false;
                btnRevert.Enabled = false;
                btnPatchWatermarks.Enabled = true;
                btnSwsigStatusFix.Enabled = true;

                ParseBtldDescriptors();
            }
            else
            {
                btnPatchBin.Enabled = false;
                btnSaveBin.Enabled = false;
                btnPatchWatermarks.Enabled = false;
                btnSwsigStatusFix.Enabled = false;
                _btldDescriptors.Clear();
                lblDetect.Text = "Unknown file/patch not supported";
                Log("Failed to detect supported patch sequence.");
                return;
            }

            lblDetect.Text = $"Detected: {_detection}";
            Log($"Loaded: {Path.GetFileName(_filePath)}");
            Log($"Detected: {_detection}");

            // Firmware version
            if (_binData.Length >= 0x164 + 10)
            {
                string fw = Encoding.ASCII.GetString(_binData, 0x164, 10);
                Log("Firmware version: " + fw);
            }
            else
            {
                Log("File too short for firmware version info (offset 0x164).");
            }

            // Current PRG
            if (_binData.Length >= 0x80145 + 7)
            {
                var prgBytes = _binData.Skip(0x80145).Take(7);
                string prg = string.Join(" ", prgBytes.Select(b => b.ToString("X2")));
                Log("Current PRG: " + prg);
            }
            else
            {
                Log("File too short for Current PRG info (offset 0x80145).");
            }

            CheckEngineChassis();
            btnPatchBin.Enabled = true;
            btnSaveBin.Enabled = true;
            btnCrcCheck.Enabled = true;
        }

        private void DisableActionButtons()
        {
            foreach (var button in new[]
            {
                btnPatchBin,
                btnSaveBin,
                btnCrcCheck,
                btnPatchWatermarks,
                btnSwsigStatusFix,
                btnOriginal,
                btnTuned,
                btnConvert,
                btnRevert
            })
            {
                if (button != null)
                    button.Enabled = false;
            }
        }

        private void BtnOriginal_Click(object sender, EventArgs e)
        {
            if (!TryReadBinaryFile("Binary files (*.bin)|*.bin", "Load Original BIN", out var data, out var path))
                return;

            _originalBinData = data;
            Log($"Original bin loaded: {Path.GetFileName(path)}");
        }

        private void BtnTuned_Click(object sender, EventArgs e)
        {
            if (!TryReadBinaryFile("Binary files (*.bin)|*.bin", "Load Tuned BIN", out var data, out var path))
                return;

            _tunedBinData = data;
            Log($"Tuned bin loaded: {Path.GetFileName(path)}");
        }

        private void BtnConvert_Click(object sender, EventArgs e)
        {
            if (_originalBinData == null || _tunedBinData == null)
            {
                MessageBox.Show("Original or Tuned bin not loaded.", "Warning",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!HasCbMarker(_originalBinData))
            {
                MessageBox.Show("Original BIN does not contain CB_ marker at 0xD00.", "Warning",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            byte[] insertData = _originalBinData.Take(0xD00).ToArray();
            byte[] tunedTail = _tunedBinData.Skip(0x40D00).ToArray();
            _tunedBinData = insertData.Concat(tunedTail).ToArray();

            SaveCustomBin("Save Converted BIN As...");
        }

        private void BtnRevert_Click(object sender, EventArgs e)
        {
            if (_tunedBinData == null || _binData == null)
            {
                MessageBox.Show("Tuned BIN or main loaded BIN not available.", "Warning",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!HasCbMarker(_tunedBinData))
            {
                MessageBox.Show("Tuned BIN does not contain CB_ marker at 0xD00.", "Warning",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            byte[] insertData = _binData.Take(0x40D00).ToArray();
            byte[] tunedTail = _tunedBinData.Skip(0xD00).ToArray();
            _tunedBinData = insertData.Concat(tunedTail).ToArray();

            SaveCustomBin("Save Reverted BIN As...");
        }

        private void SaveCustomBin(string title)
        {
            if (_tunedBinData == null)
                return;

            using (var sfd = new SaveFileDialog())
            {
                sfd.Title = title;
                sfd.DefaultExt = "bin";
                sfd.Filter = "Binary files (*.bin)|*.bin";

                if (sfd.ShowDialog() != DialogResult.OK)
                    return;

                try
                {
                    File.WriteAllBytes(sfd.FileName, _tunedBinData);
                    Log($"Saved: {Path.GetFileName(sfd.FileName)}");
                    MessageBox.Show($"BIN saved as:\n{Path.GetFileName(sfd.FileName)}", "Saved",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Failed to save file: " + ex.Message, "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void BtnPatchBin_Click(object sender, EventArgs e)
        {
            if (_binData == null || _patchInfo == null)
                return;

            if (!ApplyPatchAtOffset(_patchInfo.UnlockOffset, _patchInfo.UnlockCheck, _patchInfo.UnlockPatch, "Unlock patch"))
                return;

            int obdOffset = IndexOfSequence(_binData, _patchInfo.ObdFind);
            if (obdOffset == -1)
            {
                if (IndexOfSequence(_binData, _patchInfo.ObdPatch) != -1)
                {
                    Log("OBD patch already applied.");
                }
                else
                {
                    Log("Error: OBD patch sequence not found.");
                }
                return;
            }

            ApplyPatchSequence(obdOffset, _patchInfo.ObdPatch, "OBD patch");
            Log("Patching completed. Use 'Save BIN As...' to save the file.");
        }

        private bool ApplyPatchAtOffset(int offset, byte[] expected, byte[] patch, string patchName)
        {
            if (_binData == null || patch == null)
                return false;

            if (_binData.Length < offset + patch.Length)
            {
                Log($"File too short for {patchName} offset 0x{offset:X}");
                return false;
            }

            byte[] currentBytes = _binData.Skip(offset).Take(patch.Length).ToArray();

            if (currentBytes.SequenceEqual(patch))
            {
                Log($"{patchName} already applied at 0x{offset:X}");
                return true;
            }

            if (!currentBytes.SequenceEqual(expected))
            {
                Log($"Warning: Unexpected bytes at {patchName} offset 0x{offset:X}");
                return false;
            }

            ApplyPatchSequence(offset, patch, patchName);
            return true;
        }

        private void ApplyPatchSequence(int offset, byte[] patch, string patchName)
        {
            if (_binData == null || patch == null)
                return;

            byte[] before = _binData.Skip(offset).Take(patch.Length).ToArray();
            Array.Copy(patch, 0, _binData, offset, patch.Length);
            Log($"Applied {patchName} at 0x{offset:X}: {BytesToHex(before)} -> {BytesToHex(patch)}");
            FixChecksums(offset, patch.Length, patchName);
        }

        private void BtnSwsigStatusFix_Click(object sender, EventArgs e)
        {
            if (_binData == null)
            {
                Log("SWSIGSTATUS Fix: no BIN loaded.");
                return;
            }

            byte[] find = { 0xDF, 0x22, 0x33, 0x01 };
            byte[] patch = { 0x00, 0x00, 0x82, 0x02 };

            int offset = IndexOfSequence(_binData, find);
            if (offset == -1)
            {
                Log("SWSIGSTATUS Fix: target sequence DF223301 was not found (it may already be patched). No changes made.");
                return;
            }

            uint cpuAddress = IromBase + (uint)offset;
            Log($"SWSIGSTATUS Fix: DF223301 found at file offset 0x{offset:X} / CPU address 0x{cpuAddress:X8}.");

            byte[] before = _binData.Skip(offset).Take(find.Length).ToArray();
            Array.Copy(patch, 0, _binData, offset, patch.Length);

            Log($"Applied SWSIGSTATUS Fix at file offset 0x{offset:X} / CPU address 0x{cpuAddress:X8}: " +
                $"{BytesToHex(before)} -> {BytesToHex(patch)}");
            FixChecksums(offset, patch.Length, "SWSIGSTATUS Fix");
            Log("SWSIGSTATUS Fix completed. Use 'Save BIN As...' to save the file.");
        }

        private void SaveOutputLogCopy(string binPath)
        {
            if (string.IsNullOrEmpty(binPath))
                return;

            string logPath = Path.ChangeExtension(binPath, ".txt");

            try
            {
                File.WriteAllText(logPath, txtOutput.Text, Encoding.UTF8);
                Log($"Log saved to: {logPath}");
            }
            catch (Exception ex)
            {
                Log("Failed to save log file: " + ex.Message);
            }
        }

        private void BtnPatchWatermarks_Click(object sender, EventArgs e)
        {
            if (_binData == null || _patchInfo == null || _detection != "Gen2 ORI/IROM")
            {
                Log("Watermark patching only available for Gen2 (BTLD) images.");
                return;
            }

            bool appliedAny = false;

            appliedAny |= ApplyWatermark(
                1,
                _patchInfo.Watermark1Offset,
                _patchInfo.Watermark1Check,
                _patchInfo.Watermark1CheckAlt,
                _patchInfo.Watermark1Patch);

            appliedAny |= ApplyWatermark(
                2,
                _patchInfo.Watermark2Offset,
                _patchInfo.Watermark2Check,
                _patchInfo.Watermark2CheckAlt,
                _patchInfo.Watermark2Patch);

            if (appliedAny)
                Log("Watermark patching completed. Use 'Save BIN As...' to save the file.");
        }

        private bool ApplyWatermark(int index, int offset, byte[] check, byte[] checkAlt, byte[] patch)
        {
            if (offset < 0 || check == null || patch == null)
                return false;

            int length = check.Length;
            if (patch.Length != length)
            {
                Log($"Watermark #{index}: patch length does not match expected length — skipped.");
                return false;
            }

            if (_binData.Length < offset + length)
            {
                Log($"Watermark #{index}: offset 0x{offset:X} is outside the loaded file — skipped.");
                return false;
            }

            byte[] currentBytes = _binData.Skip(offset).Take(length).ToArray();

            if (currentBytes.SequenceEqual(patch))
            {
                Log($"Watermark #{index} already applied at 0x{offset:X}");
                return false;
            }

            bool matchesPrimary = currentBytes.SequenceEqual(check);
            bool matchesAlternate = checkAlt != null && currentBytes.SequenceEqual(checkAlt);

            if (!matchesPrimary && !matchesAlternate)
            {
                string variants = checkAlt == null
                    ? BytesToHex(check)
                    : BytesToHex(check) + " or " + BytesToHex(checkAlt);

                Log($"Warning: unexpected bytes at watermark #{index} offset 0x{offset:X}: " +
                    $"{BytesToHex(currentBytes)} (expected {variants}) — skipped.");
                return false;
            }

            Array.Copy(patch, 0, _binData, offset, length);
            Log($"Applied watermark #{index} at 0x{offset:X}: {BytesToHex(currentBytes)} -> {BytesToHex(patch)}");
            FixChecksums(offset, length, $"watermark #{index}");
            return true;
        }

        private void ParseBtldDescriptors()
        {
            _btldDescriptors.Clear();

            if (_binData == null)
                return;

            int headerOffset = checked((int)(BtldHeaderAddr - IromBase));
            int countOffset = headerOffset + BtldCountOff;
            int tableOffset = headerOffset + BtldTableOff;

            if (countOffset >= _binData.Length)
            {
                Log("Checksum parse: BTLD header out of file bounds — checksum correction unavailable for this file.");
                return;
            }

            int count = _binData[countOffset];
            if (count == 0 || count > 16)
            {
                Log($"Checksum parse: implausible descriptor count ({count}) at header +0x{BtldCountOff:X} — " +
                    "checksum correction unavailable for this file.");
                return;
            }

            Log($"Checksum parse: BTLD header @ 0x{BtldHeaderAddr:X8} reports {count} descriptor(s). Self-verifying each...");

            for (int i = 0; i < count; i++)
            {
                int entryOffset = tableOffset + (i * BtldEntrySize);
                if (entryOffset < 0 || entryOffset + BtldEntrySize > _binData.Length)
                {
                    Log($"Checksum parse: descriptor {i} out of file bounds — skipped.");
                    continue;
                }

                uint start = ReadUInt32LE(_binData, entryOffset);
                uint end = ReadUInt32LE(_binData, entryOffset + 4);
                byte type = _binData[entryOffset + 11];
                uint storedPtr = ReadUInt32LE(_binData, entryOffset + 12);

                if (type != 0x02 && type != 0x82)
                {
                    Log($"Checksum parse: descriptor {i} type=0x{type:X2} is not CRC32 — skipped " +
                        $"(region 0x{start:X8}-0x{end:X8} will NOT be corrected if patched).");
                    continue;
                }

                long regionOffsetLong = (long)start - IromBase;
                long storedOffsetLong = (long)storedPtr - IromBase;
                long lengthLong = (long)end - start + 1L;

                if (regionOffsetLong < 0 ||
                    storedOffsetLong < 0 ||
                    lengthLong <= 0 ||
                    regionOffsetLong + lengthLong > _binData.Length ||
                    storedOffsetLong + 4 > _binData.Length ||
                    lengthLong > int.MaxValue)
                {
                    Log($"Checksum parse: descriptor {i} 0x{start:X8}-0x{end:X8} out of file bounds — skipped.");
                    continue;
                }

                int regionOffset = (int)regionOffsetLong;
                int storedOffset = (int)storedOffsetLong;
                int length = (int)lengthLong;

                uint computed = ComputeCrc32(_binData, regionOffset, length);
                uint stored = ReadUInt32LE(_binData, storedOffset);

                if (computed == stored)
                {
                    _btldDescriptors.Add(new BtldDescriptor
                    {
                        Start = start,
                        End = end,
                        StoredPointer = storedPtr
                    });

                    Log($"Checksum parse: descriptor {i} 0x{start:X8}-0x{end:X8} VERIFIED " +
                        $"(stored@0x{storedPtr:X8}=0x{stored:X8}) — trusted.");
                }
                else
                {
                    Log($"Checksum parse: descriptor {i} 0x{start:X8}-0x{end:X8} MISMATCH — " +
                        $"computed 0x{computed:X8} vs stored 0x{stored:X8}. NOT trusted — " +
                        "this region will be corrected once saved");
                }
            }

            if (_btldDescriptors.Count == 0)
            {
                Log("Checksum parse: NO descriptors verified for this file. Descriptor-based patch-time correction is disabled — " +
                    "Save BIN As will calculate the configured generation-specific CRC regions.");
            }
        }

        private void FixChecksums(int fileOffset, int patchLength, string patchLabel)
        {
            if (_detection != "Gen2 ORI/IROM")
            {
                Log($"CRC fix skipped for {patchLabel}: checksum layout not verified for {_detection}.");
                return;
            }

            if (_btldDescriptors.Count == 0)
            {
                Log($"CRC fix skipped for {patchLabel}: no verified checksum descriptors for this file — " +
                    "check the checksum manually before flashing.");
                return;
            }

            int patchStart = fileOffset;
            int patchEnd = fileOffset + patchLength - 1;

            foreach (BtldDescriptor descriptor in _btldDescriptors)
            {
                long regionStartLong = (long)descriptor.Start - IromBase;
                long regionEndLong = (long)descriptor.End - IromBase;
                long storedOffsetLong = (long)descriptor.StoredPointer - IromBase;

                if (regionStartLong < 0 || regionEndLong < regionStartLong ||
                    storedOffsetLong < 0 || storedOffsetLong + 4 > _binData.Length)
                    continue;

                if (patchEnd < regionStartLong || patchStart > regionEndLong)
                    continue;

                long lengthLong = regionEndLong - regionStartLong + 1;
                if (lengthLong <= 0 || lengthLong > int.MaxValue ||
                    regionStartLong + lengthLong > _binData.Length)
                {
                    Log($"CRC fix: trusted descriptor 0x{descriptor.Start:X8}-0x{descriptor.End:X8} " +
                        "is no longer in bounds — skipped.");
                    continue;
                }

                int regionStart = (int)regionStartLong;
                int storedOffset = (int)storedOffsetLong;
                int length = (int)lengthLong;

                uint oldCrc = ReadUInt32LE(_binData, storedOffset);
                uint newCrc = ComputeCrc32(_binData, regionStart, length);
                WriteUInt32LE(_binData, storedOffset, newCrc);

                int headLength = Math.Min(8, length);
                int tailLength = Math.Min(8, length);
                string head = BytesToHex(_binData.Skip(regionStart).Take(headLength).ToArray());
                string tail = BytesToHex(_binData.Skip(regionStart + length - tailLength).Take(tailLength).ToArray());

                Log($"CRC fix: 0x{descriptor.Start:X8}-0x{descriptor.End:X8} " +
                    $"old=0x{oldCrc:X8} new=0x{newCrc:X8} stored@0x{descriptor.StoredPointer:X8} " +
                    $"region_len={length} region_head={head} region_tail={tail}");

                uint verifyStored = ReadUInt32LE(_binData, storedOffset);
                uint verifyCrc = ComputeCrc32(_binData, regionStart, length);

                if (verifyCrc == verifyStored && verifyStored == newCrc)
                {
                    Log($"CRC fix: post-write self-check PASSED " +
                        $"(re-read stored=0x{verifyStored:X8}, re-computed=0x{verifyCrc:X8})");
                }
                else
                {
                    Log($"CRC fix: post-write self-check FAILED — stored=0x{verifyStored:X8} " +
                        $"recomputed=0x{verifyCrc:X8} expected=0x{newCrc:X8}. DO NOT TRUST THIS OUTPUT.");
                }
            }

        }

        private static uint ComputeCrc32(byte[] data, int offset, int length)
        {
            uint crc = 0xFFFFFFFFu;

            for (int i = offset; i < offset + length; i++)
            {
                crc ^= data[i];

                for (int bit = 0; bit < 8; bit++)
                {
                    if ((crc & 1u) != 0)
                        crc = (crc >> 1) ^ 0xEDB88320u;
                    else
                        crc >>= 1;
                }
            }

            return ~crc;
        }

        private static uint ReadUInt32LE(byte[] data, int offset)
        {
            return (uint)(
                data[offset] |
                (data[offset + 1] << 8) |
                (data[offset + 2] << 16) |
                (data[offset + 3] << 24));
        }

        private static void WriteUInt32LE(byte[] data, int offset, uint value)
        {
            data[offset] = (byte)(value & 0xFF);
            data[offset + 1] = (byte)((value >> 8) & 0xFF);
            data[offset + 2] = (byte)((value >> 16) & 0xFF);
            data[offset + 3] = (byte)((value >> 24) & 0xFF);
        }

        private static string BytesToHex(byte[] data)
        {
            if (data == null)
                return "";

            return string.Concat(data.Select(b => b.ToString("X2")));
        }

        private void SaveLoadedBin(string path)
        {
            var generation = BinChecksums.FromDetection(_detection);
            var descriptors = _btldDescriptors.Select(d => new BinCrcRegion(
                checked((int)(d.StoredPointer - IromBase)),
                checked((int)(d.Start - IromBase)),
                checked((int)(d.End - IromBase)))).ToArray();
            var corrected = BinChecksums.Save(path, _binData, generation, descriptors);
            _binData = corrected.Data;
            foreach (string message in corrected.Messages) Log(message);
        }

        private BinCrcRegion[] GetVerifiedBtldRegions()
        {
            return _btldDescriptors
                .GroupBy(d => new { d.Start, d.End, d.StoredPointer })
                .Select(group => group.First())
                .Select(d => new BinCrcRegion(
                    checked((int)(d.StoredPointer - IromBase)),
                    checked((int)(d.Start - IromBase)),
                    checked((int)(d.End - IromBase))))
                .ToArray();
        }

        private void BtnCrcCheck_Click(object sender, EventArgs e)
        {
            if (_binData == null)
                return;

            try
            {
                var generation = BinChecksums.FromDetection(_detection);
                var result = BinChecksums.Check(_binData, generation, GetVerifiedBtldRegions());
                foreach (string message in result.Messages)
                    Log(message);
            }
            catch (Exception ex)
            {
                Log("CRC check failed: " + ex.Message);
            }
        }

        private void BtnSaveBin_Click(object sender, EventArgs e)
        {
            if (_binData == null)
                return;

            using (var sfd = new SaveFileDialog())
            {
                sfd.DefaultExt = "bin";
                sfd.Filter = "Binary files (*.bin)|*.bin";

                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        SaveLoadedBin(sfd.FileName);
                        Log($"Saved patched BIN as: {Path.GetFileName(sfd.FileName)}");
                        SaveOutputLogCopy(sfd.FileName);
                        MessageBox.Show($"BIN saved as:\n{Path.GetFileName(sfd.FileName)}", "Saved",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Failed to save file: " + ex.Message, "Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void CheckEngineChassis()
        {
            if (_binData == null)
                return;

            if (_detection == "Gen1 ORI/IROM")
            {
                if (_binData.Length >= 0x7BFE58 + 3)
                {
                    string engine = Encoding.ASCII.GetString(_binData, 0x7BFE58, 3);
                    Log("Engine: " + engine);
                }
                else
                {
                    Log("File too short for Gen1 Engine info (offset 0x7BFE58).");
                }

                if (_binData.Length >= 0x7BFE68 + 3)
                {
                    string chassis = Encoding.ASCII.GetString(_binData, 0x7BFE68, 3);
                    Log("Chassis: " + chassis);
                }
                else
                {
                    Log("File too short for Gen1 Chassis info (offset 0x7BFE68).");
                }
            }
            else if (_detection == "Gen2 ORI/IROM")
            {
                if (_binData.Length >= 0x7FFE59 + 3)
                {
                    string candidate1 = Encoding.ASCII.GetString(_binData, 0x7FFE59, 3);
                    if (candidate1 == "B46" || candidate1 == "B48")
                    {
                        Log("Engine: " + candidate1);
                        if (_binData.Length >= 0x7FFE62 + 3)
                        {
                            string chassis = Encoding.ASCII.GetString(_binData, 0x7FFE62, 3);
                            Log("Chassis: " + chassis);
                        }
                    }
                    else if (_binData.Length >= 0x7FFE5B + 3)
                    {
                        string candidate2 = Encoding.ASCII.GetString(_binData, 0x7FFE5B, 3);
                        if (candidate2 == "B58" || candidate2 == "S58")
                        {
                            Log("Engine: " + candidate2);
                            if (_binData.Length >= 0x7FFE64 + 3)
                            {
                                string chassis = Encoding.ASCII.GetString(_binData, 0x7FFE64, 3);
                                Log("Chassis: " + chassis);
                            }
                        }
                        else if (candidate2 == "S63")
                        {
                            if (_binData.Length >= 0x7FFE61 + 4)
                            {
                                string chassis = Encoding.ASCII.GetString(_binData, 0x7FFE61, 4);
                                Log("Chassis: " + chassis);
                            }
                        }
                    }
                }
            }
        }

        private bool HasCbMarker(byte[] data)
        {
            if (data == null || data.Length < 0xD00 + 3)
                return false;

            string marker = Encoding.ASCII.GetString(data, 0xD00, 3);
            return marker == "CB_";
        }

        private static bool ContainsPattern(byte[] data, byte[] pattern)
        {
            return IndexOfSequence(data, pattern) != -1;
        }

        private static int IndexOfSequence(byte[] data, byte[] pattern)
        {
            if (data == null || pattern == null || pattern.Length == 0 || data.Length < pattern.Length)
                return -1;

            for (int i = 0; i <= data.Length - pattern.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < pattern.Length; j++)
                {
                    if (data[i + j] != pattern[j])
                    {
                        match = false;
                        break;
                    }
                }
                if (match)
                    return i;
            }

            return -1;
        }

    }

    public class PatchInfo
    {
        public int UnlockOffset { get; set; }
        public byte[] UnlockPatch { get; set; }
        public byte[] UnlockCheck { get; set; }
        public byte[] UnlockApplied { get; set; }
        public byte[] ObdFind { get; set; }
        public byte[] ObdPatch { get; set; }

        public int Watermark1Offset { get; set; } = -1;
        public byte[] Watermark1Check { get; set; }
        public byte[] Watermark1CheckAlt { get; set; }
        public byte[] Watermark1Patch { get; set; }

        public int Watermark2Offset { get; set; } = -1;
        public byte[] Watermark2Check { get; set; }
        public byte[] Watermark2CheckAlt { get; set; }
        public byte[] Watermark2Patch { get; set; }
    }

    internal class BtldDescriptor
    {
        public uint Start { get; set; }
        public uint End { get; set; }
        public uint StoredPointer { get; set; }
    }
}
