using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using D3Edit.Core;

namespace D3Edit.Filetypes.Gam
{
    public static class ParagonBonusesIO
    {
        public static ParagonBonusesJsonFile ReadGamFile(string filePath)
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var br = new BinaryReader(fs, Encoding.UTF8, leaveOpen: false);

            var header = Header.Read(br);
            int balanceType = br.ReadInt32();
            int i0 = br.ReadInt32();
            int i1 = br.ReadInt32();

            int fileSize = checked((int)fs.Length);
            int blockOff = 0, blockLen = 0;

            if (fileSize >= 0x230 + 8)
            {
                long save = fs.Position;
                fs.Position = 0x230;
                int off = br.ReadInt32();
                int len = br.ReadInt32();
                fs.Position = save;
                if (off > 0 && len > 0 && off + len <= fileSize)
                {
                    blockOff = off;
                    blockLen = len;
                }
            }
            if (blockOff == 0 || blockLen <= 0)
            {
                blockOff = 0x238;
                blockLen = fileSize - blockOff;
                if (blockLen <= 0) throw new InvalidDataException("ParagonBonuses block pointer invalid.");
            }

            int detectedPreamble = DetectPreamble(br, blockOff, 32);
            int preamble = detectedPreamble >= 16 ? detectedPreamble : 16;
            long tryStart = blockOff + preamble;

            int chosenA = 0, stride = 0;
            (chosenA, stride) = PickByNameHeuristic(br, tryStart, fileSize);
            if (stride == 0)
            {
                chosenA = 24;
                stride = 544 + 4 * chosenA; // 256+16 + 4*A*4 + 12 + 256 + 4 = 544 + 4A
            }

            long blockEnd = Math.Min(fs.Length, blockOff + blockLen);
            long dataStart = tryStart;
            long usableBytes = blockEnd - dataStart;
            long whole = usableBytes - usableBytes % stride;
            long dataEnd = dataStart + whole;

            fs.Position = dataStart;
            var recs = new List<ParagonBonusRecord>();
            while (fs.Position + stride <= dataEnd)
            {
                long recStart = fs.Position;
                string name = ReadFixedCString(br, 256);
                int hash = br.ReadInt32();
                int I1 = br.ReadInt32();
                int I2 = br.ReadInt32();
                br.ReadInt32(); // pad

                var specs = new byte[4][];
                for (int i = 0; i < 4; i++) specs[i] = br.ReadBytes(chosenA);

                int category = br.ReadInt32();
                int index = br.ReadInt32();
                int heroClass = br.ReadInt32();
                string icon = ReadFixedCString(br, 256);
                br.ReadInt32(); // trailing pad

                recs.Add(new ParagonBonusRecord
                {
                    Name = name ?? string.Empty,
                    Hash = hash,
                    I1 = I1,
                    I2 = I2,
                    AttributeSpecifiers = specs,
                    Category = category,
                    Index = index,
                    HeroClass = heroClass,
                    IconName = icon ?? string.Empty
                });

                fs.Position = recStart + stride;
            }

            var outHeader = header;
            outHeader.BalanceType = balanceType;
            outHeader.I0 = i0;
            outHeader.I1 = i1;

            return new ParagonBonusesJsonFile { Header = outHeader, AttrSize = chosenA, Records = recs };
        }

        public static void WriteGamFile(string filePath, ParagonBonusesJsonFile data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (data.Header == null) throw new InvalidDataException("Header is required in JSON.");

            using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
            using var bw = new BinaryWriter(fs, Encoding.UTF8, leaveOpen: false);

            Header.Write(bw, data.Header);
            bw.Write(data.Header.BalanceType);
            bw.Write(data.Header.I0);
            bw.Write(data.Header.I1);

            if (fs.Position > 0x230) throw new InvalidDataException("Header too large for fixed layout.");
            while (fs.Position < 0x230) bw.Write((byte)0);

            const int BLOCK_OFF = 0x238;
            bw.Write(BLOCK_OFF);
            bw.Write(0); // backfilled later

            while (fs.Position < BLOCK_OFF) bw.Write((byte)0);
            bw.Write(new byte[16]); // preamble -> first record at 0x248

            int A = data.AttrSize > 0 ? data.AttrSize : data.Records?.FirstOrDefault()?.AttributeSpecifiers?.FirstOrDefault()?.Length ?? 24;

            foreach (var r in data.Records ?? Enumerable.Empty<ParagonBonusRecord>())
            {
                WriteFixedCString(bw, r?.Name ?? string.Empty, 256);
                int h = r?.Hash ?? 0;
                if (h == 0) h = HashItemName(r?.Name ?? string.Empty);
                bw.Write(h);
                bw.Write(r?.I1 ?? 0);
                bw.Write(r?.I2 ?? 0);
                bw.Write(0); // pad

                var specs = r?.AttributeSpecifiers ?? Array.Empty<byte[]>();
                for (int i = 0; i < 4; i++)
                {
                    var blob = i < specs.Length && specs[i] != null ? specs[i] : Array.Empty<byte>();
                    if (blob.Length == A)
                    {
                        bw.Write(blob);
                    }
                    else if (blob.Length > A)
                    {
                        bw.Write(blob, 0, A);
                    }
                    else
                    {
                        bw.Write(blob);
                        Bin.WriteZeros(fs, A - blob.Length);
                    }
                }

                bw.Write(r?.Category ?? 0);
                bw.Write(r?.Index ?? 0);
                bw.Write(r?.HeroClass ?? 0);
                WriteFixedCString(bw, r?.IconName ?? string.Empty, 256);
                bw.Write(0); // trailing pad
            }

            long end = fs.Position;

            long save = fs.Position;
            fs.Position = 0x230 + 4;
            bw.Write(checked((int)(end - BLOCK_OFF)));
            fs.Position = save;
        }

