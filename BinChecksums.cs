using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BMWIRomPatcher
{
    public enum BinGeneration { Unknown, Gen1, Gen2 }

    public sealed class BinCrcRegion : IEquatable<BinCrcRegion>
    {
        public int ChecksumOffset { get; private set; }
        public int Start { get; private set; }
        public int End { get; private set; }

        public BinCrcRegion(int checksumOffset, int start, int end)
        {
            ChecksumOffset = checksumOffset;
            Start = start;
            End = end;
        }

        public bool Equals(BinCrcRegion other)
        {
            return other != null && ChecksumOffset == other.ChecksumOffset && Start == other.Start && End == other.End;
        }

        public override bool Equals(object other)
        {
            return Equals(other as BinCrcRegion);
        }

        public override int GetHashCode()
        {
            return ChecksumOffset ^ Start ^ End;
        }
    }

    public sealed class BinCrcResult
    {
        public byte[] Data { get; private set; }
        public IReadOnlyList<string> Messages { get; private set; }
        public int CorrectedCount { get; private set; }
        public int SkippedCount { get; private set; }

        public BinCrcResult(byte[] data, IReadOnlyList<string> messages, int correctedCount, int skippedCount)
        {
            Data = data;
            Messages = messages;
            CorrectedCount = correctedCount;
            SkippedCount = skippedCount;
        }
    }

    public sealed class BinCrcCheckResult
    {
        public IReadOnlyList<string> Messages { get; private set; }
        public bool IsValid { get; private set; }
        public int PassedCount { get; private set; }
        public int FailedCount { get; private set; }
        public int CheckedCount { get; private set; }

        public BinCrcCheckResult(IReadOnlyList<string> messages, bool isValid, int passedCount, int failedCount, int checkedCount)
        {
            Messages = messages;
            IsValid = isValid;
            PassedCount = passedCount;
            FailedCount = failedCount;
            CheckedCount = checkedCount;
        }
    }

    public static class BinChecksums
    {
        // Inclusive byte offsets supplied for the full IROM file layout.
        public static IReadOnlyList<BinCrcRegion> Gen2Regions { get; } = Array.AsReadOnly(new[]
        {
            new BinCrcRegion(0x00BFF8, 0x000120, 0x00BFF7),
            new BinCrcRegion(0x017FF8, 0x010100, 0x017FF7),
            new BinCrcRegion(0x05FCF8, 0x028100, 0x05FCF7),
            new BinCrcRegion(0x05FFDC, 0x028100, 0x05F7DF),
            new BinCrcRegion(0x05FFFC, 0x05FFE0, 0x05FFFB),
            new BinCrcRegion(0x6FFCF8, 0x080100, 0x6FFCF7),
            new BinCrcRegion(0x6FFFDC, 0x080100, 0x6FF7DF),
            new BinCrcRegion(0x6FFFFC, 0x6FFFE0, 0x6FFFFB),
            new BinCrcRegion(0x7FFCF8, 0x700100, 0x7FFCF7),
            new BinCrcRegion(0x7FFFDC, 0x700100, 0x7FF7DF),
            new BinCrcRegion(0x7FFFFC, 0x7FFFE0, 0x7FFFFB)
        });

        public static IReadOnlyList<BinCrcRegion> Gen1Regions { get; } = Array.AsReadOnly(new[]
        {
            new BinCrcRegion(0x00BFF8, 0x000120, 0x00BFF7),
            new BinCrcRegion(0x017FF8, 0x010100, 0x017FF7),
            new BinCrcRegion(0x06A958, 0x040100, 0x06A957),
            new BinCrcRegion(0x07FFFC, 0x07FFE0, 0x07FFFB),
            new BinCrcRegion(0x6BFCF8, 0x080100, 0x6BFCF7),
            new BinCrcRegion(0x6BFFFC, 0x6BFFE0, 0x6BFFFB),
            new BinCrcRegion(0x7BFCF8, 0x6C0100, 0x7BFCF7),
            new BinCrcRegion(0x7BFFFC, 0x7BFFE0, 0x7BFFFB)
        });

        // No definitions for the unverified Gen1 fields 07FFDC, 6BFFDC, 7BFFDC.
        public static BinGeneration FromDetection(string detection)
        {
            if (detection == "Gen1 ORI/IROM") return BinGeneration.Gen1;
            if (detection == "Gen2 ORI/IROM") return BinGeneration.Gen2;
            throw new InvalidDataException("No supported BIN generation detected; save cancelled.");
        }

        public static IReadOnlyList<BinCrcRegion> RegionsFor(BinGeneration generation)
        {
            if (generation == BinGeneration.Gen1) return Gen1Regions;
            if (generation == BinGeneration.Gen2) return Gen2Regions;
            throw new InvalidDataException("Unknown BIN generation; save cancelled.");
        }

        private static uint ReadStored(byte[] bytes, int offset, BinGeneration generation)
        {
            uint result = 0;
            for (int i = 0; i < 4; i++)
                result |= (uint)bytes[offset + i] << (generation == BinGeneration.Gen1 ? (3 - i) * 8 : i * 8);
            return result;
        }

        private static void WriteStored(byte[] bytes, int offset, uint crc, BinGeneration generation)
        {
            for (int i = 0; i < 4; i++)
                bytes[offset + i] = (byte)(crc >> (generation == BinGeneration.Gen1 ? (3 - i) * 8 : i * 8));
        }

        private static readonly uint[] Table = MakeTable();

        private static uint[] MakeTable()
        {
            var table = new uint[256];
            for (uint i = 0; i < table.Length; i++)
            {
                uint value = i;
                for (int bit = 0; bit < 8; bit++)
                    value = (value >> 1) ^ ((value & 1) != 0 ? 0xEDB88320u : 0u);
                table[i] = value;
            }
            return table;
        }

        // CRC-32/ISO-HDLC: reflected polynomial EDB88320, init/xorout FFFFFFFF.
        public static uint Calculate(byte[] bytes, int offset, int length)
        {
            uint crc = 0xFFFFFFFF;
            if (bytes == null || offset < 0 || length < 0 || (long)offset + length > bytes.Length)
                throw new ArgumentOutOfRangeException();

            for (int i = offset; i < offset + length; i++)
                crc = (crc >> 8) ^ Table[(crc ^ bytes[i]) & 0xFF];

            return ~crc;
        }

        private static bool Fits(BinCrcRegion region, int length) =>
            region.Start >= 0 &&
            region.End >= region.Start &&
            region.End < length &&
            region.ChecksumOffset >= 0 &&
            (long)region.ChecksumOffset + 4 <= length;

        private static bool CoversChecksum(BinCrcRegion cover, BinCrcRegion stored)
        {
            long storedStart = stored.ChecksumOffset;
            long storedEnd = storedStart + 3;
            return cover.Start <= storedStart && storedEnd <= cover.End;
        }

        public static BinCrcCheckResult Check(byte[] input, BinGeneration generation, IEnumerable<BinCrcRegion> verifiedDescriptors = null)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));

            var messages = new List<string>
            {
                $"CRC check: detected {generation}; {(generation == BinGeneration.Gen1 ? "big-endian" : "little-endian")} storage."
            };
            var regions = new List<BinCrcRegion>();
            int passedCount = 0;
            int failedCount = 0;

            foreach (var region in RegionsFor(generation))
            {
                if (Fits(region, input.Length))
                    regions.Add(region);
                else
                    messages.Add($"CRC check: SKIPPED 0x{region.ChecksumOffset:X6} / range 0x{region.Start:X6}–0x{region.End:X6}: outside this {input.Length}-byte BIN.");
            }

            foreach (var descriptor in verifiedDescriptors ?? Array.Empty<BinCrcRegion>())
            {
                if (!Fits(descriptor, input.Length))
                    throw new InvalidDataException("Verified checksum descriptor is outside the BIN; check cancelled.");
                if (!regions.Contains(descriptor))
                    regions.Add(descriptor);
            }

            foreach (var region in regions)
            {
                uint stored = ReadStored(input, region.ChecksumOffset, generation);
                uint computed = Calculate(input, region.Start, region.End - region.Start + 1);
                bool matches = stored == computed;
                if (matches) passedCount++;
                else failedCount++;
                messages.Add($"CRC check: 0x{region.ChecksumOffset:X6} / range 0x{region.Start:X6}–0x{region.End:X6}: " +
                    $"stored={stored:X8} computed={computed:X8} ({(matches ? "OK" : "MISMATCH")}).");
            }

            messages.Add($"CRC check: {(failedCount == 0 ? "PASSED" : "FAILED")}; " +
                $"{passedCount} passed, {failedCount} failed; {regions.Count} region(s) checked.");
            return new BinCrcCheckResult(messages.AsReadOnly(), failedCount == 0, passedCount, failedCount, regions.Count);
        }

        public static BinCrcResult Correct(byte[] input, BinGeneration generation, IEnumerable<BinCrcRegion> verifiedDescriptors = null)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));

            var fixedRegions = RegionsFor(generation);
            var descriptors = verifiedDescriptors?.ToArray() ?? Array.Empty<BinCrcRegion>();
            if (generation == BinGeneration.Gen1 && descriptors.Length != 0)
                throw new InvalidDataException("Gen2 checksum descriptors cannot be used for a Gen1 BIN; save cancelled.");

            var messages = new List<string>
            {
                $"CRC save: detected {generation}; {fixedRegions.Count} configured regions; {(generation == BinGeneration.Gen1 ? "big-endian" : "little-endian")} storage."
            };

            var regions = new List<BinCrcRegion>();
            int skipped = 0;

            foreach (var region in fixedRegions)
            {
                if (Fits(region, input.Length))
                    regions.Add(region);
                else
                {
                    skipped++;
                    messages.Add($"CRC save: SKIPPED 0x{region.ChecksumOffset:X6} / range 0x{region.Start:X6}–0x{region.End:X6}: outside this {input.Length}-byte BIN.");
                }
            }

            foreach (var descriptor in descriptors)
            {
                if (!Fits(descriptor, input.Length))
                    throw new InvalidDataException("Verified checksum descriptor is outside the BIN; save cancelled.");
                if (regions.Contains(descriptor)) continue;
                regions.Add(descriptor);
            }

            if (regions.Count == 0)
                throw new InvalidDataException("BIN is too short for any configured checksum region; save cancelled.");

            for (int i = 0; i < regions.Count; i++)
            {
                if (CoversChecksum(regions[i], regions[i]))
                    throw new InvalidDataException("Checksum region covers its own stored CRC; save cancelled.");

                for (int j = i + 1; j < regions.Count; j++)
                    if (Math.Abs((long)regions[i].ChecksumOffset - regions[j].ChecksumOffset) < 4)
                        throw new InvalidDataException($"Conflicting checksum definitions at 0x{regions[i].ChecksumOffset:X6}; save cancelled.");
            }

            // Calculate inner stored values before any descriptor that covers their bytes.
            var pending = regions.ToList();
            var ordered = new List<BinCrcRegion>();
            while (pending.Count > 0)
            {
                var next = pending.FirstOrDefault(region => !pending.Any(other => other != region && CoversChecksum(region, other)));
                if (next is null)
                    throw new InvalidDataException("Cyclic checksum dependencies; save cancelled.");

                ordered.Add(next);
                pending.Remove(next);
            }

            byte[] result = input.ToArray();
            foreach (var region in ordered)
            {
                uint before = ReadStored(result, region.ChecksumOffset, generation);
                uint after = Calculate(result, region.Start, region.End - region.Start + 1);
                WriteStored(result, region.ChecksumOffset, after, generation);
                messages.Add($"CRC save: 0x{region.ChecksumOffset:X6} / range 0x{region.Start:X6}–0x{region.End:X6}: {before:X8} -> {after:X8} ({(before == after ? "unchanged" : "corrected")}).");
            }

            foreach (var region in ordered)
            {
                uint stored = ReadStored(result, region.ChecksumOffset, generation);
                if (stored != Calculate(result, region.Start, region.End - region.Start + 1))
                    throw new InvalidDataException($"Final CRC verification failed at 0x{region.ChecksumOffset:X6}; save cancelled.");
            }

            messages.Add($"CRC save: {ordered.Count} region(s) calculated and verified; {skipped} configured region(s) skipped.");
            return new BinCrcResult(result, messages.AsReadOnly(), ordered.Count, skipped);
        }

        public static BinCrcResult Save(string path, byte[] input, BinGeneration generation, IEnumerable<BinCrcRegion> verifiedDescriptors = null)
        {
            var corrected = Correct(input, generation, verifiedDescriptors);
            string destination = Path.GetFullPath(path);
            string temporary = Path.Combine(Path.GetDirectoryName(destination), ".crc-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                File.WriteAllBytes(temporary, corrected.Data);
                if (!File.ReadAllBytes(temporary).SequenceEqual(corrected.Data))
                    throw new IOException("Written BIN verification failed.");
                if (File.Exists(destination)) File.Replace(temporary, destination, null);
                else File.Move(temporary, destination);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
            return corrected;
        }
    }
}
