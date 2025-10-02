using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using D3Edit.Core;

namespace D3Edit.Filetypes.Gam
{
    public static class MonsterAffixesIO
    {
        public static MonsterAffixesJsonFile ReadGamFile(string filePath)
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var br = new BinaryReader(fs, Encoding.UTF8, leaveOpen: false);

            var header = Header.Read(br);
            int balanceType = br.ReadInt32(); // should be 18
            int i0 = br.ReadInt32();
            int i1 = br.ReadInt32();

            int fileSize = checked((int)fs.Length);

            int blockOff = 0, blockLen = 0;
            if (fileSize >= 0x230 + 8)
            {
                long save = fs.Position;
                fs.Position = 0x230;
                blockOff = br.ReadInt32();
                blockLen = br.ReadInt32();
                fs.Position = save;
            }
            if (blockOff <= 0 || blockLen <= 0 || (long)blockOff + blockLen > fileSize)
            {
                blockOff = 0x238;
                blockLen = Math.Max(0, fileSize - blockOff);
                if (blockLen <= 0) throw new InvalidDataException("MonsterAffixes block pointer invalid.");
            }

            int detectedPreamble = DetectZeroRun(br, blockOff, 32);

            int[] candidateA = { 24, 28, 32, 36, 40, 44, 48, 52, 56, 60 };

            int chosenA = 0, stride = 0, preamble = detectedPreamble;

            long endFromDir = Math.Min(fs.Length, (long)blockOff + blockLen);

            long tryStart = blockOff + (detectedPreamble >= 16 ? 16 : detectedPreamble);

            (int A, int s) PickByNameHeuristic(long start)
            {
                foreach (var a in candidateA)
                {
                    int sStride = 424 + 20 * a;
                    if (start + sStride + 256 > fs.Length) continue;

                    var n0 = PeekName(br, start);
                    var n1 = PeekName(br, start + sStride);

                    if (IsLikelyName(n0) && IsLikelyName(n1))
                        return (a, sStride);
                }
                return (0, 0);
            }

            (chosenA, stride) = PickByNameHeuristic(tryStart);

            if (stride == 0)
            {
                long startDetected = blockOff + detectedPreamble;
                foreach (var a in candidateA)
                {
                    int sStride = 424 + 20 * a;
                    long bytesAvail = endFromDir - startDetected;
                    if (bytesAvail >= sStride && bytesAvail / sStride > 0)
                    {
                        (chosenA, stride) = (a, sStride);
                        preamble = detectedPreamble;
                        break;
                    }
                }
            }

            if (stride == 0)
            {
                chosenA = 24;
                stride = 424 + 20 * chosenA;
                preamble = detectedPreamble >= 16 ? 16 : detectedPreamble;
            }

            long dataStart = blockOff + preamble;
            long dataEnd = endFromDir;

            long usableBytes = Math.Max(0, dataEnd - dataStart);
            long whole = usableBytes - usableBytes % stride;
            dataEnd = dataStart + whole;

            fs.Position = dataStart;
            var recs = new List<MonsterAffixRecord>();

            while (fs.Position + stride <= dataEnd)
            {
                long recStart = fs.Position;
                string name = ReadCString256(br);
                if (!IsLikelyName(name))
                {
                    fs.Position = recStart + stride;
                    break;
                }

                var r = new MonsterAffixRecord
                {
                    Name = name,
                    Hash = HashItemName(name),
                    I0 = br.ReadInt32(),
                    I1 = br.ReadInt32(),
                    I2 = br.ReadInt32(),
                    I3 = br.ReadInt32(),
                    I4 = br.ReadInt32(),
                    MonsterAffix = br.ReadInt32(),
                    Resistance = br.ReadInt32(),
                    AffixType = br.ReadInt32(),
                    I5 = br.ReadInt32(),
                    I6 = br.ReadInt32(),
                    I7 = br.ReadInt32(),
                    I8 = br.ReadInt32(),
                    Attributes = new byte[10][],
                    MinionAttributes = new byte[10][],
                };

                for (int i = 0; i < 10; i++) r.Attributes[i] = br.ReadBytes(chosenA);
                for (int i = 0; i < 10; i++) r.MinionAttributes[i] = br.ReadBytes(chosenA);

                br.BaseStream.Position += 4;

                r.SNOOnSpawnPowerMinion = br.ReadInt32();
                r.SNOOnSpawnPowerChampion = br.ReadInt32();
                r.SNOOnSpawnPowerRare = br.ReadInt32();

                r.BS = br.ReadBytes(99);

                br.BaseStream.Position += 5;

                recs.Add(r);
            }

            var outHeader = header;
            outHeader.BalanceType = balanceType;
            outHeader.I0 = i0;
            outHeader.I1 = i1;

            return new MonsterAffixesJsonFile
            {
                Header = outHeader,
                AttrSize = chosenA,
                Records = recs
            };
        }

