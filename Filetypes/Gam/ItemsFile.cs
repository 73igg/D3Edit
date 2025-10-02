using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using D3Edit.Core;

namespace D3Edit.Filetypes.Gam
{
    public static class ItemsIO
    {
        public static GameFileCollection ReadGamFile(string filePath)
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var br = new BinaryReader(fs, Encoding.UTF8, leaveOpen: false);

            var header = Header.Read(br);

            int balanceType = br.ReadInt32();
            int i0 = br.ReadInt32();
            int i1 = br.ReadInt32();

            var (itemsOffset, itemsLength) = FindItemsBlock(br, (int)fs.Length);
            if (itemsOffset <= 0 || itemsLength <= 0)
            {
                var fb = HeuristicFindItemsBlock(br, (int)fs.Length);
                itemsOffset = fb.offset;
                itemsLength = fb.length;
            }
            if (itemsOffset <= 0 || itemsLength <= 0) throw new InvalidDataException("Items block pointer not found.");

            fs.Position = itemsOffset;
            int skip = DetectPreambleLength(br, itemsOffset, (int)Math.Min(32, fs.Length - itemsOffset));
            fs.Position = itemsOffset + skip;

            long itemsEnd = itemsOffset + itemsLength;

            var items = new List<ItemTable>();
            const int RecordSize = 1304;

            while (fs.Position + 256 <= itemsEnd)
            {
                long recStart = fs.Position;
                string peekName = ReadString(br, 256, true);
                if (string.IsNullOrEmpty(peekName)) break;
                fs.Position = recStart;
                var it = ItemTable_Read(br);
                items.Add(it);
                fs.Position = recStart + RecordSize;
                if (fs.Position > itemsEnd + 32) break;
            }

            var outHeader = header;
            outHeader.BalanceType = balanceType;
            outHeader.I0 = i0;
            outHeader.I1 = i1;

            return new GameFileCollection
            {
                Header = outHeader,
                Items = items
            };
        }

