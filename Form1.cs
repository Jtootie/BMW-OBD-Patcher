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
            MessageBox.Show("v2.4, Created by O.S. Automotives and Jtooties Garage.       Added Software signature patch for 2020+ DMEs. new unlock bytes and watermark based on AutoTuner's post June 2020 DME unlock. AT bytes and checksum provided by tlovenspclsauce", "About",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void Log(string text)
        {
            txtOutput.AppendText(text + Environment.NewLine);
            txtOutput.ScrollToCaret();
        }

        private void BtnLoadBin_Click(object sender, EventArgs e)
        {
            using (var ofd = new OpenFileDialog())
            {
                ofd.Filter = "Binary files (*.bin)|*.bin";
                if (ofd.ShowDialog() != DialogResult.OK)
                    return;

                txtOutput.Clear();

                try
                {
                    _binData = File.ReadAllBytes(ofd.FileName);
                    _filePath = ofd.FileName;
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Failed to load file: " + ex.Message, "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                DetectAndSetup();
            }
        }

        private void DetectAndSetup()
        {
            if (_binData == null)
                return;

            // Gen1 detection
            byte[] gen1Pattern1 = { 0x80, 0x2A, 0x03, 0xE2, 0x07 };
            byte[] gen1Pattern2 = { 0x80, 0x48, 0x03, 0x44, 0x00 };

            // Gen2 detection
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
                    UnlockPatch = new byte[] { 0xA8, 0xCD, 0xBE, 0xAC },
                    UnlockCheck = new byte[] { 0x00, 0x00, 0x00, 0x00 },
                    UnlockApplied = new byte[] { 0xA8, 0xCD, 0xBE, 0xAC },
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
        }

        private void BtnOriginal_Click(object sender, EventArgs e)
        {
            using (var ofd = new OpenFileDialog())
            {
                ofd.Filter = "Binary files (*.bin)|*.bin";
                if (ofd.ShowDialog() != DialogResult.OK)
                    return;

                try
                {
                    _originalBinData = File.ReadAllBytes(ofd.FileName);
                    Log($"Original bin loaded: {Path.GetFileName(ofd.FileName)}");
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Failed to load original bin: " + ex.Message, "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void BtnTuned_Click(object sender, EventArgs e)
        {
            using (var ofd = new OpenFileDialog())
            {
                ofd.Filter = "Binary files (*.bin)|*.bin";
                if (ofd.ShowDialog() != DialogResult.OK)
                    return;

                try
                {
                    _tunedBinData = File.ReadAllBytes(ofd.FileName);
                    Log($"Tuned bin loaded: {Path.GetFileName(ofd.FileName)}");
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Failed to load tuned bin: " + ex.Message, "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
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

                if (sfd.ShowDialog() == DialogResult.OK)
                {
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
        }

        private void BtnPatchBin_Click(object sender, EventArgs e)
        {
            if (_binData == null || _patchInfo == null)
                return;

            int offset = _patchInfo.UnlockOffset;
            byte[] expected = _patchInfo.UnlockCheck;
            byte[] patch = _patchInfo.UnlockPatch;

            if (_binData.Length < offset + patch.Length)
            {
                Log($"File too short for unlock patch offset 0x{offset:X}");
                return;
            }

            byte[] currentBytes = _binData.Skip(offset).Take(patch.Length).ToArray();

            if (currentBytes.SequenceEqual(patch))
            {
                Log($"Unlock patch already applied at 0x{offset:X}");
            }
            else if (!currentBytes.SequenceEqual(expected))
            {
                Log($"Warning: Unexpected bytes at unlock offset 0x{offset:X}");
                return;
            }
            else
            {
                Log($"Unlock patch: before={BytesToHex(currentBytes)} after={BytesToHex(patch)} at 0x{offset:X}");
                Array.Copy(patch, 0, _binData, offset, patch.Length);
                Log($"Applied unlock patch at 0x{offset:X}");
                FixChecksums(offset, patch.Length, "unlock patch");
            }

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

            byte[] obdBefore = _binData.Skip(obdOffset).Take(_patchInfo.ObdPatch.Length).ToArray();
            Array.Copy(_patchInfo.ObdPatch, 0, _binData, obdOffset, _patchInfo.ObdPatch.Length);
            Log($"Applied OBD patch at 0x{obdOffset:X}: {BytesToHex(obdBefore)} -> {BytesToHex(_patchInfo.ObdPatch)}");
            FixChecksums(obdOffset, _patchInfo.ObdPatch.Length, "OBD patch");

            Log("Patching completed. Use 'Save BIN As...' to save the file.");
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
                SaveOutputLogCopy();
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
            SaveOutputLogCopy();
        }

        private void SaveOutputLogCopy()
        {
            if (string.IsNullOrEmpty(_filePath))
                return;

            string binName = Path.GetFileNameWithoutExtension(_filePath);
            string logPath = Path.Combine(Application.StartupPath, binName + ".txt");

            try
            {
                // Write once, log the destination, then write again so the TXT is an exact
                // copy of what is currently visible in the output window.
                File.WriteAllText(logPath, txtOutput.Text, Encoding.UTF8);
                Log($"Log saved to: {logPath}");
                File.WriteAllText(logPath, txtOutput.Text, Encoding.UTF8);
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
                        "this region will NOT be corrected if patched. Fix this checksum manually before flashing.");
                }
            }

            if (_btldDescriptors.Count == 0)
            {
                Log("Checksum parse: NO descriptors verified for this file. Checksum auto-correction is disabled — " +
                    "any patch inside BTLD will need its checksum fixed manually before flashing.");
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
            bool touchedAny = false;

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

                touchedAny = true;

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

            if (!touchedAny)
            {
                uint cpuAddress = IromBase + (uint)patchStart;
                Log($"CRC fix: 0x{cpuAddress:X8} ({patchLabel}) is outside all verified checksum descriptor ranges — " +
                    "no correction needed.");
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
                        File.WriteAllBytes(sfd.FileName, _binData);
                        Log($"Saved patched BIN as: {Path.GetFileName(sfd.FileName)}");
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

        private void lblConvertHeader_Click(object sender, EventArgs e)
        {

        }

        private void BMWPatcherForm_Load(object sender, EventArgs e)
        {

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
