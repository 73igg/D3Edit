using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using D3Edit.Core;

namespace D3Edit.Filetypes.Gam
{
    public static class PowerFormulaTablesIO
    {
        public static PowerFormulaTablesJsonFile ReadGamFile(string filePath)
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
                if (blockLen <= 0) throw new InvalidDataException("PowerFormulaTables block pointer invalid.");
            }

            int preamble = DetectPreamble(br, blockOff, 32);
            fs.Position = blockOff + (preamble > 0 ? preamble : 0x10);

            var recs = new List<PowerFormulaRecord>();
            const int RecordSize = 1024 + 76 * 4; // = 1328 bytes
            long end = Math.Min(fs.Length, blockOff + blockLen);

            while (fs.Position + RecordSize <= end)
            {
                recs.Add(ReadOne(br));
            }

            var outHeader = header;
            outHeader.BalanceType = balanceType;
            outHeader.I0 = i0;
            outHeader.I1 = i1;

            return new PowerFormulaTablesJsonFile { Header = outHeader, Records = recs };
        }

        public static void WriteGamFile(string filePath, PowerFormulaTablesJsonFile data)
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
            bw.Write(0); // placeholder for length

            while (fs.Position < BLOCK_OFF) bw.Write((byte)0);

            bw.Write(new byte[0x10]);

            foreach (var r in data.Records ?? Enumerable.Empty<PowerFormulaRecord>())
                WriteOne(bw, r);

            long end = fs.Position;

            long save = fs.Position;
            fs.Position = 0x230 + 4;
            bw.Write(checked((int)(end - BLOCK_OFF)));
            fs.Position = save;
        }

        private static PowerFormulaRecord ReadOne(BinaryReader s)
        {
            var r = new PowerFormulaRecord();
            r.S0 = ReadFixedCString(s, 1024);
            r.F0 = new float[76];
            for (int i = 0; i < r.F0.Length; i++) r.F0[i] = s.ReadSingle();
            return r;
        }

        private static void WriteOne(BinaryWriter w, PowerFormulaRecord r)
        {
            WriteFixedCString(w, r?.S0 ?? string.Empty, 1024);
            var arr = r?.F0 ?? Array.Empty<float>();
            int n = Math.Min(arr.Length, 76);
            for (int i = 0; i < n; i++) w.Write(arr[i]);
            for (int i = n; i < 76; i++) w.Write(0f);
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

    public class PowerFormulaTablesJsonFile
    {
        public Header Header { get; set; } = Header.Default();
        public List<PowerFormulaRecord> Records { get; set; } = new List<PowerFormulaRecord>();
    }

    public class PowerFormulaRecord
    {
        public string S0 { get; set; } = string.Empty; // fixed 1024 byte, null-terminated
        public float[] F0 { get; set; } = Array.Empty<float>(); // exactly 76 floats written; shorter arrays are padded with 0
    }
}
