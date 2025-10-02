using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using D3Edit.Core;

namespace D3Edit.Filetypes.Gam
{
    public static class ExperienceAltTableIO
    {
        public static ExperienceAltTableJsonFile ReadGamFile(string filePath)
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var br = new BinaryReader(fs, Encoding.UTF8, leaveOpen: false);

            var header = Header.Read(br);
            int balanceType = br.ReadInt32();
            int i0 = br.ReadInt32();
            int i1 = br.ReadInt32();

            int blockOff = 0;
            int blockLen = 0;

            if (fs.Length >= 0x230 + 8)
            {
                fs.Position = 0x230;
                blockOff = br.ReadInt32();   // usually 0x238
                blockLen = br.ReadInt32();
            }

            bool dirLooksValid = blockOff > 0 && blockLen >= 0 && (long)blockOff + blockLen <= fs.Length;
            if (!dirLooksValid)
            {
                blockOff = 0x238;
                blockLen = checked((int)Math.Max(0, fs.Length - blockOff));
                if (blockLen <= 0 || blockOff > fs.Length)
                    throw new InvalidDataException("ExperienceAltTable block pointer invalid.");
            }

            fs.Position = blockOff;
            int preamble = DetectPreamble(br, 32);
            fs.Position = blockOff + preamble;

            var recs = new List<ExperienceAltTableRecord>();
            const int RecordSize = 128;
            long end = Math.Min(fs.Length, (long)blockOff + blockLen);

            while (fs.Position + RecordSize <= end)
            {
                long start = fs.Position;
                var r = ReadOne(br);
                recs.Add(r);
                fs.Position = start + RecordSize;
            }

            var outHeader = header;
            outHeader.BalanceType = balanceType;
            outHeader.I0 = i0;
            outHeader.I1 = i1;

            return new ExperienceAltTableJsonFile { Header = outHeader, Records = recs };
        }

        public static void WriteGamFile(string filePath, ExperienceAltTableJsonFile data)
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

            long recStart = fs.Position;
            foreach (var r in data.Records ?? Enumerable.Empty<ExperienceAltTableRecord>())
                WriteOne(bw, r);
            long recEnd = fs.Position;

            long save = fs.Position;
            fs.Position = 0x230 + 4;
            bw.Write(checked((int)(recEnd - BLOCK_OFF)));
            fs.Position = save;
        }

        private static ExperienceAltTableRecord ReadOne(BinaryReader s)
        {
            var r = new ExperienceAltTableRecord
            {
                L0 = s.ReadInt64(),

                I1 = s.ReadInt32(),
                I2 = s.ReadInt32(),
                I3 = s.ReadInt32(),
                I4 = s.ReadInt32(),
                I5 = s.ReadInt32(),
                I6 = s.ReadInt32(),
                I7 = s.ReadInt32(),
                I8 = s.ReadInt32(),
                I9 = s.ReadInt32(),
                I10 = s.ReadInt32(),

                I11 = s.ReadInt32(),
                I12 = s.ReadInt32(),
                I13 = s.ReadInt32(),
                I14 = s.ReadInt32(),
                I15 = s.ReadInt32(),
                I16 = s.ReadInt32(),
                I17 = s.ReadInt32(),
                I18 = s.ReadInt32(),
                I19 = s.ReadInt32(),
                I20 = s.ReadInt32(),

                I21 = s.ReadInt32(),
                I22 = s.ReadInt32(),
                I23 = s.ReadInt32(),
                I24 = s.ReadInt32(),
                I25 = s.ReadInt32(),
                I26 = s.ReadInt32(),
                I27 = s.ReadInt32(),
                I28 = s.ReadInt32(),
                I29 = s.ReadInt32(),
                I30 = s.ReadInt32()
            };
            return r;
        }

        private static void WriteOne(BinaryWriter w, ExperienceAltTableRecord r)
        {
            long start = w.BaseStream.Position;

            w.Write(r?.L0 ?? 0L);

            w.Write(r?.I1 ?? 0);
            w.Write(r?.I2 ?? 0);
            w.Write(r?.I3 ?? 0);
            w.Write(r?.I4 ?? 0);
            w.Write(r?.I5 ?? 0);
            w.Write(r?.I6 ?? 0);
            w.Write(r?.I7 ?? 0);
            w.Write(r?.I8 ?? 0);
            w.Write(r?.I9 ?? 0);
            w.Write(r?.I10 ?? 0);

            w.Write(r?.I11 ?? 0);
            w.Write(r?.I12 ?? 0);
            w.Write(r?.I13 ?? 0);
            w.Write(r?.I14 ?? 0);
            w.Write(r?.I15 ?? 0);
            w.Write(r?.I16 ?? 0);
            w.Write(r?.I17 ?? 0);
            w.Write(r?.I18 ?? 0);
            w.Write(r?.I19 ?? 0);
            w.Write(r?.I20 ?? 0);

            w.Write(r?.I21 ?? 0);
            w.Write(r?.I22 ?? 0);
            w.Write(r?.I23 ?? 0);
            w.Write(r?.I24 ?? 0);
            w.Write(r?.I25 ?? 0);
            w.Write(r?.I26 ?? 0);
            w.Write(r?.I27 ?? 0);
            w.Write(r?.I28 ?? 0);
            w.Write(r?.I29 ?? 0);
            w.Write(r?.I30 ?? 0);

            const int RecordSize = 128;
            long wrote = w.BaseStream.Position - start;
            if (wrote > RecordSize) throw new InvalidDataException($"ExperienceAltTable record overflow ({wrote} > {RecordSize}).");
            if (wrote < RecordSize) w.Write(new byte[RecordSize - wrote]);
        }

        private static int DetectPreamble(BinaryReader br, int maxInspect)
        {
            var s = br.BaseStream;
            long saved = s.Position;
            try
            {
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

    public class ExperienceAltTableJsonFile
    {
        public Header Header { get; set; } = Header.Default();
        public List<ExperienceAltTableRecord> Records { get; set; } = new List<ExperienceAltTableRecord>();
    }

    public class ExperienceAltTableRecord
    {
        public long L0 { get; set; }
        public int I1 { get; set; }
        public int I2 { get; set; }
        public int I3 { get; set; }
        public int I4 { get; set; }
        public int I5 { get; set; }
        public int I6 { get; set; }
        public int I7 { get; set; }
        public int I8 { get; set; }
        public int I9 { get; set; }
        public int I10 { get; set; }
        public int I11 { get; set; }
        public int I12 { get; set; }
        public int I13 { get; set; }
        public int I14 { get; set; }
        public int I15 { get; set; }
        public int I16 { get; set; }
        public int I17 { get; set; }
        public int I18 { get; set; }
        public int I19 { get; set; }
        public int I20 { get; set; }
        public int I21 { get; set; }
        public int I22 { get; set; }
        public int I23 { get; set; }
        public int I24 { get; set; }
        public int I25 { get; set; }
        public int I26 { get; set; }
        public int I27 { get; set; }
        public int I28 { get; set; }
        public int I29 { get; set; }
        public int I30 { get; set; }
    }
}
