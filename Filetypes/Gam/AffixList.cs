using D3Edit.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json.Serialization;

namespace D3Edit.Filetypes.Gam
{
    public enum DamageAffixType
    {
        None = 0, Lightning = 1, Cold = 2, Fire = 3, Poison = 4, Arcane = 5,
        WitchdoctorDamage = 6, LifeSteal = 7, ManaSteal = 8, MagicFind = 9,
        GoldFind = 10, AttackSpeedBonus = 11, CastSpeedBonus = 12, Holy = 13, WizardDamage = 14
    }
    public enum Class
    {
        None = -1, DemonHunter = 0, Barbarian = 1, Wizard = 2, Witchdoctor = 3, Monk = 4, Crusader = 5, Necromancer = 6
    }
    public enum AffixType
    {
        Prefix = 0, Suffix = 1, Inherit = 2, Title = 5, Quality = 6, Immunity = 7, Random = 9, Enhancement = 10, SocketEnhancement = 11,
    }

    public sealed class AttributeSpecifierStub
    {
        public int AttributeId { get; set; }
        public int SNOParam { get; set; }
        public int FormulaCount { get; set; }
        public int FormulaOffset { get; set; } // absolute file offset; preserved
    }

    public sealed class AffixRecord
    {
        public string Name { get; set; } = "";
        public int I0 { get; set; }
        public int AffixLevel { get; set; }
        public int SupMask { get; set; }
        public int Frequency { get; set; }
        public int DemonHunterFrequency { get; set; }
        public int BarbarianFrequency { get; set; }
        public int WizardFrequency { get; set; }
        public int WitchDoctorFrequency { get; set; }
        public int MonkFrequency { get; set; }
        public int CrafterRequiredLevel { get; set; }
        public int NecromancerFrequency { get; set; }
        public int HirelingNoneFrequency { get; set; }
        public int TemplarFrequency { get; set; }
        public int ScoundrelFrequency { get; set; }
        public int EnchantressFrequency { get; set; }
        public int AffixLevelMin { get; set; }
        public int AffixLevelMax { get; set; }
        public int Cost { get; set; }
        public int IdentifyCost { get; set; }
        public int OverrideLevelReq { get; set; }
        public DamageAffixType ItemEffectType { get; set; }
        public int ItemEffectLevel { get; set; }
        public int ConvertsTo { get; set; }
        public int LegendaryUprankAffix { get; set; }
        public int SNORareNamePrefixStringList { get; set; }
        public int SNORareNameSuffixStringList { get; set; }
        public int AffixFamily0 { get; set; }
        public int AffixFamily1 { get; set; }
        public Class PlayerClass { get; set; }
        public int ExclusionCategory { get; set; }
        public int[] ExcludedCategories { get; set; } = new int[6];
        public int[] ItemGroup { get; set; } = new int[24];
        public int[] LegendaryAllowedTypes { get; set; } = new int[24];
        public int AllowedQualityLevels { get; set; }
        public AffixType AffixType { get; set; }
        public int AssociatedAffix { get; set; }
        public AttributeSpecifierStub[] AttributeSpecifiers { get; set; } = new AttributeSpecifierStub[4];
        public int AffixGroup { get; set; }
    }

    public sealed class AffixJsonFile
    {
        public Header Header { get; set; } = Header.Default();
        public List<AffixRecord> Records { get; set; } = new List<AffixRecord>();
    }

    public static class AffixListReader
    {
        public const int FirstRecordOffset = 584; // 0x248
        public const int RecordSize = 784;
        private const int MaxNameBytes = 256;

        public static (Header header, List<AffixRecord> records) ReadAll(string path)
        {
            byte[] file = File.ReadAllBytes(path);
            if (file.Length < FirstRecordOffset + MaxNameBytes)
                throw new InvalidDataException("File too small.");

            var header = Header.Read(new ReadOnlySpan<byte>(file, 0, 28));

            var name0 = Bin.ZStr(new ReadOnlySpan<byte>(file, FirstRecordOffset, MaxNameBytes));
            if (name0.Length == 0)
                return (header, new List<AffixRecord>(0)); // keep zero-record case explicit

            int count = 0;
            for (int off = FirstRecordOffset; off + MaxNameBytes <= file.Length; off += RecordSize)
            {
                string n = Bin.ZStr(new ReadOnlySpan<byte>(file, off, MaxNameBytes));
                if (string.IsNullOrWhiteSpace(n)) break;
                count++;
            }

            var list = new List<AffixRecord>(count);
            int p = FirstRecordOffset;
            for (int i = 0; i < count; i++, p += RecordSize)
                list.Add(ReadOne(file, p));

            return (header, list);
        }

