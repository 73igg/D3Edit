using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using D3Edit.Core;

namespace D3Edit.Filetypes.Gam
{
    public static class RecipesIO
    {
        private const int IngredientSlots = 5; // Extracted .gam stores 5 pairs (GBID,Count)

        public static RecipesJsonFile ReadGamFile(string filePath)
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
                if (blockLen <= 0) throw new InvalidDataException("Recipes block pointer invalid.");
            }

            int preamble = DetectPreamble(br, blockOff, 32);
            fs.Position = blockOff + (preamble > 0 ? preamble : 0x10);

            var recs = new List<RecipeRecord>();
            const int RecordSize = 256 + 10 * 4 + IngredientSlots * 8; // = 256 + 40 + 40 = 336 bytes
            long end = Math.Min(fs.Length, blockOff + blockLen);

            while (fs.Position + RecordSize <= end)
            {
                recs.Add(ReadOne(br));
            }

            var outHeader = header;
            outHeader.BalanceType = balanceType;
            outHeader.I0 = i0;
            outHeader.I1 = i1;

            return new RecipesJsonFile { Header = outHeader, Records = recs };
        }

        public static void WriteGamFile(string filePath, RecipesJsonFile data)
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

            foreach (var r in data.Records ?? Enumerable.Empty<RecipeRecord>())
                WriteOne(bw, r);

            long end = fs.Position;

            long save = fs.Position;
            fs.Position = 0x230 + 4;
            bw.Write(checked((int)(end - BLOCK_OFF)));
            fs.Position = save;
        }

        private static RecipeRecord ReadOne(BinaryReader s)
        {
            var r = new RecipeRecord();
            r.Name = ReadFixedCString(s, 256);

            r.Hash = s.ReadInt32();            // may be 0 → recompute on write
            r.GBID = s.ReadInt32();
            r.PAD = s.ReadInt32();             // often -1 for trainer rows
            r.SNORecipe = s.ReadInt32();
            r.CrafterType = s.ReadInt32();     // enum numeric
            r.Flags = s.ReadInt32();
            r.Level = s.ReadInt32();
            r.Gold = s.ReadInt32();
            r.NumIngredients = s.ReadInt32();
            r.Reserved0 = s.ReadInt32();       // observed non-zero cookie/opaque value

            r.Ingredients = new List<RecipeReagent>(IngredientSlots);
            for (int i = 0; i < IngredientSlots; i++)
            {
                var ing = new RecipeReagent
                {
                    GBID = s.ReadInt32(),
                    Count = s.ReadInt32(),
                };
                r.Ingredients.Add(ing);
            }

            return r;
        }

        private static void WriteOne(BinaryWriter w, RecipeRecord r)
        {
            WriteFixedCString(w, r?.Name ?? string.Empty, 256);

            int hash = r?.Hash ?? 0;
            if (hash == 0) hash = HashItemName(r?.Name ?? string.Empty);

            w.Write(hash);
            w.Write(r?.GBID ?? 0);
            w.Write(r?.PAD ?? 0);
            w.Write(r?.SNORecipe ?? 0);
            w.Write(r?.CrafterType ?? 0);
            w.Write(r?.Flags ?? 0);
            w.Write(r?.Level ?? 0);
            w.Write(r?.Gold ?? 0);
            w.Write(r?.NumIngredients ?? 0);
            w.Write(r?.Reserved0 ?? 0);

            var list = r?.Ingredients ?? new List<RecipeReagent>();
            for (int i = 0; i < IngredientSlots; i++)
            {
                int gbid = 0, cnt = 0;
                if (i < list.Count && list[i] != null)
                {
                    gbid = list[i].GBID;
                    cnt = list[i].Count;
                }
                w.Write(gbid);
                w.Write(cnt);
            }
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

        private static int HashItemName(string s)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            unchecked
            {
                int h = 0;
                foreach (char ch in s.ToLowerInvariant())
                    h = h * 0x1003F + ch;
                return h;
            }
        }
    }

    public class RecipesJsonFile
    {
        public Header Header { get; set; } = Header.Default();
        public List<RecipeRecord> Records { get; set; } = new List<RecipeRecord>();
    }

    public class RecipeRecord
    {
        public string Name { get; set; } = string.Empty; // 256 bytes, null-terminated
        public int Hash { get; set; }
        public int GBID { get; set; }
        public int PAD { get; set; }
        public int SNORecipe { get; set; }
        public int CrafterType { get; set; } // RecipeType numeric enum
        public int Flags { get; set; }
        public int Level { get; set; }
        public int Gold { get; set; }
        public int NumIngredients { get; set; }
        public int Reserved0 { get; set; } // observed opaque/cookie
        public List<RecipeReagent> Ingredients { get; set; } = new List<RecipeReagent>(); // exactly 5 written
    }

    public class RecipeReagent
    {
        public int GBID { get; set; }
        public int Count { get; set; }
    }
}
