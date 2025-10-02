using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using D3Edit.Core;

namespace D3Edit.Filetypes.Gam
{
    public static class MonsterNamesIO
    {
        public static MonsterNamesJsonFile ReadGamFile(string filePath)
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
                if (blockLen <= 0) throw new InvalidDataException("MonsterNames block pointer invalid.");
            }

            int preamble = DetectPreamble(br, blockOff, 32);
            fs.Position = blockOff + (preamble > 0 ? preamble : 0x10);

            var recs = new List<MonsterNameRecord>();
            const int RecordSize = 400; // 256 + 3*4 + 128 + 4
            long end = Math.Min(fs.Length, blockOff + blockLen);

            while (fs.Position + RecordSize <= end)
            {
                recs.Add(ReadOne(br));
            }

            var outHeader = header;
            outHeader.BalanceType = balanceType;
            outHeader.I0 = i0;
            outHeader.I1 = i1;

            return new MonsterNamesJsonFile { Header = outHeader, Records = recs };
        }

        public static void WriteGamFile(string filePath, MonsterNamesJsonFile data)
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
            bw.Write(0); // placeholder for block length

            while (fs.Position < BLOCK_OFF) bw.Write((byte)0);

            bw.Write(new byte[0x10]);

            foreach (var r in data.Records ?? Enumerable.Empty<MonsterNameRecord>())
                WriteOne(bw, r);

            long end = fs.Position;

            long save = fs.Position;
            fs.Position = 0x230 + 4;
            bw.Write(checked((int)(end - BLOCK_OFF)));
            fs.Position = save;
        }

        private static MonsterNameRecord ReadOne(BinaryReader s)
        {
            var r = new MonsterNameRecord();
            r.Name = ReadFixedCString(s, 256);
            r.I0 = s.ReadInt32();
            r.I1 = s.ReadInt32();
            r.AffixType = s.ReadInt32(); // enum numeric value
            r.S0 = ReadFixedCString(s, 128);
            r.I2 = s.ReadInt32();
            return r;
        }

        private static void WriteOne(BinaryWriter w, MonsterNameRecord r)
        {
            WriteFixedCString(w, r?.Name ?? string.Empty, 256);
            w.Write(r?.I0 ?? 0);
            w.Write(r?.I1 ?? 0);
            w.Write(r?.AffixType ?? 0);
            WriteFixedCString(w, r?.S0 ?? string.Empty, 128);
            w.Write(r?.I2 ?? 0);
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
                if (pad > 0) w.Write(new byte[pad]);
            }
        }
    }

    public class MonsterNamesJsonFile
    {
        public Header Header { get; set; } = Header.Default();
        public List<MonsterNameRecord> Records { get; set; } = new List<MonsterNameRecord>();
    }

    public class MonsterNameRecord
    {
        public string Name { get; set; } = string.Empty; // 256 bytes, null-terminated
        public int I0 { get; set; }
        public int I1 { get; set; }
        public int AffixType { get; set; } // numeric enum value
        public string S0 { get; set; } = string.Empty;   // 128 bytes, null-terminated
        public int I2 { get; set; }
    }
}
