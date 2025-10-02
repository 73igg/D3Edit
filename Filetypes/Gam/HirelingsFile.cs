using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using D3Edit.Core;

namespace D3Edit.Filetypes.Gam
{
    public static class HirelingsIO
    {
        public static HirelingsJsonFile ReadGamFile(string filePath)
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
                blockOff = br.ReadInt32();
                blockLen = br.ReadInt32();
                fs.Position = save;

                if (!(blockOff > 0 && blockLen >= 0 && (long)blockOff + blockLen <= fileSize))
                {
                    blockOff = 0;
                    blockLen = 0;
                }
            }
            if (blockOff == 0)
            {
                blockOff = 0x238;
                blockLen = Math.Max(0, fileSize - blockOff);
                if (blockLen <= 0) throw new InvalidDataException("Hirelings block pointer invalid.");
            }

            int preamble = DetectPreamble(br, blockOff, 32);
            fs.Position = blockOff + preamble;

            var recs = new List<HirelingRecord>();
            const int RecordSize = 328;
            long end = Math.Min(fs.Length, (long)blockOff + blockLen);

            while (fs.Position + RecordSize <= end)
            {
                long start = fs.Position;
                recs.Add(ReadOne(br));
                fs.Position = start + RecordSize; // fixed-stride (328
            }

            var outHeader = header;
            outHeader.BalanceType = balanceType;
            outHeader.I0 = i0;
            outHeader.I1 = i1;

            return new HirelingsJsonFile { Header = outHeader, Records = recs };
        }

        public static void WriteGamFile(string filePath, HirelingsJsonFile data)
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
            bw.Write(0); // len placeholder

            while (fs.Position < BLOCK_OFF) bw.Write((byte)0);
            bw.Write(new byte[0x10]);

            long start = fs.Position;
            foreach (var r in data.Records ?? Enumerable.Empty<HirelingRecord>())
                WriteOne(bw, r);
            long end = fs.Position;

            long save = fs.Position;
            fs.Position = 0x230 + 4; // len slot
            bw.Write(checked((int)(end - BLOCK_OFF)));
            fs.Position = save;
        }

        private static HirelingRecord ReadOne(BinaryReader s)
        {
            var rec = new HirelingRecord();

            var nameBytes = s.ReadBytes(256);
            int nul = Array.IndexOf(nameBytes, (byte)0);
            if (nul < 0) nul = nameBytes.Length;
            rec.Name = Encoding.UTF8.GetString(nameBytes, 0, nul);

            rec.I0 = s.ReadInt32();
            rec.I1 = s.ReadInt32();
            rec.SNOActor = s.ReadInt32();
            rec.SNOProxy = s.ReadInt32();
            rec.SNOInventory = s.ReadInt32();
            rec.TreasureClassSNO = s.ReadInt32();
            rec.Attribute = s.ReadInt32(); // store raw enum as int for portability

            rec.F0 = s.ReadSingle();
            rec.F1 = s.ReadSingle();
            rec.F2 = s.ReadSingle();
            rec.F3 = s.ReadSingle();
            rec.F4 = s.ReadSingle();
            rec.F5 = s.ReadSingle();
            rec.F6 = s.ReadSingle();
            rec.F7 = s.ReadSingle();
            rec.F8 = s.ReadSingle();
            rec.F9 = s.ReadSingle();
            rec.F10 = s.ReadSingle();

            return rec;
        }

        private static void WriteOne(BinaryWriter w, HirelingRecord r)
        {
            long start = w.BaseStream.Position;

            var name = r?.Name ?? string.Empty;
            var nameBytes = Encoding.UTF8.GetBytes(name);
            if (nameBytes.Length >= 256)
            {
                nameBytes = nameBytes.Take(255).ToArray();
            }
            w.Write(nameBytes);
            if (nameBytes.Length < 256)
            {
                w.Write((byte)0);
                int pad = 256 - (nameBytes.Length + 1);
                if (pad > 0) w.Write(new byte[pad]);
            }

            w.Write(r?.I0 ?? 0);
            w.Write(r?.I1 ?? 0);
            w.Write(r?.SNOActor ?? 0);
            w.Write(r?.SNOProxy ?? 0);
            w.Write(r?.SNOInventory ?? 0);
            w.Write(r?.TreasureClassSNO ?? 0);
            w.Write(r?.Attribute ?? 0);

            w.Write(r?.F0 ?? 0);
            w.Write(r?.F1 ?? 0);
            w.Write(r?.F2 ?? 0);
            w.Write(r?.F3 ?? 0);
            w.Write(r?.F4 ?? 0);
            w.Write(r?.F5 ?? 0);
            w.Write(r?.F6 ?? 0);
            w.Write(r?.F7 ?? 0);
            w.Write(r?.F8 ?? 0);
            w.Write(r?.F9 ?? 0);
            w.Write(r?.F10 ?? 0);

            const int RecordSize = 328;
            long wrote = w.BaseStream.Position - start;
            if (wrote > RecordSize) throw new InvalidDataException($"Hireling record overflow ({wrote} > {RecordSize}).");
            if (wrote < RecordSize) w.Write(new byte[RecordSize - wrote]);
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

    public class HirelingsJsonFile
    {
        public Header Header { get; set; } = Header.Default();
        public List<HirelingRecord> Records { get; set; } = new List<HirelingRecord>();
    }

    public class HirelingRecord
    {
        public string Name { get; set; } = "";
        public int I0 { get; set; }
        public int I1 { get; set; }
        public int SNOActor { get; set; }
        public int SNOProxy { get; set; }
        public int SNOInventory { get; set; }
        public int TreasureClassSNO { get; set; }
        public int Attribute { get; set; }   // keep raw enum value for compatibility

        public float F0 { get; set; }
        public float F1 { get; set; }
        public float F2 { get; set; }
        public float F3 { get; set; }
        public float F4 { get; set; }
        public float F5 { get; set; }
        public float F6 { get; set; }
        public float F7 { get; set; }
        public float F8 { get; set; }
        public float F9 { get; set; }
        public float F10 { get; set; }
    }
}
