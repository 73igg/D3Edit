using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using D3Edit.Core;

namespace D3Edit.Filetypes.Gam
{
    public static class CurrencyIO
    {
        public static CurrencyJsonFile ReadGamFile(string filePath)
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
                if (blockLen <= 0) throw new InvalidDataException("Currency block pointer invalid.");
            }

            int preamble = DetectPreamble(br, blockOff, 32);
            fs.Position = blockOff + (preamble > 0 ? preamble : 0x10);

            var recs = new List<CurrencyRecord>();
            const int RecordSize = 304;
            long end = Math.Min(fs.Length, blockOff + blockLen);

            while (fs.Position + 256 <= end)
            {
                long start = fs.Position;
                string peek = ReadFixedString(br, 256, true);
                if (string.IsNullOrEmpty(peek)) break; // terminator / padding
                fs.Position = start;

                recs.Add(ReadOne(br));

                fs.Position = start + RecordSize;
            }

            var outHeader = header;
            outHeader.BalanceType = balanceType;
            outHeader.I0 = i0;
            outHeader.I1 = i1;

            return new CurrencyJsonFile { Header = outHeader, Records = recs };
        }

        public static void WriteGamFile(string filePath, CurrencyJsonFile data)
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

            bw.Write(new byte[0x10]);

            long recordsStart = fs.Position;

            foreach (var r in data.Records ?? Enumerable.Empty<CurrencyRecord>())
                WriteOne(bw, r);

            long recordsEnd = fs.Position;

            long save = fs.Position;
            fs.Position = 0x230 + 4;
            bw.Write(checked((int)(recordsEnd - BLOCK_OFF)));
            fs.Position = save;
        }

        private static CurrencyRecord ReadOne(BinaryReader br)
        {
            var r = new CurrencyRecord();

            r.Name = ReadFixedString(br, 256, true);
            r.GBID = br.ReadInt32();
            r.PAD = br.ReadInt32();
            r.CurrencyType = br.ReadInt32();

            r.LinkedItemsGBIDs = new int[5];
            for (int i = 0; i < r.LinkedItemsGBIDs.Length; i++)
                r.LinkedItemsGBIDs[i] = br.ReadInt32();

            r.SortOrder = br.ReadInt32();    // comment in your snippet: //872 (kept here)
            r.Hidden = br.ReadInt32();
            r.AutoPickup = br.ReadInt32();

            br.BaseStream.Position += 4;

            return r;
        }

        private static void WriteOne(BinaryWriter bw, CurrencyRecord r)
        {
            const int RecordSize = 304;
            long start = bw.BaseStream.Position;

            WriteFixedString(bw, r?.Name ?? string.Empty, 256);
            bw.Write(r?.GBID ?? 0);
            bw.Write(r?.PAD ?? 0);
            bw.Write(r?.CurrencyType ?? 0);

            var links = EnsureLen(r?.LinkedItemsGBIDs, 5);
            for (int i = 0; i < 5; i++) bw.Write(links[i]);

            bw.Write(r?.SortOrder ?? 0);
            bw.Write(r?.Hidden ?? 0);
            bw.Write(r?.AutoPickup ?? 0);

            bw.Write(0);

            long wrote = bw.BaseStream.Position - start;
            if (wrote > RecordSize)
                throw new InvalidDataException($"Currency record overflow ({wrote} > {RecordSize}).");
            if (wrote < RecordSize)
                bw.Write(new byte[RecordSize - wrote]);
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

        private static string ReadFixedString(BinaryReader br, int byteLen, bool nullTerminated)
        {
            var bytes = br.ReadBytes(byteLen);
            if (bytes.Length < byteLen) throw new EndOfStreamException();
            int end = bytes.Length;
            if (nullTerminated)
            {
                int idx = Array.IndexOf(bytes, (byte)0);
                if (idx >= 0) end = idx;
            }
            return Encoding.UTF8.GetString(bytes, 0, end);
        }

        private static void WriteFixedString(BinaryWriter bw, string s, int byteLen)
        {
            var bytes = Encoding.UTF8.GetBytes(s ?? string.Empty);
            int copy = Math.Min(bytes.Length, byteLen - 1);
            bw.Write(bytes, 0, copy);
            bw.Write((byte)0);
            int written = copy + 1;
            if (written < byteLen) bw.Write(new byte[byteLen - written]);
        }

        private static int[] EnsureLen(int[]? arr, int len)
        {
            var a = arr ?? Array.Empty<int>();
            if (a.Length == len) return a;
            var r = new int[len];
            Array.Copy(a, r, Math.Min(a.Length, len));
            return r;
        }
    }

    public class CurrencyJsonFile
    {
        public Header Header { get; set; } = Header.Default();
        public List<CurrencyRecord> Records { get; set; } = new List<CurrencyRecord>();
    }

    public class CurrencyRecord
    {
        public string Name { get; set; } = "";
        public int GBID { get; set; }
        public int PAD { get; set; }
        public int CurrencyType { get; set; }              // keep as int to avoid enum dependency
        public int[] LinkedItemsGBIDs { get; set; } = new int[5];
        public int SortOrder { get; set; }
        public int Hidden { get; set; }
        public int AutoPickup { get; set; }
    }
}