        private static AffixRecord ReadOne(byte[] file, int baseOff)
        {
            int p = baseOff;

            string Name = Bin.ZStr(new ReadOnlySpan<byte>(file, p, 256)); p += 256;

            int I0 = Bin.I32(file.AsSpan(p, 4)); p += 4;
            int AffixLevel = Bin.I32(file.AsSpan(p, 4)); p += 4;
            int SupMask = Bin.I32(file.AsSpan(p, 4)); p += 4;
            int Frequency = Bin.I32(file.AsSpan(p, 4)); p += 4;
            int DemonHunterFrequency = Bin.I32(file.AsSpan(p, 4)); p += 4;
            int BarbarianFrequency = Bin.I32(file.AsSpan(p, 4)); p += 4;
            int WizardFrequency = Bin.I32(file.AsSpan(p, 4)); p += 4;
            int WitchDoctorFrequency = Bin.I32(file.AsSpan(p, 4)); p += 4;
            int MonkFrequency = Bin.I32(file.AsSpan(p, 4)); p += 4;

            int CrafterRequiredLevel_1 = Bin.I32(file.AsSpan(p, 4)); p += 4;
            int NecromancerFrequency = Bin.I32(file.AsSpan(p, 4)); p += 4;
            int HirelingNoneFrequency = Bin.I32(file.AsSpan(p, 4)); p += 4;
            int TemplarFrequency = Bin.I32(file.AsSpan(p, 4)); p += 4;
            int ScoundrelFrequency = Bin.I32(file.AsSpan(p, 4)); p += 4;
            int EnchantressFrequency = Bin.I32(file.AsSpan(p, 4)); p += 4;
            int AffixLevelMin = Bin.I32(file.AsSpan(p, 4)); p += 4;
            int AffixLevelMax = Bin.I32(file.AsSpan(p, 4)); p += 4;
            int Cost = Bin.I32(file.AsSpan(p, 4)); p += 4;
            int IdentifyCost = Bin.I32(file.AsSpan(p, 4)); p += 4;
            int OverrideLevelReq = Bin.I32(file.AsSpan(p, 4)); p += 4;

            int _CrafterRequiredLevel_2 = Bin.I32(file.AsSpan(p, 4)); p += 4;

            var ItemEffectType = (DamageAffixType)Bin.I32(file.AsSpan(p, 4)); p += 4;
            int ItemEffectLevel = Bin.I32(file.AsSpan(p, 4)); p += 4;
            int ConvertsTo = Bin.I32(file.AsSpan(p, 4)); p += 4;
            int LegendaryUprankAffix = Bin.I32(file.AsSpan(p, 4)); p += 4;
            int SNORareNamePrefixStringList = Bin.I32(file.AsSpan(p, 4)); p += 4;
            int SNORareNameSuffixStringList = Bin.I32(file.AsSpan(p, 4)); p += 4;
            int AffixFamily0 = Bin.I32(file.AsSpan(p, 4)); p += 4;
            int AffixFamily1 = Bin.I32(file.AsSpan(p, 4)); p += 4;
            var PlayerClass = (Class)Bin.I32(file.AsSpan(p, 4)); p += 4;
            int ExclusionCategory = Bin.I32(file.AsSpan(p, 4)); p += 4;

            var ExcludedCategories = new int[6];
            for (int i = 0; i < 6; i++) { ExcludedCategories[i] = Bin.I32(file.AsSpan(p, 4)); p += 4; }

            var ItemGroup = new int[24];
            for (int i = 0; i < 24; i++) { ItemGroup[i] = Bin.I32(file.AsSpan(p, 4)); p += 4; }

            var LegendaryAllowedTypes = new int[24];
            for (int i = 0; i < 24; i++) { LegendaryAllowedTypes[i] = Bin.I32(file.AsSpan(p, 4)); p += 4; }

            int AllowedQualityLevels = Bin.I32(file.AsSpan(p, 4)); p += 4;
            var AffixType = (AffixType)Bin.I32(file.AsSpan(p, 4)); p += 4;
            int AssociatedAffix = Bin.I32(file.AsSpan(p, 4)); p += 4;

            var attr = new AttributeSpecifierStub[4];
            for (int i = 0; i < 4; i++)
            {
                var spec = new AttributeSpecifierStub
                {
                    AttributeId = Bin.I32(file.AsSpan(p, 4)),
                    SNOParam = Bin.I32(file.AsSpan(p + 4, 4))
                };
                p += 8;
                p += 8; // unknown skip
                spec.FormulaCount = Bin.I32(file.AsSpan(p, 4));
                spec.FormulaOffset = Bin.I32(file.AsSpan(p + 4, 4));
                p += 8;
                attr[i] = spec;
            }

            p += 72;
            int AffixGroup = Bin.I32(file.AsSpan(p, 4)); p += 4;
            p += 4;

            int consumed = p - baseOff;
            if (consumed != RecordSize)
                throw new InvalidDataException($"Record mismatch: consumed {consumed}, expected {RecordSize}.");

            return new AffixRecord
            {
                Name = Name,
                I0 = I0,
                AffixLevel = AffixLevel,
                SupMask = SupMask,
                Frequency = Frequency,
                DemonHunterFrequency = DemonHunterFrequency,
                BarbarianFrequency = BarbarianFrequency,
                WizardFrequency = WizardFrequency,
                WitchDoctorFrequency = WitchDoctorFrequency,
                MonkFrequency = MonkFrequency,
                CrafterRequiredLevel = CrafterRequiredLevel_1,
                NecromancerFrequency = NecromancerFrequency,
                HirelingNoneFrequency = HirelingNoneFrequency,
                TemplarFrequency = TemplarFrequency,
                ScoundrelFrequency = ScoundrelFrequency,
                EnchantressFrequency = EnchantressFrequency,
                AffixLevelMin = AffixLevelMin,
                AffixLevelMax = AffixLevelMax,
                Cost = Cost,
                IdentifyCost = IdentifyCost,
                OverrideLevelReq = OverrideLevelReq,
                ItemEffectType = ItemEffectType,
                ItemEffectLevel = ItemEffectLevel,
                ConvertsTo = ConvertsTo,
                LegendaryUprankAffix = LegendaryUprankAffix,
                SNORareNamePrefixStringList = SNORareNamePrefixStringList,
                SNORareNameSuffixStringList = SNORareNameSuffixStringList,
                AffixFamily0 = AffixFamily0,
                AffixFamily1 = AffixFamily1,
                PlayerClass = PlayerClass,
                ExclusionCategory = ExclusionCategory,
                ExcludedCategories = ExcludedCategories,
                ItemGroup = ItemGroup,
                LegendaryAllowedTypes = LegendaryAllowedTypes,
                AllowedQualityLevels = AllowedQualityLevels,
                AffixType = AffixType,
                AssociatedAffix = AssociatedAffix,
                AttributeSpecifiers = attr,
                AffixGroup = AffixGroup
            };
        }
    }

