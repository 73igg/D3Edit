using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using D3Edit.Core;

namespace D3Edit.Filetypes.Gam
{
    public static class ItemTypesIO
    {
        public static ItemTypesJsonFile ReadGamFile(string filePath)
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
                if (blockLen <= 0) throw new InvalidDataException("ItemTypes block pointer invalid.");
            }

            int preamble = DetectPreamble(br, blockOff, 32);
            fs.Position = blockOff + preamble;

            var recs = new List<ItemTypeRecord>();
            const int RecordSize = 336; // 256-byte name + 20 * 4-byte ints

            long end = Math.Min(fs.Length, (long)blockOff + blockLen);
            long usable = end - fs.Position;
            if (usable < 0) usable = 0;
            long wholeBytes = usable - usable % RecordSize;
            end = fs.Position + wholeBytes;

            while (fs.Position + RecordSize <= end)
            {
                long start = fs.Position;
                recs.Add(ReadOne(br));
                fs.Position = start + RecordSize; // fixed stride
            }

            var outHeader = header;
            outHeader.BalanceType = balanceType;
            outHeader.I0 = i0;
            outHeader.I1 = i1;

            return new ItemTypesJsonFile { Header = outHeader, Records = recs };
        }

        public static void WriteGamFile(string filePath, ItemTypesJsonFile data)
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
            foreach (var r in data.Records ?? Enumerable.Empty<ItemTypeRecord>())
                WriteOne(bw, r);
            long end = fs.Position;

            long save = fs.Position;
            fs.Position = 0x230 + 4;
            bw.Write(checked((int)(end - BLOCK_OFF)));
            fs.Position = save;
        }

        private static ItemTypeRecord ReadOne(BinaryReader s)
        {
            var rec = new ItemTypeRecord();

            var nameBytes = s.ReadBytes(256);
            int nul = Array.IndexOf(nameBytes, (byte)0);
            if (nul < 0) nul = nameBytes.Length;
            rec.Name = Encoding.UTF8.GetString(nameBytes, 0, nul);

            rec.ParentType = s.ReadInt32();
            rec.GBID = s.ReadInt32();
            rec.I0 = s.ReadInt32();
            rec.LootLevelRange = s.ReadInt32();
            rec.ReqCrafterLevelForEnchant = s.ReadInt32();
            rec.MaxSockets = s.ReadInt32();
            rec.Usable = s.ReadInt32(); // store flags as raw int
            rec.BodySlot1 = s.ReadInt32();
            rec.BodySlot2 = s.ReadInt32();
            rec.BodySlot3 = s.ReadInt32();
            rec.BodySlot4 = s.ReadInt32();
            rec.InheritedAffix0 = s.ReadInt32();
            rec.InheritedAffix1 = s.ReadInt32();
            rec.InheritedAffix2 = s.ReadInt32();
            rec.InheritedAffixFamily0 = s.ReadInt32();

            rec.Labels = new int[5];
            for (int i = 0; i < 5; i++) rec.Labels[i] = s.ReadInt32();

            rec.Hash = StringHashHelper.HashItemName(rec.Name);
            return rec;
        }

        private static void WriteOne(BinaryWriter w, ItemTypeRecord r)
        {
            long start = w.BaseStream.Position;

            var name = r?.Name ?? string.Empty;
            var nameBytes = Encoding.UTF8.GetBytes(name);
            if (nameBytes.Length >= 256) nameBytes = nameBytes.Take(255).ToArray();
            w.Write(nameBytes);
            if (nameBytes.Length < 256)
            {
                w.Write((byte)0);
                int pad = 256 - (nameBytes.Length + 1);
                if (pad > 0) w.Write(new byte[pad]);
            }

            w.Write(r?.ParentType ?? 0);
            w.Write(r?.GBID ?? 0);
            w.Write(r?.I0 ?? 0);
            w.Write(r?.LootLevelRange ?? 0);
            w.Write(r?.ReqCrafterLevelForEnchant ?? 0);
            w.Write(r?.MaxSockets ?? 0);
            w.Write(r?.Usable ?? 0);
            w.Write(r?.BodySlot1 ?? 0);
            w.Write(r?.BodySlot2 ?? 0);
            w.Write(r?.BodySlot3 ?? 0);
            w.Write(r?.BodySlot4 ?? 0);
            w.Write(r?.InheritedAffix0 ?? 0);
            w.Write(r?.InheritedAffix1 ?? 0);
            w.Write(r?.InheritedAffix2 ?? 0);
            w.Write(r?.InheritedAffixFamily0 ?? 0);

            var labels = r?.Labels ?? Array.Empty<int>();
            for (int i = 0; i < 5; i++)
                w.Write(i < labels.Length ? labels[i] : 0);

            const int RecordSize = 336;
            long wrote = w.BaseStream.Position - start;
            if (wrote > RecordSize) throw new InvalidDataException($"ItemType record overflow ({wrote} > {RecordSize}).");
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
                if (zeros >= 17) return 16;
                return zeros;
            }
            finally { s.Position = saved; }
        }
    }

    public class ItemTypesJsonFile
    {
        public Header Header { get; set; } = Header.Default();
        public List<ItemTypeRecord> Records { get; set; } = new List<ItemTypeRecord>();
    }

    public class ItemTypeRecord
    {
        public int Hash { get; set; }                 // derived from Name (not stored in file)
        public string Name { get; set; } = "";
        public int ParentType { get; set; }
        public int I0 { get; set; }
        public int GBID { get; set; }
        public int LootLevelRange { get; set; }
        public int ReqCrafterLevelForEnchant { get; set; }
        public int MaxSockets { get; set; }
        public int Usable { get; set; }               // ItemFlags as raw int
        public int BodySlot1 { get; set; }            // eItemType as raw int
        public int BodySlot2 { get; set; }
        public int BodySlot3 { get; set; }
        public int BodySlot4 { get; set; }
        public int InheritedAffix0 { get; set; }
        public int InheritedAffix1 { get; set; }
        public int InheritedAffix2 { get; set; }
        public int InheritedAffixFamily0 { get; set; }
        public int[] Labels { get; set; } = new int[5];
    }
}