        public static void WriteGamFile(string filePath, GameFileCollection game)
        {
            if (game == null) throw new ArgumentNullException(nameof(game));
            if (game.Header == null) throw new InvalidDataException("Header is required in JSON.");

            foreach (var it in game.Items ?? Enumerable.Empty<ItemTable>())
            {
                it.Labels = EnsureLen(it.Labels, 5);
                it.Attribute = EnsureLen(it.Attribute, 16, () => new AttributeSpecifier());
                it.RecipeToGrant = EnsureLen(it.RecipeToGrant, 10);
                it.TransmogsToGrant = EnsureLen(it.TransmogsToGrant, 8);
                it.Massive0 = EnsureLen(it.Massive0, 9);
                it.LegendaryAffixFamily = EnsureLen(it.LegendaryAffixFamily, 6);
                it.MaxAffixLevel = EnsureLen(it.MaxAffixLevel, 6);
                it.I38 = EnsureLen(it.I38, 6);
                it.EnchantAffixIngredients = EnsureLen(it.EnchantAffixIngredients, 6, () => new RecipeIngredient());
                it.EnchantAffixIngredientsX1 = EnsureLen(it.EnchantAffixIngredientsX1, 6, () => new RecipeIngredient());
                it.Attribute1 = EnsureLen(it.Attribute1, 2, () => new AttributeSpecifier());
                it.Name ??= string.Empty;
            }

            using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
            using var bw = new BinaryWriter(fs, Encoding.UTF8, leaveOpen: false);

            Header.Write(bw, game.Header);
            bw.Write(game.Header.BalanceType);
            bw.Write(game.Header.I0);
            bw.Write(game.Header.I1);

            if (fs.Position > 0x230) throw new InvalidDataException("Header too large for fixed layout.");
            while (fs.Position < 0x230) bw.Write((byte)0);

            const int ITEMS_BLOCK_OFF = 0x238;
            bw.Write(ITEMS_BLOCK_OFF); // offset
            bw.Write(0);               // length placeholder

            while (fs.Position < ITEMS_BLOCK_OFF) bw.Write((byte)0);

            const int PREAMBLE_LEN = 0x10; // originals have 0x10 zeros, so first record starts at 0x248
            bw.Write(new byte[PREAMBLE_LEN]);

            const int RecordSize = 1304;
            var items = game.Items ?? new List<ItemTable>();
            long recordsStart = fs.Position;

            foreach (var it in items)
            {
                using var ms = new MemoryStream(RecordSize);
                using var rec = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true);

                WriteFixedString(rec, it.Name ?? string.Empty, 256);

                rec.Write(it.GBID);
                rec.Write(it.PAD);

                rec.Write(it.SNOActor);
                rec.Write(it.ItemTypesGBID);

                rec.Write(it.Flags);
                rec.Write(it.DyeType);

                rec.Write(it.ItemLevel);
                rec.Write((int)it.ItemAct);

                rec.Write(it.AffixLevel);
                rec.Write(it.BonusAffixes);
                rec.Write(it.BonusMajorAffixes);
                rec.Write(it.BonusMinorAffixes);
                rec.Write(it.MaxSockets);
                rec.Write(it.MaxStackSize);
                rec.Write(it.Cost);
                rec.Write(it.CostAlt);
                rec.Write(it.IdentifyCost);
                rec.Write(it.SellOverrideCost);
                rec.Write(it.RemoveGemCost);
                rec.Write(it.RequiredLevel);
                rec.Write(it.CrafterRequiredLevel);
                rec.Write(it.BaseDurability);
                rec.Write(it.DurabilityVariance);
                rec.Write(it.EnchantAffixCost);
                rec.Write(it.EnchantAffixCostX1);
                rec.Write(it.TransmogUnlockCrafterLevel);
                rec.Write(it.TransmogCost);
                rec.Write(it.SNOBaseItem);
                rec.Write(it.SNOSet);
                rec.Write(it.SNOComponentTreasureClass);
                rec.Write(it.SNOComponentTreasureClassMagic);
                rec.Write(it.SNOComponentTreasureClassRare);
                rec.Write(it.SNOComponentTreasureClassLegend);
                rec.Write(it.SNORareNamePrefixStringList);
                rec.Write(it.SNORareNameSuffixStringList);
                rec.Write(it.StartEffect);
                rec.Write(it.EndEffect);
                rec.Write(it.PortraitBkgrnd);
                rec.Write(it.PortraitHPBar);
                rec.Write(it.PortraitBanner);
                rec.Write(it.PortraitFrame);

                for (int i = 0; i < 5; i++) rec.Write(it.Labels[i]);

                rec.Write(BitConverter.ToInt32(BitConverter.GetBytes(it.Pad), 0));

                rec.Write(it.WeaponDamageMin);
                rec.Write(it.WeaponDamageDelta);
                rec.Write(it.DamageMinVariance);
                rec.Write(it.DamageDeltaVariance);
                rec.Write(it.AttacksPerSecond);
                rec.Write(it.Armor);
                rec.Write(it.ArmorDelta);

                rec.Write(it.SNOSkill0); rec.Write(it.SkillI0);
                rec.Write(it.SNOSkill1); rec.Write(it.SkillI1);
                rec.Write(it.SNOSkill2); rec.Write(it.SkillI2);
                rec.Write(it.SNOSkill3); rec.Write(it.SkillI3);

                for (int i = 0; i < 16; i++) WriteAttribute(rec, it.Attribute[i]);

                rec.Write((int)it.Quality);

                for (int i = 0; i < 10; i++) rec.Write(it.RecipeToGrant[i]);
                for (int i = 0; i < 8; i++) rec.Write(it.TransmogsToGrant[i]);
                for (int i = 0; i < 9; i++) rec.Write(it.Massive0[i]);
                for (int i = 0; i < 6; i++) rec.Write(it.LegendaryAffixFamily[i]);
                for (int i = 0; i < 6; i++) rec.Write(it.MaxAffixLevel[i]);
                for (int i = 0; i < 6; i++) rec.Write(it.I38[i]);

                rec.Write(it.LegendaryFamily);
                rec.Write((int)it.GemT);
                rec.Write(it.CraftingTier);
                rec.Write((int)it.CraftingQuality);

                rec.Write(it.snoActorPageOfFatePortal);
                rec.Write(it.snoWorldPageOfFate1);
                rec.Write(it.snoWorldPageOfFate2);
                rec.Write(it.snoLevelAreaPageOfFatePortal);

                rec.Write(it.EnchantAffixIngredientsCount);
                for (int i = 0; i < 6; i++) WriteIngredient(rec, it.EnchantAffixIngredients[i]);

                rec.Write(it.EnchantAffixIngredientsCountX1);
                for (int i = 0; i < 6; i++) WriteIngredient(rec, it.EnchantAffixIngredientsX1[i]);

                rec.Write(it.LegendaryPowerItemReplacement);
                rec.Write(it.SeasonRequiredToDrop);

                for (int i = 0; i < 2; i++) WriteAttribute(rec, it.Attribute1[i]);

                rec.Write(it.JewelSecondaryEffectUnlockRank);
                rec.Write(it.JewelMaxRank);
                rec.Write(it.MainEffect);
                rec.Write(it.DateReleased);
                rec.Write(it.VacuumPickup);

                rec.Write(it.CostAlt2);
                rec.Write(it.DynamicCraftCostMagic);
                rec.Write(it.DynamicCraftCostRare);
                rec.Write(it.DynamicCraftAffixCount);
                rec.Write(it.SeasonCacheTreasureClass);

                if (ms.Position > RecordSize) throw new InvalidDataException($"Record overflow: wrote {ms.Position} > {RecordSize} bytes.");
                if (ms.Position < RecordSize) rec.Write(new byte[RecordSize - ms.Position]);

                bw.Write(ms.GetBuffer(), 0, RecordSize);
            }

