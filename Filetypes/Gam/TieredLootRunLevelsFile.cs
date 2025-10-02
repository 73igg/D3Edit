using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using D3Edit.Core;

namespace D3Edit.Filetypes.Gam
{
    public static class TieredLootRunLevelsIO
    {
        public static TieredLootRunLevelsJsonFile ReadGamFile(string filePath)
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
                if (off > 0 && len > 0 && off + len <= fileSize) { blockOff = off; blockLen = len; }
            }
            if (blockOff == 0 || blockLen <= 0)
            {
                blockOff = 0x238;
                blockLen = fileSize - blockOff;
                if (blockLen <= 0) throw new InvalidDataException("TieredLootRunLevels block pointer invalid.");
            }

            int preamble = DetectPreamble(br, blockOff, 32);
            fs.Position = blockOff + (preamble > 0 ? preamble : 0x10);

            const int RecordSize = 56; // 8*f32 + 2*i32 + 1*i64 + 2*f32
            long end = Math.Min(fs.Length, blockOff + blockLen);

            var recs = new List<TieredLootRunLevelRecord>();
            while (fs.Position + RecordSize <= end)
            {
                recs.Add(ReadOne(br));
            }

            var outHeader = header;
            outHeader.BalanceType = balanceType;
            outHeader.I0 = i0;
            outHeader.I1 = i1;

            return new TieredLootRunLevelsJsonFile { Header = outHeader, Records = recs };
        }

        public static void WriteGamFile(string filePath, TieredLootRunLevelsJsonFile data)
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
            bw.Write(0); // placeholder length

            while (fs.Position < BLOCK_OFF) bw.Write((byte)0);
            bw.Write(new byte[0x10]);

            foreach (var r in data.Records ?? Enumerable.Empty<TieredLootRunLevelRecord>())
                WriteOne(bw, r);

            long end = fs.Position;

            long save = fs.Position;
            fs.Position = 0x230 + 4;
            bw.Write(checked((int)(end - BLOCK_OFF)));
            fs.Position = save;
        }

        private static TieredLootRunLevelRecord ReadOne(BinaryReader s)
        {
            var r = new TieredLootRunLevelRecord();
            r.F0 = s.ReadSingle();
            r.F1 = s.ReadSingle();
            r.F2 = s.ReadSingle();
            r.F3 = s.ReadSingle();
            r.F4 = s.ReadSingle();
            r.F5 = s.ReadSingle();
            r.F6 = s.ReadSingle();
            r.F7 = s.ReadSingle();
            r.I0 = s.ReadInt32();
            r.I1 = s.ReadInt32();
            r.L0 = s.ReadInt64();
            r.F8 = s.ReadSingle();
            r.F9 = s.ReadSingle();
            return r;
        }

        private static void WriteOne(BinaryWriter w, TieredLootRunLevelRecord r)
        {
            w.Write(r?.F0 ?? 0f);
            w.Write(r?.F1 ?? 0f);
            w.Write(r?.F2 ?? 0f);
            w.Write(r?.F3 ?? 0f);
            w.Write(r?.F4 ?? 0f);
            w.Write(r?.F5 ?? 0f);
            w.Write(r?.F6 ?? 0f);
            w.Write(r?.F7 ?? 0f);
            w.Write(r?.I0 ?? 0);
            w.Write(r?.I1 ?? 0);
            w.Write(r?.L0 ?? 0L);
            w.Write(r?.F8 ?? 0f);
            w.Write(r?.F9 ?? 0f);
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
    }

    public class TieredLootRunLevelsJsonFile
    {
        public Header Header { get; set; } = Header.Default();
        public List<TieredLootRunLevelRecord> Records { get; set; } = new List<TieredLootRunLevelRecord>();
    }

    public class TieredLootRunLevelRecord
    {
        public float F0 { get; set; }
        public float F1 { get; set; }
        public float F2 { get; set; }
        public float F3 { get; set; }
        public float F4 { get; set; }
        public float F5 { get; set; }
        public float F6 { get; set; }
        public float F7 { get; set; }
        public int I0 { get; set; }
        public int I1 { get; set; }
        public long L0 { get; set; }
        public float F8 { get; set; }
        public float F9 { get; set; }
    }
}
