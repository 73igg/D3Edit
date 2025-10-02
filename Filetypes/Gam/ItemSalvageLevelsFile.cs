using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using D3Edit.Core;

namespace D3Edit.Filetypes.Gam
{
    public static class ItemSalvageLevelsIO
    {
        public static ItemSalvageLevelsJsonFile ReadGamFile(string filePath)
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
                if (blockLen <= 0) throw new InvalidDataException("ItemSalvageLevels block pointer invalid.");
            }

            int detectedPreamble = DetectZeroRun(br, blockOff, 32);

            var strideCandidates = new[] { 16, 20, 24, 32, 40, 48, 64 };

            long startAssuming16 = blockOff + 16;
            long endFromDir = Math.Min(fs.Length, (long)blockOff + blockLen);
            int stride = GuessStride(endFromDir - startAssuming16, strideCandidates);

            int preamble = detectedPreamble;
            if (stride == 0)
            {
                long startDetected = blockOff + detectedPreamble;
                stride = GuessStride(endFromDir - startDetected, strideCandidates);

                if (stride == 0)
                {
                    preamble = detectedPreamble >= 16 ? 16 : detectedPreamble;
                    stride = 16;
                }
                else
                {
                    preamble = detectedPreamble;
                }
            }
            else
            {
                preamble = 16;
            }

            long dataStart = blockOff + preamble;
            long dataEnd = Math.Min(fs.Length, (long)blockOff + blockLen);

            long usable = Math.Max(0, dataEnd - dataStart);
            long wholeBytes = usable - usable % stride;
            dataEnd = dataStart + wholeBytes;

            if (dataStart > dataEnd) dataStart = dataEnd; // nothing to read

            fs.Position = dataStart;

            var recs = new List<ItemSalvageLevelRecord>();
            while (fs.Position + stride <= dataEnd)
            {
                long rowStart = fs.Position;

                if (dataEnd - fs.Position < 16) break; // safety check
                var rec = new ItemSalvageLevelRecord
                {
                    TreasureClassSNO0 = br.ReadInt32(),
                    TreasureClassSNO1 = br.ReadInt32(),
                    TreasureClassSNO2 = br.ReadInt32(),
                    TreasureClassSNO3 = br.ReadInt32(),
                };
                recs.Add(rec);

                long toSkip = stride - 16;
                if (toSkip > 0)
                {
                    long newPos = rowStart + stride;
                    if (newPos > dataEnd) break; // don't run past end
                    fs.Position = newPos;
                }
            }

            var outHeader = header;
            outHeader.BalanceType = balanceType;
            outHeader.I0 = i0;
            outHeader.I1 = i1;

            return new ItemSalvageLevelsJsonFile { Header = outHeader, Records = recs };
        }

        public static void WriteGamFile(string filePath, ItemSalvageLevelsJsonFile data)
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

            foreach (var r in data.Records ?? Enumerable.Empty<ItemSalvageLevelRecord>())
            {
                bw.Write(r?.TreasureClassSNO0 ?? 0);
                bw.Write(r?.TreasureClassSNO1 ?? 0);
                bw.Write(r?.TreasureClassSNO2 ?? 0);
                bw.Write(r?.TreasureClassSNO3 ?? 0);
            }

            long end = fs.Position;

            long save = fs.Position;
            fs.Position = 0x230 + 4;
            bw.Write(checked((int)(end - BLOCK_OFF)));
            fs.Position = save;
        }

        private static int GuessStride(long usableBytes, int[] candidates)
        {
            if (usableBytes <= 0) return 0;
            foreach (var c in candidates)
            {
                if (c > 0 && usableBytes % c == 0) return c;
            }
            return 0;
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
                if (zeros >= 17) return 16;
                return zeros;
            }
            finally { s.Position = saved; }
        }
    }

    public class ItemSalvageLevelsJsonFile
    {
        public Header Header { get; set; } = Header.Default();
        public List<ItemSalvageLevelRecord> Records { get; set; } = new List<ItemSalvageLevelRecord>();
    }

    public class ItemSalvageLevelRecord
    {
        public int TreasureClassSNO0 { get; set; }
        public int TreasureClassSNO1 { get; set; }
        public int TreasureClassSNO2 { get; set; }
        public int TreasureClassSNO3 { get; set; }
    }
}
