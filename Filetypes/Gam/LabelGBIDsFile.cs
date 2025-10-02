using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using D3Edit.Core;

namespace D3Edit.Filetypes.Gam
{
    public static class LabelGBIDsIO
    {
        public static LabelGBIDsJsonFile ReadGamFile(string filePath)
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
                if (off > 0 && len >= 0 && (long)off + len <= fileSize)
                {
                    blockOff = off;
                    blockLen = len;
                }
            }
            if (blockOff == 0)
            {
                blockOff = 0x238;
                blockLen = Math.Max(0, fileSize - blockOff);
                if (blockLen <= 0) throw new InvalidDataException("LabelGBIDs block pointer invalid.");
            }

            int detectedPreamble = DetectPreamble(br, blockOff, 32);

            var candidates = new[] { 272, 264, 276, 280 };

            long startAssuming16 = blockOff + 16;
            long endFromDir = Math.Min(fs.Length, (long)blockOff + blockLen);
            int stride = GuessStride(endFromDir - startAssuming16, candidates);
            int preamble = stride != 0 ? 16 : detectedPreamble;

            if (stride == 0)
            {
                long startDetected = blockOff + detectedPreamble;
                stride = GuessStride(endFromDir - startDetected, candidates);
                if (stride == 0)
                {
                    stride = 272;
                    preamble = detectedPreamble >= 16 ? 16 : detectedPreamble;
                }
                else
                {
                    preamble = detectedPreamble;
                }
            }

            long dataStart = blockOff + preamble;
            long dataEnd = endFromDir;

            long usable = Math.Max(0, dataEnd - dataStart);
            long whole = usable - usable % stride;
            dataEnd = dataStart + whole;

            fs.Position = dataStart;

            var recs = new List<LabelGBIDRecord>();
            while (fs.Position + stride <= dataEnd)
            {
                long rowStart = fs.Position;
                recs.Add(ReadOne(br));
                long toSkip = stride - 272;
                if (toSkip > 0)
                {
                    long newPos = rowStart + stride;
                    if (newPos > dataEnd) break;
                    fs.Position = newPos;
                }
            }

            var outHeader = header;
            outHeader.BalanceType = balanceType;
            outHeader.I0 = i0;
            outHeader.I1 = i1;

            return new LabelGBIDsJsonFile { Header = outHeader, Records = recs };
        }

        public static void WriteGamFile(string filePath, LabelGBIDsJsonFile data)
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
            bw.Write(0);

            while (fs.Position < BLOCK_OFF) bw.Write((byte)0);
            bw.Write(new byte[0x10]);

            long start = fs.Position;
            foreach (var r in data.Records ?? Enumerable.Empty<LabelGBIDRecord>())
                WriteOne(bw, r);
            long end = fs.Position;

            long save = fs.Position;
            fs.Position = 0x230 + 4;
            bw.Write(checked((int)(end - BLOCK_OFF)));
            fs.Position = save;
        }

        private static LabelGBIDRecord ReadOne(BinaryReader s)
        {
            var nameBytes = s.ReadBytes(256);
            int nul = Array.IndexOf(nameBytes, (byte)0);
            if (nul < 0) nul = nameBytes.Length;
            string name = Encoding.UTF8.GetString(nameBytes, 0, nul);

            return new LabelGBIDRecord
            {
                Name = name,
                I0 = s.ReadInt32(),
                I1 = s.ReadInt32(),
                I2 = s.ReadInt32(),
                I3 = s.ReadInt32(),
            };
        }

        private static void WriteOne(BinaryWriter w, LabelGBIDRecord r)
        {
            long start = w.BaseStream.Position;

            var name = r?.Name ?? string.Empty;
            var bytes = Encoding.UTF8.GetBytes(name);
            if (bytes.Length >= 256) bytes = bytes.Take(255).ToArray();
            w.Write(bytes);
            if (bytes.Length < 256)
            {
                w.Write((byte)0);
                int pad = 256 - (bytes.Length + 1);
                if (pad > 0) w.Write(new byte[pad]);
            }

            w.Write(r?.I0 ?? 0);
            w.Write(r?.I1 ?? 0);
            w.Write(r?.I2 ?? 0);
            w.Write(r?.I3 ?? 0);

            const int Canonical = 272;
            long wrote = w.BaseStream.Position - start;
            if (wrote > Canonical)
                throw new InvalidDataException($"LabelGBID record overflow ({wrote} > {Canonical}).");
            if (wrote < Canonical)
                w.Write(new byte[Canonical - wrote]);
        }

        private static int GuessStride(long usable, int[] candidates)
        {
            if (usable <= 0) return 0;
            foreach (var c in candidates)
                if (c > 0 && usable % c == 0) return c;
            return 0;
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
                if (zeros >= 17) return 16;
                return zeros;
            }
            finally { s.Position = saved; }
        }
    }

    public class LabelGBIDsJsonFile
    {
        public Header Header { get; set; } = Header.Default();
        public List<LabelGBIDRecord> Records { get; set; } = new List<LabelGBIDRecord>();
    }

    public class LabelGBIDRecord
    {
        public string Name { get; set; } = "";
        public int I0 { get; set; }
        public int I1 { get; set; }
        public int I2 { get; set; }
        public int I3 { get; set; }
    }
}
