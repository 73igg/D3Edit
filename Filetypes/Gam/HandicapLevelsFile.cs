using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using D3Edit.Core;

namespace D3Edit.Filetypes.Gam
{
    public static class HandicapLevelsIO
    {
        public static HandicapLevelsJsonFile ReadGamFile(string filePath)
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

                if (off == 0x238 && len > 0 && off + len <= fileSize)
                {
                    blockOff = off;
                    blockLen = len;
                }
            }
            if (blockOff == 0 || blockLen <= 0)
            {
                blockOff = 0x238;
                blockLen = fileSize - blockOff;
                if (blockLen <= 0) throw new InvalidDataException("HandicapLevels block pointer invalid.");
            }

            int preamble = DetectPreamble(br, blockOff, 32);
            fs.Position = blockOff + (preamble > 0 ? preamble : 0x10);

            var recs = new List<HandicapLevelRecord>();
            const int RecordSize = 32; // 6 floats (24) + 2 ints (8) = 32 bytes
            long end = Math.Min(fs.Length, blockOff + blockLen);

            while (fs.Position + RecordSize <= end)
            {
                recs.Add(ReadOne(br));
            }

            var outHeader = header;
            outHeader.BalanceType = balanceType;
            outHeader.I0 = i0;
            outHeader.I1 = i1;

            return new HandicapLevelsJsonFile { Header = outHeader, Records = recs };
        }

        public static void WriteGamFile(string filePath, HandicapLevelsJsonFile data)
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
            bw.Write(0); // placeholder

            while (fs.Position < BLOCK_OFF) bw.Write((byte)0);

            bw.Write(new byte[0x10]);

            long start = fs.Position;
            foreach (var r in data.Records ?? Enumerable.Empty<HandicapLevelRecord>())
                WriteOne(bw, r);
            long end = fs.Position;

            long save = fs.Position;
            fs.Position = 0x230 + 4;
            bw.Write(checked((int)(end - BLOCK_OFF)));
            fs.Position = save;
        }

        private static HandicapLevelRecord ReadOne(BinaryReader s)
        {
            return new HandicapLevelRecord
            {
                HPMod = s.ReadSingle(),
                DmgMod = s.ReadSingle(),
                F2 = s.ReadSingle(),
                XPMod = s.ReadSingle(),
                GoldMod = s.ReadSingle(),
                F5 = s.ReadSingle(),
                I0 = s.ReadInt32(),
                I1 = s.ReadInt32(),
            };
        }

        private static void WriteOne(BinaryWriter w, HandicapLevelRecord r)
        {
            w.Write(r?.HPMod ?? 0);
            w.Write(r?.DmgMod ?? 0);
            w.Write(r?.F2 ?? 0);
            w.Write(r?.XPMod ?? 0);
            w.Write(r?.GoldMod ?? 0);
            w.Write(r?.F5 ?? 0);
            w.Write(r?.I0 ?? 0);
            w.Write(r?.I1 ?? 0);
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

    public class HandicapLevelsJsonFile
    {
        public Header Header { get; set; } = Header.Default();
        public List<HandicapLevelRecord> Records { get; set; } = new List<HandicapLevelRecord>();
    }

    public class HandicapLevelRecord
    {
        public float HPMod { get; set; }
        public float DmgMod { get; set; }
        public float F2 { get; set; }
        public float XPMod { get; set; }
        public float GoldMod { get; set; }
        public float F5 { get; set; }
        public int I0 { get; set; }
        public int I1 { get; set; }
    }
}