        private static (int A, int stride) PickByNameHeuristic(BinaryReader br, long start, int fileSize)
        {
            var s = br.BaseStream;
            string n0 = PeekName(br, start);
            if (!IsLikelyName(n0)) return (0, 0);

            int[] candidateA = new[] { 24, 28, 32, 36, 40, 44, 48, 52, 56, 60, 72, 84, 96, 108, 120 };
            foreach (var a in candidateA)
            {
                int stride = 544 + 4 * a;
                if (start + stride + 256 > fileSize) continue;
                var n1 = PeekName(br, start + stride);
                if (IsLikelyName(n1)) return (a, stride);
            }
            return (0, 0);
        }

        private static string PeekName(BinaryReader br, long pos)
        {
            var s = br.BaseStream;
            long saved = s.Position;
            try
            {
                s.Position = pos;
                return ReadFixedCString(br, 256);
            }
            finally { s.Position = saved; }
        }

        private static bool IsLikelyName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            int n = name.Length;
            int printable = 0;
            foreach (char ch in name)
            {
                if (ch >= 0x20 && ch <= 0x7E && ch != '\\' && ch != '"') printable++;
            }
            return n > 0 && printable >= Math.Max(3, (int)(0.8 * n));
        }

        private static int DetectPreamble(BinaryReader br, long start, int maxInspect)
        {
            var s = br.BaseStream;
            long saved = s.Position;
            try
            {
                s.Position = start;
                int zeros = 0;
                for (int i = 0; i < maxInspect; i++)
                {
                    int b = s.ReadByte();
                    if (b < 0) break;
                    if (b == 0) zeros++;
                    else break;
                }
                if (zeros >= 17) return 17;
                if (zeros >= 16) return 16;
                return 0;
            }
            finally { s.Position = saved; }
        }

        private static string ReadFixedCString(BinaryReader r, int size)
        {
            var bytes = r.ReadBytes(size);
            int n = Array.IndexOf<byte>(bytes, 0);
            if (n < 0) n = bytes.Length;
            return Encoding.UTF8.GetString(bytes, 0, n);
        }

        private static void WriteFixedCString(BinaryWriter w, string value, int size)
        {
            if (size <= 0) return;
            var src = value ?? string.Empty;
            var bytes = Encoding.UTF8.GetBytes(src);
            if (bytes.Length >= size)
            {
                w.Write(bytes, 0, size - 1);
                w.Write((byte)0);
            }
            else
            {
                w.Write(bytes);
                w.Write((byte)0);
                int pad = size - (bytes.Length + 1);
                if (pad > 0) Bin.WriteZeros(w.BaseStream, pad);
            }
        }

        private static int HashItemName(string s)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            unchecked
            {
                int h = 0;
                foreach (char ch in s.ToLowerInvariant())
                    h = h * 0x1003F + ch;
                return h;
            }
        }
    }

    public class ParagonBonusesJsonFile
    {
        public Header Header { get; set; } = Header.Default();
        public int AttrSize { get; set; } = 24;
        public List<ParagonBonusRecord> Records { get; set; } = new List<ParagonBonusRecord>();
    }

    public class ParagonBonusRecord
    {
        public string Name { get; set; } = string.Empty; // 256 bytes, null-terminated
        public int Hash { get; set; }                    // on-disk hash; writer recomputes only if 0
        public int I1 { get; set; }
        public int I2 { get; set; }
        public byte[][] AttributeSpecifiers { get; set; } = Array.Empty<byte[]>(); // 4 entries of A bytes
        public int Category { get; set; }
        public int Index { get; set; }
        public int HeroClass { get; set; }              // numeric enum value
        public string IconName { get; set; } = string.Empty; // 256 bytes, null-terminated
    }
}