    public static class AffixListWriter
    {
        public const int FirstRecordOffset = AffixListReader.FirstRecordOffset; // 584
        public const int RecordSize = AffixListReader.RecordSize;               // 784

        public static void WriteAll(string outPath, Header header, IReadOnlyList<AffixRecord> recs)
        {
            using var fs = new FileStream(outPath, FileMode.Create, FileAccess.Write, FileShare.None);

            header.Write(fs);

            long pad = FirstRecordOffset - fs.Position;
            if (pad < 0) throw new InvalidOperationException("Header larger than expected.");
            if (pad > 0) Bin.WriteZeros(fs, (int)pad);

            foreach (var r in recs) WriteOne(fs, r);
        }

        private static void WriteOne(Stream w, AffixRecord r)
        {
            var exCats = NormalizeFixed(r.ExcludedCategories, 6, -1);
            var itemGrp = NormalizeFixed(r.ItemGroup, 24, -1);
            var legAllow = NormalizeFixed(r.LegendaryAllowedTypes, 24, -1);
            var attrs = NormalizeAttrs(r.AttributeSpecifiers, 4);

            long start = w.Position;

            Bin.WriteFixedAsciiZ(w, r.Name ?? "", 256);

            Bin.WriteI32(w, r.I0);
            Bin.WriteI32(w, r.AffixLevel);
            Bin.WriteI32(w, r.SupMask);
            Bin.WriteI32(w, r.Frequency);
            Bin.WriteI32(w, r.DemonHunterFrequency);
            Bin.WriteI32(w, r.BarbarianFrequency);
            Bin.WriteI32(w, r.WizardFrequency);
            Bin.WriteI32(w, r.WitchDoctorFrequency);
            Bin.WriteI32(w, r.MonkFrequency);

            Bin.WriteI32(w, r.CrafterRequiredLevel);
            Bin.WriteI32(w, r.NecromancerFrequency);
            Bin.WriteI32(w, r.HirelingNoneFrequency);
            Bin.WriteI32(w, r.TemplarFrequency);
            Bin.WriteI32(w, r.ScoundrelFrequency);
            Bin.WriteI32(w, r.EnchantressFrequency);
            Bin.WriteI32(w, r.AffixLevelMin);
            Bin.WriteI32(w, r.AffixLevelMax);
            Bin.WriteI32(w, r.Cost);
            Bin.WriteI32(w, r.IdentifyCost);
            Bin.WriteI32(w, r.OverrideLevelReq);

            Bin.WriteI32(w, r.CrafterRequiredLevel); // duplicated

            Bin.WriteI32(w, (int)r.ItemEffectType);
            Bin.WriteI32(w, r.ItemEffectLevel);
            Bin.WriteI32(w, r.ConvertsTo);
            Bin.WriteI32(w, r.LegendaryUprankAffix);
            Bin.WriteI32(w, r.SNORareNamePrefixStringList);
            Bin.WriteI32(w, r.SNORareNameSuffixStringList);
            Bin.WriteI32(w, r.AffixFamily0);
            Bin.WriteI32(w, r.AffixFamily1);
            Bin.WriteI32(w, (int)r.PlayerClass);
            Bin.WriteI32(w, r.ExclusionCategory);

            foreach (var v in exCats) Bin.WriteI32(w, v);
            foreach (var v in itemGrp) Bin.WriteI32(w, v);
            foreach (var v in legAllow) Bin.WriteI32(w, v);

            Bin.WriteI32(w, r.AllowedQualityLevels);
            Bin.WriteI32(w, (int)r.AffixType);
            Bin.WriteI32(w, r.AssociatedAffix);

            foreach (var a in attrs)
            {
                Bin.WriteI32(w, a.AttributeId);
                Bin.WriteI32(w, a.SNOParam);
                Bin.WriteZeros(w, 8);
                Bin.WriteI32(w, a.FormulaCount);
                Bin.WriteI32(w, a.FormulaOffset);
            }

            Bin.WriteZeros(w, 72);
            Bin.WriteI32(w, r.AffixGroup);
            Bin.WriteI32(w, 0); // trailing pad

            long written = w.Position - start;
            if (written != RecordSize)
                throw new InvalidOperationException($"Wrote {written} bytes, expected {RecordSize}.");
        }

        private static int[] NormalizeFixed(int[]? src, int len, int pad)
        {
            var dst = new int[len];
            int n = Math.Min(src?.Length ?? 0, len);
            if (src != null) Array.Copy(src, dst, n);
            for (int i = n; i < len; i++) dst[i] = pad;
            return dst;
        }
        private static AttributeSpecifierStub[] NormalizeAttrs(AttributeSpecifierStub[]? src, int len)
        {
            var dst = new AttributeSpecifierStub[len];
            for (int i = 0; i < len; i++) dst[i] = new AttributeSpecifierStub();
            if (src == null) return dst;
            int n = Math.Min(src.Length, len);
            for (int i = 0; i < n; i++)
            {
                dst[i].AttributeId = src[i]?.AttributeId ?? 0;
                dst[i].SNOParam = src[i]?.SNOParam ?? -1;
                dst[i].FormulaCount = src[i]?.FormulaCount ?? 0;
                dst[i].FormulaOffset = src[i]?.FormulaOffset ?? 0;
            }
            return dst;
        }
    }
}