        public static void WriteGamFile(string filePath, MonsterAffixesJsonFile data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (data.Header == null) throw new InvalidDataException("Header is required in JSON.");

            int A = data.AttrSize > 0 ? data.AttrSize : data.Records?.FirstOrDefault()?.Attributes?.FirstOrDefault()?.Length ?? 24;
            if (A <= 0) A = 24;

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

            long start = fs.Position;
            foreach (var r in data.Records ?? Enumerable.Empty<MonsterAffixRecord>())
            {
                WriteCString256(bw, r?.Name ?? "");
                bw.Write(r?.I0 ?? 0);
                bw.Write(r?.I1 ?? 0);
                bw.Write(r?.I2 ?? 0);
                bw.Write(r?.I3 ?? 0);
                bw.Write(r?.I4 ?? 0);
                bw.Write(r?.MonsterAffix ?? 0);
                bw.Write(r?.Resistance ?? 0);
                bw.Write(r?.AffixType ?? 0);
                bw.Write(r?.I5 ?? 0);
                bw.Write(r?.I6 ?? 0);
                bw.Write(r?.I7 ?? 0);
                bw.Write(r?.I8 ?? 0);

                for (int i = 0; i < 10; i++) WriteFixedBlob(bw, r?.Attributes, i, A);
                for (int i = 0; i < 10; i++) WriteFixedBlob(bw, r?.MinionAttributes, i, A);

                bw.Write(0);

                bw.Write(r?.SNOOnSpawnPowerMinion ?? 0);
                bw.Write(r?.SNOOnSpawnPowerChampion ?? 0);
                bw.Write(r?.SNOOnSpawnPowerRare ?? 0);

                var bs = r?.BS ?? Array.Empty<byte>();
                if (bs.Length != 99)
                {
                    var tmp = new byte[99];
                    Array.Copy(bs, 0, tmp, 0, Math.Min(bs.Length, tmp.Length));
                    bs = tmp;
                }
                bw.Write(bs);

                bw.Write(new byte[5]);
            }
            long end = fs.Position;

            long save = fs.Position;
            fs.Position = 0x230 + 4;
            bw.Write(checked((int)(end - BLOCK_OFF)));
            fs.Position = save;
        }

        private static int DetectZeroRun(BinaryReader br, long start, int maxBytes)
        {
            var s = br.BaseStream;
            long saved = s.Position;
            try
            {
                s.Position = start;
                int zeros = 0;
                for (int i = 0; i < maxBytes; i++)
                {
                    int b = s.ReadByte();
                    if (b < 0) break;
                    if (b == 0) zeros++;
                    else break;
                }
                return zeros;
            }
            finally { s.Position = saved; }
        }

        private static string PeekName(BinaryReader br, long pos)
        {
            var s = br.BaseStream;
            long saved = s.Position;
            try
            {
                s.Position = pos;
                return ReadCString256(br);
            }
            finally { s.Position = saved; }
        }

        private static string ReadCString256(BinaryReader br)
        {
            byte[] buf = br.ReadBytes(256);
            int n = Array.IndexOf<byte>(buf, 0);
            if (n < 0) n = buf.Length;
            return Encoding.UTF8.GetString(buf, 0, n);
        }

        private static void WriteCString256(BinaryWriter bw, string s)
        {
            var bytes = Encoding.UTF8.GetBytes(s ?? "");
            if (bytes.Length >= 256)
            {
                bw.Write(bytes, 0, 255);
                bw.Write((byte)0);
            }
            else
            {
                bw.Write(bytes);
                bw.Write(new byte[256 - bytes.Length]);
            }
        }

        private static void WriteFixedBlob(BinaryWriter bw, byte[][]? arr, int idx, int size)
        {
            var src = arr != null && idx >= 0 && idx < arr.Length ? arr[idx] : null;
            if (src == null || src.Length != size)
            {
                var pad = new byte[size];
                if (src != null) Array.Copy(src, 0, pad, 0, Math.Min(src.Length, size));
                src = pad;
            }
            bw.Write(src);
        }

        private static bool IsLikelyName(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            int printable = s.Count(ch => ch >= 32 && ch < 127);
            return printable >= Math.Max(3, (int)(0.8 * s.Length));
        }

        private static bool IsLikelyName(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return false;
            int n = Array.IndexOf(bytes, (byte)0);
            if (n < 0) n = bytes.Length;
            int printable = 0;
            for (int i = 0; i < n; i++)
            {
                byte b = bytes[i];
                if (b >= 32 && b < 127) printable++;
            }
            return n > 0 && printable >= Math.Max(3, (int)(0.8 * n));
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

    public class MonsterAffixesJsonFile
    {
        public Header Header { get; set; } = Header.Default();
        public int AttrSize { get; set; } = 24;
        public List<MonsterAffixRecord> Records { get; set; } = new List<MonsterAffixRecord>();
    }

    public class MonsterAffixRecord
    {
        public string Name { get; set; } = "";
        public int Hash { get; set; }         // convenience (Name-derived)
        public int I0 { get; set; }
        public int I1 { get; set; }
        public int I2 { get; set; }
        public int I3 { get; set; }
        public int I4 { get; set; }
        public int MonsterAffix { get; set; }
        public int Resistance { get; set; }
        public int AffixType { get; set; }
        public int I5 { get; set; }
        public int I6 { get; set; }
        public int I7 { get; set; }
        public int I8 { get; set; }

        public byte[][] Attributes { get; set; } = Array.Empty<byte[]>();
        public byte[][] MinionAttributes { get; set; } = Array.Empty<byte[]>();

        public int SNOOnSpawnPowerMinion { get; set; }
        public int SNOOnSpawnPowerChampion { get; set; }
        public int SNOOnSpawnPowerRare { get; set; }
        public byte[] BS { get; set; } = Array.Empty<byte>();
    }
}
