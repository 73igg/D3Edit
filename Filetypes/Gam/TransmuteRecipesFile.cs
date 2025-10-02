using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using D3Edit.Core;

namespace D3Edit.Filetypes.Gam
{
    public static class TransmuteRecipesIO
    {
        public static TransmuteRecipesJsonFile ReadGamFile(string filePath)
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
                if (blockLen <= 0) throw new InvalidDataException("TransmuteRecipes block pointer invalid.");
            }

            int preamble = DetectPreamble(br, blockOff, 32);
            fs.Position = blockOff + (preamble > 0 ? preamble : 0x10);

            int B = DetectIngredientSize(br, fs.Position, fileSize);
            if (B == 0) B = 8;

            var recs = new List<TransmuteRecipeRecord>();
            int stride = 256 + 16 + 8 * B + 12;
            long end = Math.Min(fs.Length, blockOff + blockLen);

            while (fs.Position + stride <= end)
            {
                recs.Add(ReadOne(br, B));
            }

            var outHeader = header;
            outHeader.BalanceType = balanceType;
            outHeader.I0 = i0;
            outHeader.I1 = i1;

            return new TransmuteRecipesJsonFile { Header = outHeader, IngredientSize = B, Records = recs };
        }

        public static void WriteGamFile(string filePath, TransmuteRecipesJsonFile data)
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

            int B = data.IngredientSize > 0 ? data.IngredientSize :
                    data.Records?.FirstOrDefault()?.Ingredients?.FirstOrDefault()?.Length ?? 8;

            foreach (var r in data.Records ?? Enumerable.Empty<TransmuteRecipeRecord>())
                WriteOne(bw, r, B);

            long end = fs.Position;

            long save = fs.Position;
            fs.Position = 0x230 + 4;
            bw.Write(checked((int)(end - BLOCK_OFF)));
            fs.Position = save;
        }

        private static TransmuteRecipeRecord ReadOne(BinaryReader s, int B)
        {
            var r = new TransmuteRecipeRecord();
            r.Name = ReadFixedCString(s, 256);
            r.GBID = s.ReadInt32();
            r.PAD = s.ReadInt32();
            r.TransmuteType = s.ReadInt32();

            r.Ingredients = new byte[8][];
            for (int i = 0; i < 8; i++) r.Ingredients[i] = s.ReadBytes(B);

            r.IngredientsCount = s.ReadInt32();
            r.Page = s.ReadInt32();
            r.Hidden = s.ReadInt32();
            return r;
        }

        private static void WriteOne(BinaryWriter w, TransmuteRecipeRecord r, int B)
        {
            WriteFixedCString(w, r?.Name ?? string.Empty, 256);
            w.Write(r?.GBID ?? 0);
            w.Write(r?.PAD ?? 0);
            w.Write(r?.TransmuteType ?? 0);

            var arr = r?.Ingredients ?? Array.Empty<byte[]>();
            for (int i = 0; i < 8; i++)
            {
                var blob = i < arr.Length && arr[i] != null ? arr[i] : Array.Empty<byte>();
                if (blob.Length == B) w.Write(blob);
                else if (blob.Length > B) w.Write(blob, 0, B);
                else { w.Write(blob); Bin.WriteZeros(w.BaseStream, B - blob.Length); }
            }

            w.Write(r?.IngredientsCount ?? 0);
            w.Write(r?.Page ?? 0);
            w.Write(r?.Hidden ?? 0);
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

        private static int DetectIngredientSize(BinaryReader br, long firstRecPos, int fileSize)
        {
            int[] candB = new[] { 8, 12, 16, 20, 24, 28, 32 };
            foreach (var b in candB)
            {
                int stride = 256 + 16 + 8 * b + 12;
                long pos = firstRecPos + stride;
                if (pos + 256 > fileSize) continue;
                string n = PeekName(br, pos);
                if (IsLikelyName(n)) return b;
            }
            return 0;
        }

        private static string PeekName(BinaryReader br, long pos)
        {
            var s = br.BaseStream;
            long saved = s.Position;
            try
            {
                s.Position = pos;
                return ReadFixedCString(br, 256);
            }
            finally { s.Position = saved; }
        }

        private static bool IsLikelyName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            int n = name.Length;
            int printable = 0;
            foreach (char ch in name)
                if (ch >= 0x20 && ch <= 0x7E) printable++;
            return n > 0 && printable >= Math.Max(3, (int)(0.8 * n));
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
                if (pad > 0) Bin.WriteZeros(w.BaseStream, pad);
            }
        }
    }

    public class TransmuteRecipesJsonFile
    {
        public Header Header { get; set; } = Header.Default();
        public int IngredientSize { get; set; } = 8;
        public List<TransmuteRecipeRecord> Records { get; set; } = new List<TransmuteRecipeRecord>();
    }

    public class TransmuteRecipeRecord
    {
        public string Name { get; set; } = string.Empty; // 256 bytes, null-terminated
        public int GBID { get; set; }
        public int PAD { get; set; }
        public int TransmuteType { get; set; } // numeric enum value
        public byte[][] Ingredients { get; set; } = Array.Empty<byte[]>(); // 8 entries of B bytes
        public int IngredientsCount { get; set; }
        public int Page { get; set; }
        public int Hidden { get; set; }
    }
}