            long recordsEnd = fs.Position;
            int itemsLengthDir = checked((int)(recordsEnd - ITEMS_BLOCK_OFF));

            long save = fs.Position;
            fs.Position = 0x230 + 4;  // write length at directory
            bw.Write(itemsLengthDir);
            fs.Position = save;
        }

        private static int DetectPreambleLength(BinaryReader br, long start, int maxInspect)
        {
            var fs = br.BaseStream;
            long saved = fs.Position;
            try
            {
                fs.Position = start;
                int zeros = 0;
                for (int i = 0; i < maxInspect; i++)
                {
                    int b = fs.ReadByte();
                    if (b < 0) break;
                    if (b == 0) zeros++;
                    else break;
                }
                if (zeros >= 17) return 17;
                if (zeros >= 16) return 16;
                return 0;
            }
            finally { fs.Position = saved; }
        }

        private static (int offset, int length) FindItemsBlock(BinaryReader br, int fileSize)
        {
            var fs = br.BaseStream;
            long saved = fs.Position;

            if (fileSize >= 0x230 + 8)
            {
                fs.Position = 0x230;
                int off = br.ReadInt32();
                int len = br.ReadInt32();
                if (off == 0x238 && len > 0 && off + len <= fileSize)
                {
                    fs.Position = saved;
                    return (off, len);
                }
                fs.Position = saved;
            }

            for (int i = 0; i < 1024; i++)
            {
                if (fs.Position + 8 > fileSize) break;
                int off = br.ReadInt32();
                int len = br.ReadInt32();

                if (off > 0 && len > 0 && off + len <= fileSize)
                {
                    long keep = fs.Position;
                    try
                    {
                        fs.Position = off;
                        int skip = DetectPreambleLength(br, off, 32);
                        fs.Position = off + skip;
                        string name = ReadString(br, 256, true);
                        if (!string.IsNullOrEmpty(name) && IsMostlyPrintable(name))
                            return (off, len);
                    }
                    finally { fs.Position = keep; }
                }
            }
            fs.Position = saved;
            return (0, 0);
        }

        private static (int offset, int length) HeuristicFindItemsBlock(BinaryReader br, int fileSize)
        {
            const int RecordSize = 1304;
            var fs = br.BaseStream;
            long saved = fs.Position;
            try
            {
                for (long pos = 0; pos <= fileSize - 256; pos += 1)
                {
                    fs.Position = pos;
                    int skip = DetectPreambleLength(br, pos, 32);
                    fs.Position = pos + skip;
                    string name = ReadString(br, 256, true);
                    if (string.IsNullOrEmpty(name) || !IsMostlyPrintable(name)) continue;

                    long cur = pos + skip;
                    int count = 0;
                    while (cur + 256 <= fileSize)
                    {
                        fs.Position = cur;
                        string n = ReadString(br, 256, true);
                        if (string.IsNullOrEmpty(n)) break;
                        count++;
                        cur += RecordSize;
                    }
                    if (count > 0)
                    {
                        long length = cur - pos;
                        return ((int)pos, (int)length);
                    }
                }
            }
            finally { fs.Position = saved; }
            return (0, 0);
        }

        private static bool IsMostlyPrintable(string s)
        {
            int printable = 0;
            foreach (char c in s) if (c >= 32 && c <= 126) printable++;
            return s.Length > 0 && printable >= Math.Max(1, (int)(0.9 * s.Length));
        }

        private static string ReadString(BinaryReader br, int byteLen, bool nullTerminated)
        {
            byte[] bytes = br.ReadBytes(byteLen);
            if (bytes.Length < byteLen) throw new EndOfStreamException();
            int end = bytes.Length;
            if (nullTerminated)
            {
                int idx = Array.IndexOf(bytes, (byte)0);
                if (idx >= 0) end = idx;
            }
            return Encoding.UTF8.GetString(bytes, 0, end);
        }

        private static ItemTable ItemTable_Read(BinaryReader br)
        {
            var it = new ItemTable();

            it.Name = ReadString(br, 256, true);
            it.Hash = StringHashHelper.HashItemName(it.Name);

            it.GBID = br.ReadInt32();
            it.PAD = br.ReadInt32();

            it.SNOActor = br.ReadInt32();
            it.ItemTypesGBID = br.ReadInt32();

            it.Flags = br.ReadInt32();
            it.DyeType = br.ReadInt32();

            it.ItemLevel = br.ReadInt32();
            it.ItemAct = (eItemAct)br.ReadInt32();

            it.AffixLevel = br.ReadInt32();
            it.BonusAffixes = br.ReadInt32();
            it.BonusMajorAffixes = br.ReadInt32();
            it.BonusMinorAffixes = br.ReadInt32();
            it.MaxSockets = br.ReadInt32();
            it.MaxStackSize = br.ReadInt32();
            it.Cost = br.ReadInt32();
            it.CostAlt = br.ReadInt32();
            it.IdentifyCost = br.ReadInt32();
            it.SellOverrideCost = br.ReadInt32();
            it.RemoveGemCost = br.ReadInt32();
            it.RequiredLevel = br.ReadInt32();
            it.CrafterRequiredLevel = br.ReadInt32();
            it.BaseDurability = br.ReadInt32();
            it.DurabilityVariance = br.ReadInt32();
            it.EnchantAffixCost = br.ReadInt32();
            it.EnchantAffixCostX1 = br.ReadInt32();
            it.TransmogUnlockCrafterLevel = br.ReadInt32();
            it.TransmogCost = br.ReadInt32();
            it.SNOBaseItem = br.ReadInt32();
            it.SNOSet = br.ReadInt32();
            it.SNOComponentTreasureClass = br.ReadInt32();
            it.SNOComponentTreasureClassMagic = br.ReadInt32();
            it.SNOComponentTreasureClassRare = br.ReadInt32();
            it.SNOComponentTreasureClassLegend = br.ReadInt32();
            it.SNORareNamePrefixStringList = br.ReadInt32();
            it.SNORareNameSuffixStringList = br.ReadInt32();
            it.StartEffect = br.ReadInt32();
            it.EndEffect = br.ReadInt32();
            it.PortraitBkgrnd = br.ReadInt32();
            it.PortraitHPBar = br.ReadInt32();
            it.PortraitBanner = br.ReadInt32();
            it.PortraitFrame = br.ReadInt32();

            it.Labels = new int[5];
            for (int i = 0; i < 5; i++) it.Labels[i] = br.ReadInt32();

            it.Pad = BitConverter.ToSingle(BitConverter.GetBytes(br.ReadInt32()), 0);

            it.WeaponDamageMin = br.ReadSingle();
            it.WeaponDamageDelta = br.ReadSingle();
            it.DamageMinVariance = br.ReadSingle();
            it.DamageDeltaVariance = br.ReadSingle();
            it.AttacksPerSecond = br.ReadSingle();
            it.Armor = br.ReadSingle();
            it.ArmorDelta = br.ReadSingle();

            it.SNOSkill0 = br.ReadInt32();
            it.SkillI0 = br.ReadInt32();
            it.SNOSkill1 = br.ReadInt32();
            it.SkillI1 = br.ReadInt32();
            it.SNOSkill2 = br.ReadInt32();
            it.SkillI2 = br.ReadInt32();
            it.SNOSkill3 = br.ReadInt32();
            it.SkillI3 = br.ReadInt32();

            it.Attribute = new AttributeSpecifier[16];
            for (int i = 0; i < 16; i++) it.Attribute[i] = AttributeSpecifier_Read(br);

            it.Quality = (ItemQuality)br.ReadInt32();

            it.RecipeToGrant = new int[10];
            for (int i = 0; i < 10; i++) it.RecipeToGrant[i] = br.ReadInt32();

            it.TransmogsToGrant = new int[8];
            for (int i = 0; i < 8; i++) it.TransmogsToGrant[i] = br.ReadInt32();

            it.Massive0 = new int[9];
            for (int i = 0; i < 9; i++) it.Massive0[i] = br.ReadInt32();

            it.LegendaryAffixFamily = new int[6];
            for (int i = 0; i < 6; i++) it.LegendaryAffixFamily[i] = br.ReadInt32();

            it.MaxAffixLevel = new int[6];
            for (int i = 0; i < 6; i++) it.MaxAffixLevel[i] = br.ReadInt32();

            it.I38 = new int[6];
            for (int i = 0; i < 6; i++) it.I38[i] = br.ReadInt32();

            it.LegendaryFamily = br.ReadInt32();
            it.GemT = (GemType)br.ReadInt32();
            it.CraftingTier = br.ReadInt32();
            it.CraftingQuality = (Alpha)br.ReadInt32();

            it.snoActorPageOfFatePortal = br.ReadInt32();
            it.snoWorldPageOfFate1 = br.ReadInt32();
            it.snoWorldPageOfFate2 = br.ReadInt32();
            it.snoLevelAreaPageOfFatePortal = br.ReadInt32();

            it.EnchantAffixIngredientsCount = br.ReadInt32();
            it.EnchantAffixIngredients = new RecipeIngredient[6];
            for (int i = 0; i < 6; i++) it.EnchantAffixIngredients[i] = RecipeIngredient_Read(br);

            it.EnchantAffixIngredientsCountX1 = br.ReadInt32();
            it.EnchantAffixIngredientsX1 = new RecipeIngredient[6];
            for (int i = 0; i < 6; i++) it.EnchantAffixIngredientsX1[i] = RecipeIngredient_Read(br);

            it.LegendaryPowerItemReplacement = br.ReadInt32();
            it.SeasonRequiredToDrop = br.ReadInt32();

            it.Attribute1 = new AttributeSpecifier[2];
            for (int i = 0; i < 2; i++) it.Attribute1[i] = AttributeSpecifier_Read(br);

            it.JewelSecondaryEffectUnlockRank = br.ReadInt32();
            it.JewelMaxRank = br.ReadInt32();
            it.MainEffect = br.ReadInt32();
            it.DateReleased = br.ReadInt32();
            it.VacuumPickup = br.ReadInt32();

            it.CostAlt2 = br.ReadInt32();
            it.DynamicCraftCostMagic = br.ReadInt32();
            it.DynamicCraftCostRare = br.ReadInt32();
            it.DynamicCraftAffixCount = br.ReadInt32();
            it.SeasonCacheTreasureClass = br.ReadInt32();

            return it;
        }

        private static AttributeSpecifier AttributeSpecifier_Read(BinaryReader br)
        {
            var spec = new AttributeSpecifier
            {
                AttributeId = br.ReadInt32(),
                SNOParam = br.ReadInt32(),
                Formula = new List<int>()
            };
            br.BaseStream.Position += 16; // skip
            return spec;
        }

        private static RecipeIngredient RecipeIngredient_Read(BinaryReader br)
        {
            return new RecipeIngredient
            {
                ItemsGBID = br.ReadInt32(),
                Count = br.ReadInt32()
            };
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

        private static void WriteAttribute(BinaryWriter bw, AttributeSpecifier a)
        {
            bw.Write(a?.AttributeId ?? 0);
            bw.Write(a?.SNOParam ?? 0);
            bw.Write(new byte[16]);
        }

        private static void WriteIngredient(BinaryWriter bw, RecipeIngredient ri)
        {
            bw.Write(ri?.ItemsGBID ?? 0);
            bw.Write(ri?.Count ?? 0);
        }

        private static T[] EnsureLen<T>(T[]? arr, int len, Func<T>? factory = null)
        {
            var a = arr ?? Array.Empty<T>();
            if (a.Length == len) return a;
            var r = new T[len];
            int copy = Math.Min(a.Length, len);
            Array.Copy(a, r, copy);
            if (factory != null) for (int i = copy; i < len; i++) r[i] = factory();
            return r;
        }

        private static int[] EnsureLen(int[]? arr, int len) => EnsureLen(arr, len, () => 0);
        private static int[] EnsureLen(int[]? arr, int len, Func<int> factory)
        {
            var a = arr ?? Array.Empty<int>();
            if (a.Length == len) return a;
            var r = new int[len];
            int copy = Math.Min(a.Length, len);
            Array.Copy(a, r, copy);
            for (int i = copy; i < len; i++) r[i] = factory();
            return r;
        }
    }

    public class GameFileCollection
    {
        public Header Header { get; set; } = Header.Default();
        public List<ItemTable> Items { get; set; } = new List<ItemTable>();
    }

    public class ItemTable
    {
        public int Hash { get; set; }
        public string Name { get; set; } = "";
        public int GBID { get; set; }
        public int PAD { get; set; }
        public int SNOActor { get; set; }
        public int ItemTypesGBID { get; set; }
        public int Flags { get; set; }
        public int DyeType { get; set; }
        public int ItemLevel { get; set; }
        public eItemAct ItemAct { get; set; }
        public int AffixLevel { get; set; }
        public int BonusAffixes { get; set; }
        public int BonusMajorAffixes { get; set; }
        public int BonusMinorAffixes { get; set; }
        public int MaxSockets { get; set; }
        public int MaxStackSize { get; set; }
        public int Cost { get; set; }
        public int CostAlt { get; set; }
        public int IdentifyCost { get; set; }
        public int SellOverrideCost { get; set; }
        public int RemoveGemCost { get; set; }
        public int RequiredLevel { get; set; }
        public int CrafterRequiredLevel { get; set; }
        public int BaseDurability { get; set; }
        public int DurabilityVariance { get; set; }
        public int EnchantAffixCost { get; set; }
        public int EnchantAffixCostX1 { get; set; }
        public int TransmogUnlockCrafterLevel { get; set; }
        public int TransmogCost { get; set; }
        public int SNOBaseItem { get; set; }
        public int SNOSet { get; set; }
        public int SNOComponentTreasureClass { get; set; }
        public int SNOComponentTreasureClassMagic { get; set; }
        public int SNOComponentTreasureClassRare { get; set; }
        public int SNOComponentTreasureClassLegend { get; set; }
        public int SNORareNamePrefixStringList { get; set; }
        public int SNORareNameSuffixStringList { get; set; }
        public int StartEffect { get; set; }
        public int EndEffect { get; set; }
        public int PortraitBkgrnd { get; set; }
        public int PortraitHPBar { get; set; }
        public int PortraitBanner { get; set; }
        public int PortraitFrame { get; set; }
        public int[] Labels { get; set; } = new int[5];
        public float Pad { get; set; }
        public float WeaponDamageMin { get; set; }
        public float WeaponDamageDelta { get; set; }
        public float DamageMinVariance { get; set; }
        public float DamageDeltaVariance { get; set; }
        public float AttacksPerSecond { get; set; }
        public float Armor { get; set; }
        public float ArmorDelta { get; set; }
        public int SNOSkill0 { get; set; }
        public int SkillI0 { get; set; }
        public int SNOSkill1 { get; set; }
        public int SkillI1 { get; set; }
        public int SNOSkill2 { get; set; }
        public int SkillI2 { get; set; }
        public int SNOSkill3 { get; set; }
        public int SkillI3 { get; set; }
        public AttributeSpecifier[] Attribute { get; set; } = new AttributeSpecifier[16];
        public ItemQuality Quality { get; set; }
        public int[] RecipeToGrant { get; set; } = new int[10];
        public int[] TransmogsToGrant { get; set; } = new int[8];
        public int[] Massive0 { get; set; } = new int[9];
        public int[] LegendaryAffixFamily { get; set; } = new int[6];
        public int[] MaxAffixLevel { get; set; } = new int[6];
        public int[] I38 { get; set; } = new int[6];
        public int LegendaryFamily { get; set; }
        public GemType GemT { get; set; }
        public int CraftingTier { get; set; }
        public Alpha CraftingQuality { get; set; }
        public int snoActorPageOfFatePortal { get; set; }
        public int snoWorldPageOfFate1 { get; set; }
        public int snoWorldPageOfFate2 { get; set; }
        public int snoLevelAreaPageOfFatePortal { get; set; }
        public int EnchantAffixIngredientsCount { get; set; }
        public RecipeIngredient[] EnchantAffixIngredients { get; set; } = new RecipeIngredient[6];
        public int EnchantAffixIngredientsCountX1 { get; set; }
        public RecipeIngredient[] EnchantAffixIngredientsX1 { get; set; } = new RecipeIngredient[6];
        public int LegendaryPowerItemReplacement { get; set; }
        public int SeasonRequiredToDrop { get; set; }
        public AttributeSpecifier[] Attribute1 { get; set; } = new AttributeSpecifier[2];
        public int JewelSecondaryEffectUnlockRank { get; set; }
        public int JewelMaxRank { get; set; }
        public int MainEffect { get; set; }
        public int DateReleased { get; set; }
        public int VacuumPickup { get; set; }
        public int CostAlt2 { get; set; }
        public int DynamicCraftCostMagic { get; set; }
        public int DynamicCraftCostRare { get; set; }
        public int DynamicCraftAffixCount { get; set; }
        public int SeasonCacheTreasureClass { get; set; }
    }

    public class AttributeSpecifier
    {
        public int AttributeId { get; set; }
        public int SNOParam { get; set; }
        public List<int> Formula { get; set; } = new List<int>();
    }

    public class RecipeIngredient
    {
        public int ItemsGBID { get; set; }
        public int Count { get; set; }
    }

    public enum ItemQuality
    {
        Invalid = -1,
        Inferior = 0,
        Normal = 1,
        Superior = 2,
        Magic1 = 3,
        Magic2 = 4,
        Magic3 = 5,
        Rare4 = 6,
        Rare5 = 7,
        Rare6 = 8,
        Legendary = 9,
        Special = 10,
        Set = 11
    }

    [Flags]
    public enum eItemAct
    {
        Invalid = -1,
        A1 = 0,
        A2 = 100,
        A3 = 200,
        A4 = 300,
        A5 = 400,
        Test = 1000,
        OpenWorld = 3000
    }

    public enum GemType : int
    {
        Amethyst = 1,
        Emerald = 2,
        Ruby = 3,
        Topaz = 4,
        Diamond = 5
    }

    public enum Alpha : int
    {
        A = 1,
        B = 2,
        C = 3,
        D = 4
    }

    public static class StringHashHelper
    {
        public static int HashItemName(string name)
        {
            int hash = 0;
            foreach (char c in name) hash = (hash << 5) + hash + c;
            return hash;
        }
    }
}
