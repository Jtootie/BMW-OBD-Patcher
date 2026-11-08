using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace BMWIRomPatcher
{
    internal sealed class BinMetadataRow
    {
        public string Field { get; set; }
        public string Value { get; set; }
        public string Source { get; set; }
    }

    internal sealed class BinMetadataReport
    {
        public readonly List<BinMetadataRow> Rows = new List<BinMetadataRow>();
        public const string Notice = "Identification labels only; checksums, signatures, unlock status and vehicle compatibility are not validated. " +
            "Component parsing supports recognized BMW description/header structures, not every ECU format. " +
            "Not found means no supported metadata was identified, not that a component is absent. " +
            "Multiple values are shown rather than choosing one. Release labels are not a verified vehicle I-Step; output labels are not measured power.";

        public string ToText()
        {
            var text = new StringBuilder();
            text.AppendLine("BIN information — current loaded data");
            text.AppendLine();
            foreach (var row in Rows)
            {
                text.AppendLine(row.Field + ": " + row.Value);
                if (!string.IsNullOrEmpty(row.Source)) text.AppendLine("  " + row.Source);
            }
            text.AppendLine();
            text.AppendLine(Notice);
            return text.ToString();
        }
    }

    // Read-only, marker-based extraction. No global scan for bare SGBM byte patterns:
    // they also occur in dependencies, historical records and machine code.
    internal static class BinMetadata
    {
        private sealed class Label
        {
            public int Offset;
            public string Text;
        }

        public static BinMetadataReport Read(byte[] data, string path)
        {
            if (data == null) throw new ArgumentNullException("data");
            var report = new BinMetadataReport();
            Add(report, "File", Path.GetFileName(path ?? "") , "Current in-memory BIN (includes unsaved edits)");
            Add(report, "Size", data.Length.ToString("N0", CultureInfo.InvariantCulture) + " bytes", "");
            using (var sha = SHA256.Create())
                Add(report, "SHA-256", BitConverter.ToString(sha.ComputeHash(data)).Replace("-", "").ToLowerInvariant(), "Current in-memory BIN");

            var labels = PrintableLabels(data).ToList();
            const string familyPattern = @"\b(?:MG1[A-Z]{2}\d{3}|MD1[A-Z]{2}\d{3}|MEVD\d(?:\.\d+)+|MED\d(?:\.\d+)+|EDC\d[A-Z0-9]+)\b";
            var identificationLabels = labels.Where(x => Regex.IsMatch(x.Text, @"^(?:MG1|MD1)[A-Z]{2}\d{3}(?:-|$)")).ToList();
            // Hardware identification takes precedence over software build references,
            // which can name a different shared ECU family (e.g. CS024 versus CS201).
            AddMatches(report, "ECU family", identificationLabels.Count > 0 ? identificationLabels : labels, familyPattern);
            AddMatches(report, "Software ECU family references", labels.Where(x => x.Text.Contains("/11/")), familyPattern);
            AddMatches(report, "ECU identification string", labels, @"\b(?:MG1|MD1)[A-Z]{2}\d{3}-[A-Za-z0-9.]+(?: +H\d+)?\b");
            AddMatches(report, "Bosch number", labels, @"\b0(?:261|281)[A-Z0-9]{6}\b");
            AddMatches(report, "Bosch software number", labels, @"\b10SW\d{6}\b");
            var application = new List<Label>();
            var market = new List<Label>();
            var output = new List<Label>();
            var calibration = new List<Label>();
            var software = new List<Label>();
            var release = new List<Label>();
            var engineVariants = new List<Label>();
            var electrical = new List<Label>();
            var calibrationIds = new List<Label>();
            var components = new Dictionary<string, List<Label>>();
            foreach (string kind in new[] { "HWEL", "HWAP", "BTLD", "SWFL", "SWFK" }) components[kind] = new List<Label>();

            foreach (var label in labels)
            {
                // A label can begin inside a longer printable run, so retain the exact match offset.
                foreach (Match match in Regex.Matches(label.Text, @"#DME_[^#\r\n]{1,40}#[^#\r\n]{1,8}#(HWE|HWA|BTL|PST|DST)#([^#\r\n]+)"))
                {
                    string role = match.Groups[1].Value;
                    string description = match.Groups[2].Value.TrimEnd('_', ' ');
                    int marker = label.Offset + match.Index;
                    int descriptionOffset = label.Offset + match.Groups[2].Index;
                    if (role == "DST")
                    {
                        calibration.Add(new Label { Offset = descriptionOffset, Text = description });
                        foreach (Match variant in Regex.Matches(description, @"(?<=[-_])[BSNM]\d{2}[BD]\d{2}[A-Z]\d(?=[-_]|$)"))
                            engineVariants.Add(new Label { Offset = descriptionOffset + variant.Index, Text = variant.Value });
                        foreach (Match volts in Regex.Matches(description, @"(?<=[-_])(?:12|24|48)V(?=[-_]|$)"))
                            electrical.Add(new Label { Offset = descriptionOffset + volts.Index, Text = volts.Value });
                        string calibrationTail = label.Text.Substring(match.Index + match.Length);
                        foreach (Match id in Regex.Matches(calibrationTail, @"#(?!EPST:)([A-Z0-9]{9,}(?:_[A-Z0-9]+)*)(?=[_#]|$)"))
                            calibrationIds.Add(new Label { Offset = label.Offset + match.Index + match.Length + id.Groups[1].Index, Text = id.Groups[1].Value.TrimEnd('_') });
                        Match app = Regex.Match(description, @"(?:^|_)((?:[BSNM]\d{2}[A-Z0-9]*)_(?:[EFGUIT]\d{2,3})(?:_(?:RWD|AWD|XDRIVE))?)(?:_|$)", RegexOptions.IgnoreCase);
                        if (app.Success) application.Add(new Label { Offset = descriptionOffset + app.Groups[1].Index, Text = app.Groups[1].Value.Replace('_', ' ') });
                        if (!app.Success)
                        {
                            // Gen1 descriptions use hyphens and insert engine/platform variants
                            // between the engine and chassis. Preserve those tokens as evidence.
                            Match legacy = Regex.Match(description, @"(?:^|-)((?:[BSNM]\d{2}[A-Z0-9]*)(?:-[A-Z0-9]+){0,6}-(?:[EFGUIT]\d{2,3}))(?=-|_|$)");
                            if (legacy.Success)
                            {
                                application.Add(new Label { Offset = descriptionOffset + legacy.Groups[1].Index, Text = legacy.Groups[1].Value });
                                Match drive = Regex.Match(description, @"(?<=-)(?:RWD|AWD|XDRIVE)(?=-|_|$)");
                                if (drive.Success) application.Add(new Label { Offset = descriptionOffset + drive.Index, Text = drive.Value });
                            }
                        }
                        Match region = Regex.Match(description, @"[-_](ROW|CUSA|USA(?:_KOR)?|KOR|ECE|CHN|JPN|EU)[-_](EU\d[A-Z0-9]*|ULEV\d*|SULEV\d*|LEV\d*|CN\d[A-Z0-9]*)(?:[-_]|$)");
                        if (region.Success) market.Add(new Label { Offset = descriptionOffset + region.Groups[1].Index, Text = region.Groups[1].Value.Replace('_', '/') + " / " + region.Groups[2].Value });
                        foreach (Match power in Regex.Matches(description, @"\d+(?:\.\d+)?(?:kW|Nm)(?![A-Za-z0-9])"))
                            output.Add(new Label { Offset = descriptionOffset + power.Index, Text = power.Value });
                    }
                    if (role == "PST")
                    {
                        if (description.StartsWith("EPST:", StringComparison.Ordinal))
                        {
                            description = description.Substring(5);
                            descriptionOffset += 5;
                        }
                        software.Add(new Label { Offset = descriptionOffset, Text = description });
                        string tail = label.Text.Substring(match.Index + match.Length);
                        Match rel = Regex.Match(tail, @"^#((?:[A-Z]\d{2}[A-Z0-9]_)*[A-Z]\d{2}[A-Z0-9]-\d{2}-\d{2}-\d{3})");
                        if (rel.Success) release.Add(new Label { Offset = label.Offset + match.Index + match.Length + rel.Groups[1].Index, Text = rel.Groups[1].Value });
                    }
                    if (role == "HWE" || role == "HWA")
                    {
                        int header = marker - 10;
                        byte type = role == "HWE" ? (byte)1 : (byte)2;
                        if (ValidRecord(data, header, type))
                            components[role == "HWE" ? "HWEL" : "HWAP"].Add(Record(data, header));
                    }
                    else
                    {
                        byte type = role == "BTL" ? (byte)6 : role == "PST" ? (byte)8 : (byte)13;
                        // Description records follow a count and N eight-byte dependency records.
                        // A 64-byte signature header is 0x10c bytes before that dependency preamble.
                        for (int count = 0; count <= 16; count++)
                        {
                            int preamble = marker - 2 - count * 8;
                            int header = preamble - 0x10c;
                            if (!ValidRecord(data, header, type) || preamble < 0 || data[preamble + 1] != count) continue;
                            // Gen1 stores the signature-length word big-endian; Gen2 uses little-endian.
                            bool littleEndianLength = data[header + 8] == 0x40 && data[header + 9] == 0 && data[header + 10] == 0 && data[header + 11] == 0;
                            bool bigEndianLength = data[header + 8] == 0 && data[header + 9] == 0 && data[header + 10] == 0 && data[header + 11] == 0x40;
                            if (!littleEndianLength && !bigEndianLength) continue;
                            bool validDependencies = true;
                            for (int d = 0; d < count; d++)
                            {
                                int start = preamble + 2 + d * 8;
                                byte dep = data[start];
                                if ((dep != 1 && dep != 2 && dep != 6 && dep != 8 && dep != 13) || !ValidRecord(data, start, dep)) validDependencies = false;
                            }
                            if (validDependencies) components[role == "BTL" ? "BTLD" : role == "PST" ? "SWFL" : "SWFK"].Add(Record(data, header));
                        }
                    }
                }
            }
            AddValues(report, "Engine/application label", application);
            AddValues(report, "Engine variant label", engineVariants);
            AddValues(report, "Electrical system label", electrical);
            AddValues(report, "Market/emissions label", market);
            AddValues(report, "Nominal output labels", output);
            AddValues(report, "Software identifier", software);
            AddValues(report, "Embedded release label", release);
            AddValues(report, "Full calibration label", calibration);
            AddValues(report, "Calibration identifier", calibrationIds);
            foreach (var component in components) AddValues(report, component.Key, component.Value.Select(x => new Label { Offset = x.Offset, Text = component.Key + "_" + x.Text }));
            AddMatches(report, "Modification markers", labels, @"CADE-Unlock-v[\d.]+|MHDun|MHD\+ v\d+|(?<![A-Za-z])ATAT(?![A-Za-z])");
            return report;
        }

        private static bool ValidRecord(byte[] data, int offset, byte type)
        {
            if (offset < 0 || offset > data.Length - 12 || data[offset] != type) return false;
            uint id = ((uint)data[offset + 1] << 24) | ((uint)data[offset + 2] << 16) | ((uint)data[offset + 3] << 8) | data[offset + 4];
            return id != 0 && id != uint.MaxValue;
        }

        private static Label Record(byte[] data, int start)
        {
            return new Label { Offset = start, Text = BitConverter.ToString(data, start + 1, 4).Replace("-", "") + "_" +
                data[start + 5].ToString("D3") + "_" + data[start + 6].ToString("D3") + "_" + data[start + 7].ToString("D3") };
        }

        private static IEnumerable<Label> PrintableLabels(byte[] data)
        {
            int start = 0;
            for (int i = 0; i <= data.Length; i++)
            {
                if (i < data.Length && data[i] >= 32 && data[i] <= 126) continue;
                int length = i - start;
                // Bound regex work for arbitrary files, retaining overlap across long printable runs.
                if (length >= 8)
                    for (int part = start; part < i; part += 3072)
                        yield return new Label { Offset = part, Text = Encoding.ASCII.GetString(data, part, Math.Min(4096, i - part)) };
                start = i + 1;
            }
        }

        private static void AddMatches(BinMetadataReport report, string field, IEnumerable<Label> labels, string pattern)
        {
            AddValues(report, field, labels.SelectMany(label => Regex.Matches(label.Text, pattern).Cast<Match>()
                .Select(match => new Label { Offset = label.Offset + match.Index, Text = match.Value })));
        }

        private static void AddValues(BinMetadataReport report, string field, IEnumerable<Label> values)
        {
            var groups = values.GroupBy(x => x.Text).Take(33).ToList();
            if (groups.Count == 0) { Add(report, field, "Not found", "No recognized metadata"); return; }
            foreach (var group in groups.Take(32))
                Add(report, field, group.Key, "File offset(s): " + string.Join(", ", group.Select(x => x.Offset).Distinct().OrderBy(x => x).Take(16).Select(x => "0x" + x.ToString("X"))));
            if (groups.Count > 32) Add(report, field, "Additional matches omitted", "Too many candidates; inspect the file manually");
        }

        private static void Add(BinMetadataReport report, string field, string value, string source)
        {
            report.Rows.Add(new BinMetadataRow { Field = field, Value = value, Source = source });
        }
    }
}
