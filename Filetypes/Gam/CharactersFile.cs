using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using D3Edit.Core;

namespace D3Edit.Filetypes.Gam
{
    public static class CharactersIO
    {
        public static CharactersJsonFile ReadGamFile(string filePath)
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
                if (blockLen <= 0)
                    throw new InvalidDataException("Characters block pointer invalid.");
            }

            int preamble = DetectPreamble(br, blockOff, 32);
            fs.Position = blockOff + (preamble > 0 ? preamble : 0x10); // land on 0x248 in standard case

            var recs = new List<CharacterRecord>();
            const int RecordSize = 504;
            long end = Math.Min(fs.Length, blockOff + blockLen);

            while (fs.Position + 256 <= end)
            {
                long start = fs.Position;
                string peek = ReadFixedString(br, 256, true);
                if (string.IsNullOrEmpty(peek)) break;   // hit terminator / padding
                fs.Position = start;

                var c = ReadOne(br);
                recs.Add(c);

                fs.Position = start + RecordSize; // hard-step to next slot
            }

            var outHeader = header;
            outHeader.BalanceType = balanceType;
            outHeader.I0 = i0;
            outHeader.I1 = i1;

            return new CharactersJsonFile { Header = outHeader, Records = recs };
        }

        public static void WriteGamFile(string filePath, CharactersJsonFile data)
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
            bw.Write(0); // length placeholder

            while (fs.Position < BLOCK_OFF) bw.Write((byte)0);

            bw.Write(new byte[0x10]);

            long recordsStart = fs.Position;
            foreach (var c in data.Records ?? Enumerable.Empty<CharacterRecord>())
            {
                WriteOne(bw, c);
            }
            long recordsEnd = fs.Position;

            long save = fs.Position;
            fs.Position = 0x230 + 4;
            bw.Write(checked((int)(recordsEnd - BLOCK_OFF)));
            fs.Position = save;
        }

        private static CharacterRecord ReadOne(BinaryReader br)
        {
            var c = new CharacterRecord
            {
                Name = ReadFixedString(br, 256, true),

                I0 = br.ReadInt32(),
                I1 = br.ReadInt32(),
                SNOMaleActor = br.ReadInt32(),
                SNOFemaleActor = br.ReadInt32(),
                SNOInventory = br.ReadInt32(),
                MaxTrainableSkills = br.ReadInt32(),
                SNOStartingLMBSkill = br.ReadInt32(),
                SNOStartingRMBSkill = br.ReadInt32(),
                SNOSKillKit0 = br.ReadInt32(),
                SNOSKillKit1 = br.ReadInt32(),
                SNOSKillKit2 = br.ReadInt32(),
                SNOSKillKit3 = br.ReadInt32(),
                PrimaryResource = (Resource)br.ReadInt32(),
                SecondaryResource = (Resource)br.ReadInt32(),
                CoreAttribute = (PrimaryAttribute)br.ReadInt32(),
                PlayerAwarenessRadius = br.ReadSingle(),
                IsRanged = br.ReadInt32(),

                Strength = br.ReadSingle(),
                Dexterity = br.ReadSingle(),
                Intelligence = br.ReadSingle(),
                Vitality = br.ReadSingle(),
                HitpointsMax = br.ReadSingle(),
                HitpointsFactorLevel = br.ReadSingle(),
                HPRegen = br.ReadSingle(),
                ClassDamageReductionPercent = br.ReadSingle(),
                ClassDamageReductionPercentPVP = br.ReadSingle(),
                PrimaryResourceBase = br.ReadSingle(),
                PrimaryResourceFactorLevel = br.ReadSingle(),
                PrimaryResourceRegen = br.ReadSingle(),
                SecondaryResourceBase = br.ReadSingle(),
                SecondaryResourceFactorLevel = br.ReadSingle(),
                SecondaryResourceRegen = br.ReadSingle(),
                Armor = br.ReadSingle(),
                Dmg = br.ReadSingle(),
                WalkingRate = br.ReadSingle(),
                RunningRate = br.ReadSingle(),
                SprintRate = br.ReadSingle(),
                ProjRate = br.ReadSingle(),
                CritDamageCap = br.ReadSingle(),
                CritPercentBase = br.ReadSingle(),
                CritPercentCap = br.ReadSingle(),
                DodgeRatingBase = br.ReadSingle(),
                GetHitMaxBase = br.ReadSingle(),
                GetHitMaxPerLevel = br.ReadSingle(),
                GetHitRecoveryBase = br.ReadSingle(),
                GetHitRecoveryPerLevel = br.ReadSingle(),
                ResistPhysical = br.ReadSingle(),
                ResistFire = br.ReadSingle(),
                ResistLightning = br.ReadSingle(),
                ResistCold = br.ReadSingle(),
                ResistPoison = br.ReadSingle(),
                ResistArcane = br.ReadSingle(),
                ResistChill = br.ReadSingle(),
                ResistStun = br.ReadSingle(),
                KnockbackWeight = br.ReadSingle(),
                OOCHealthRegen = br.ReadSingle(),
                OOCManaRegen = br.ReadSingle(),
                PotionDilutionDuration = br.ReadSingle(),
                PotionDilutionScalar = br.ReadSingle(),
                DualWieldBothAttackChance = br.ReadSingle(),
                Freeze_Capacity = br.ReadSingle(),
                Thaw_Rate = br.ReadSingle(),
            };

            return c;
        }

        private static void WriteOne(BinaryWriter bw, CharacterRecord c)
        {
            const int RecordSize = 504;
            long start = bw.BaseStream.Position;

            WriteFixedString(bw, c?.Name ?? string.Empty, 256);

            bw.Write(c?.I0 ?? 0);
            bw.Write(c?.I1 ?? 0);
            bw.Write(c?.SNOMaleActor ?? 0);
            bw.Write(c?.SNOFemaleActor ?? 0);
            bw.Write(c?.SNOInventory ?? 0);
            bw.Write(c?.MaxTrainableSkills ?? 0);
            bw.Write(c?.SNOStartingLMBSkill ?? 0);
            bw.Write(c?.SNOStartingRMBSkill ?? 0);
            bw.Write(c?.SNOSKillKit0 ?? 0);
            bw.Write(c?.SNOSKillKit1 ?? 0);
            bw.Write(c?.SNOSKillKit2 ?? 0);
            bw.Write(c?.SNOSKillKit3 ?? 0);
            bw.Write((int)(c?.PrimaryResource ?? Resource.None));
            bw.Write((int)(c?.SecondaryResource ?? Resource.None));
            bw.Write((int)(c?.CoreAttribute ?? PrimaryAttribute.None));
            bw.Write(c?.PlayerAwarenessRadius ?? 0);
            bw.Write(c?.IsRanged ?? 0);

            bw.Write(c?.Strength ?? 0);
            bw.Write(c?.Dexterity ?? 0);
            bw.Write(c?.Intelligence ?? 0);
            bw.Write(c?.Vitality ?? 0);
            bw.Write(c?.HitpointsMax ?? 0);
            bw.Write(c?.HitpointsFactorLevel ?? 0);
            bw.Write(c?.HPRegen ?? 0);
            bw.Write(c?.ClassDamageReductionPercent ?? 0);
            bw.Write(c?.ClassDamageReductionPercentPVP ?? 0);
            bw.Write(c?.PrimaryResourceBase ?? 0);
            bw.Write(c?.PrimaryResourceFactorLevel ?? 0);
            bw.Write(c?.PrimaryResourceRegen ?? 0);
            bw.Write(c?.SecondaryResourceBase ?? 0);
            bw.Write(c?.SecondaryResourceFactorLevel ?? 0);
            bw.Write(c?.SecondaryResourceRegen ?? 0);
            bw.Write(c?.Armor ?? 0);
            bw.Write(c?.Dmg ?? 0);
            bw.Write(c?.WalkingRate ?? 0);
            bw.Write(c?.RunningRate ?? 0);
            bw.Write(c?.SprintRate ?? 0);
            bw.Write(c?.ProjRate ?? 0);
            bw.Write(c?.CritDamageCap ?? 0);
            bw.Write(c?.CritPercentBase ?? 0);
            bw.Write(c?.CritPercentCap ?? 0);
            bw.Write(c?.DodgeRatingBase ?? 0);
            bw.Write(c?.GetHitMaxBase ?? 0);
            bw.Write(c?.GetHitMaxPerLevel ?? 0);
            bw.Write(c?.GetHitRecoveryBase ?? 0);
            bw.Write(c?.GetHitRecoveryPerLevel ?? 0);
            bw.Write(c?.ResistPhysical ?? 0);
            bw.Write(c?.ResistFire ?? 0);
            bw.Write(c?.ResistLightning ?? 0);
            bw.Write(c?.ResistCold ?? 0);
            bw.Write(c?.ResistPoison ?? 0);
            bw.Write(c?.ResistArcane ?? 0);
            bw.Write(c?.ResistChill ?? 0);
            bw.Write(c?.ResistStun ?? 0);
            bw.Write(c?.KnockbackWeight ?? 0);
            bw.Write(c?.OOCHealthRegen ?? 0);
            bw.Write(c?.OOCManaRegen ?? 0);
            bw.Write(c?.PotionDilutionDuration ?? 0);
            bw.Write(c?.PotionDilutionScalar ?? 0);
            bw.Write(c?.DualWieldBothAttackChance ?? 0);
            bw.Write(c?.Freeze_Capacity ?? 0);
            bw.Write(c?.Thaw_Rate ?? 0);

            long wrote = bw.BaseStream.Position - start;
            if (wrote > RecordSize)
                throw new InvalidDataException($"Characters record overflow ({wrote} > {RecordSize}).");
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
                if (zeros >= 17) return 17;   // some builds show 17
                if (zeros >= 16) return 16;   // originals commonly 16
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
    }

    public class CharactersJsonFile
    {
        public Header Header { get; set; } = Header.Default();
        public List<CharacterRecord> Records { get; set; } = new List<CharacterRecord>();
    }

    public class CharacterRecord
    {
        public string Name { get; set; } = "";

        public int I0 { get; set; }
        public int I1 { get; set; }
        public int SNOMaleActor { get; set; }
        public int SNOFemaleActor { get; set; }
        public int SNOInventory { get; set; }
        public int MaxTrainableSkills { get; set; }
        public int SNOStartingLMBSkill { get; set; }
        public int SNOStartingRMBSkill { get; set; }
        public int SNOSKillKit0 { get; set; }
        public int SNOSKillKit1 { get; set; }
        public int SNOSKillKit2 { get; set; }
        public int SNOSKillKit3 { get; set; }

        public Resource PrimaryResource { get; set; }
        public Resource SecondaryResource { get; set; }
        public PrimaryAttribute CoreAttribute { get; set; }

        public float PlayerAwarenessRadius { get; set; }
        public int IsRanged { get; set; }

        public float Strength { get; set; }
        public float Dexterity { get; set; }
        public float Intelligence { get; set; }
        public float Vitality { get; set; }
        public float HitpointsMax { get; set; }
        public float HitpointsFactorLevel { get; set; }
        public float HPRegen { get; set; }
        public float ClassDamageReductionPercent { get; set; }
        public float ClassDamageReductionPercentPVP { get; set; }
        public float PrimaryResourceBase { get; set; }
        public float PrimaryResourceFactorLevel { get; set; }
        public float PrimaryResourceRegen { get; set; }
        public float SecondaryResourceBase { get; set; }
        public float SecondaryResourceFactorLevel { get; set; }
        public float SecondaryResourceRegen { get; set; }
        public float Armor { get; set; }
        public float Dmg { get; set; }
        public float WalkingRate { get; set; }
        public float RunningRate { get; set; }
        public float SprintRate { get; set; }
        public float ProjRate { get; set; }
        public float CritDamageCap { get; set; }
        public float CritPercentBase { get; set; }
        public float CritPercentCap { get; set; }
        public float DodgeRatingBase { get; set; }
        public float GetHitMaxBase { get; set; }
        public float GetHitMaxPerLevel { get; set; }
        public float GetHitRecoveryBase { get; set; }
        public float GetHitRecoveryPerLevel { get; set; }
        public float ResistPhysical { get; set; }
        public float ResistFire { get; set; }
        public float ResistLightning { get; set; }
        public float ResistCold { get; set; }
        public float ResistPoison { get; set; }
        public float ResistArcane { get; set; }
        public float ResistChill { get; set; }
        public float ResistStun { get; set; }
        public float KnockbackWeight { get; set; }
        public float OOCHealthRegen { get; set; }
        public float OOCManaRegen { get; set; }
        public float PotionDilutionDuration { get; set; }
        public float PotionDilutionScalar { get; set; }
        public float DualWieldBothAttackChance { get; set; }
        public float Freeze_Capacity { get; set; }
        public float Thaw_Rate { get; set; }
    }

    public enum Resource : int
    {
        None = -1,
        Mana = 0,
        Arcanum = 1,
        Fury = 2,
        Spirit = 3,
        Power = 4,
        Hatred = 5,
        Discipline = 6,
        Faith = 7,
        Essence = 8
    }

    public enum PrimaryAttribute : int
    {
        None = -1,
        Strength = 0,
        Dexterity = 1,
        Intelligence = 2
    }
}
