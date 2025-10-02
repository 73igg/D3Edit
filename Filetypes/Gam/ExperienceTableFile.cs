using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using D3Edit.Core;

namespace D3Edit.Filetypes.Gam
{
    public static class ExperienceTableIO
    {
        public static ExperienceTableJsonFile ReadGamFile(string filePath)
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var br = new BinaryReader(fs, Encoding.UTF8, leaveOpen: false);

            var header = Header.Read(br);

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
                    throw new InvalidDataException("ExperienceTable block pointer invalid.");
            }

            fs.Position = blockOff;
            int preamble = DetectPreamble(br, 32); // accept 0/16/17
            fs.Position = blockOff + preamble;

            var recs = new List<ExperienceTableRecord>();
            const int RecordSize = 468; // <-- FIX: was 224
            long end = Math.Min(fs.Length, (long)blockOff + blockLen);

            while (fs.Position + 4 /*at least something to read*/ <= end)
            {
                long start = fs.Position;
                var rec = ReadRecord(br);
                recs.Add(rec);

                long next = start + RecordSize;
                if (next <= fs.Length)
                    fs.Position = next;
                else
                    break;
            }

            return new ExperienceTableJsonFile
            {
                Header = header,
                Records = recs
            };
        }

        public static void WriteGamFile(string filePath, ExperienceTableJsonFile data)
        {
            using var fs = new FileStream(filePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
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

            fs.Position = BLOCK_OFF;
            for (int i = 0; i < 16; i++) bw.Write((byte)0);

            long blockStart = fs.Position;
            foreach (var r in data.Records ?? Enumerable.Empty<ExperienceTableRecord>())
                WriteRecord(bw, r);
            long blockEnd = fs.Position;

            long save = fs.Position;
            fs.Position = 0x234; // BLOCK_OFF(0x238) written at 0x230, len at 0x234
            bw.Write(checked((int)(blockEnd - BLOCK_OFF)));
            fs.Position = save;

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
                    if (s.Position >= s.Length) break;
                    byte b = br.ReadByte();
                    if (b == 0) zeros++;
                    else break;
                }
                if (zeros == 0 || zeros == 16 || zeros == 17) return zeros;
                return zeros;
            }
            finally
            {
                s.Position = saved;
            }
        }

        private static ExperienceTableRecord ReadRecord(BinaryReader r)
        {
            var rec = new ExperienceTableRecord();


            rec.Level = r.ReadInt32();
            rec.Experience = r.ReadInt32();
            rec.ParagonExperience = r.ReadInt32();
            rec.LegendaryGemLevelReq = r.ReadInt32();

            rec.GoldDropScalar = r.ReadSingle();
            rec.MagicFindScalar = r.ReadSingle();
            rec.LegendaryFindScalar = r.ReadSingle();

            rec.EnchantCost = r.ReadInt32();
            rec.GambleCost = r.ReadInt32();
            rec.GemUpgradeChanceBonus = r.ReadSingle();

            rec.CraftingMatScalar = r.ReadSingle();
            rec.ReforgeCostScalar = r.ReadSingle();
            rec.ImbueCostScalar = r.ReadSingle();

            rec.BountyXPMult = r.ReadSingle();
            rec.RiftXPMult = r.ReadSingle();
            rec.GriftXPMult = r.ReadSingle();

            rec.HellfireXPMult = r.ReadSingle();
            rec.ShrineXPMult = r.ReadSingle();

            rec.MonsterHPScalar = r.ReadSingle();
            rec.MonsterDmgScalar = r.ReadSingle();

            rec.PVPXPWin = r.ReadInt32();
            rec.PVPNormalXPWin = r.ReadInt32();
            rec.PVPTokensWin = r.ReadInt32();
            rec.PVPAltXPWin = r.ReadInt32();

            rec.PVPXPLoss = r.ReadInt32();
            rec.PVPNormalXPLoss = r.ReadInt32();
            rec.PVPTokensLoss = r.ReadInt32();
            rec.PVPAltXPLoss = r.ReadInt32();

            rec.PVPXPTie = r.ReadInt32();
            rec.PVPNormalXPTie = r.ReadInt32();
            rec.PVPTokensTie = r.ReadInt32();
            rec.PVPAltXPTie = r.ReadInt32();

            rec.GoldCostLevelScalar = r.ReadSingle();

            rec.SidekickPrimaryStatIdeal = r.ReadInt32();
            rec.SidekickVitalityIdeal = r.ReadInt32();
            rec.SidekickTotalArmorIdeal = r.ReadInt32();
            rec.SidekickTotalResistIdeal = r.ReadInt32();
            rec.SidekickTargetLifeOnHitIdeal = r.ReadInt32();
            rec.SidekickTargetDPSIdeal = r.ReadInt32();

            rec.GearXPScalar = r.ReadSingle();

            return rec;
        }

        private static void WriteRecord(BinaryWriter w, ExperienceTableRecord r)
        {
            long start = w.BaseStream.Position;

            w.Write(r?.Level ?? 0);
            w.Write(r?.Experience ?? 0);
            w.Write(r?.ParagonExperience ?? 0);
            w.Write(r?.LegendaryGemLevelReq ?? 0);

            w.Write(r?.GoldDropScalar ?? 0);
            w.Write(r?.MagicFindScalar ?? 0);
            w.Write(r?.LegendaryFindScalar ?? 0);

            w.Write(r?.EnchantCost ?? 0);
            w.Write(r?.GambleCost ?? 0);
            w.Write(r?.GemUpgradeChanceBonus ?? 0);

            w.Write(r?.CraftingMatScalar ?? 0);
            w.Write(r?.ReforgeCostScalar ?? 0);
            w.Write(r?.ImbueCostScalar ?? 0);

            w.Write(r?.BountyXPMult ?? 0);
            w.Write(r?.RiftXPMult ?? 0);
            w.Write(r?.GriftXPMult ?? 0);

            w.Write(r?.HellfireXPMult ?? 0);
            w.Write(r?.ShrineXPMult ?? 0);

            w.Write(r?.MonsterHPScalar ?? 0);
            w.Write(r?.MonsterDmgScalar ?? 0);

            w.Write(r?.PVPXPWin ?? 0);
            w.Write(r?.PVPNormalXPWin ?? 0);
            w.Write(r?.PVPTokensWin ?? 0);
            w.Write(r?.PVPAltXPWin ?? 0);

            w.Write(r?.PVPXPLoss ?? 0);
            w.Write(r?.PVPNormalXPLoss ?? 0);
            w.Write(r?.PVPTokensLoss ?? 0);
            w.Write(r?.PVPAltXPLoss ?? 0);

            w.Write(r?.PVPXPTie ?? 0);
            w.Write(r?.PVPNormalXPTie ?? 0);
            w.Write(r?.PVPTokensTie ?? 0);
            w.Write(r?.PVPAltXPTie ?? 0);

            w.Write(r?.GoldCostLevelScalar ?? 0);

            w.Write(r?.SidekickPrimaryStatIdeal ?? 0);
            w.Write(r?.SidekickVitalityIdeal ?? 0);
            w.Write(r?.SidekickTotalArmorIdeal ?? 0);
            w.Write(r?.SidekickTotalResistIdeal ?? 0);
            w.Write(r?.SidekickTargetLifeOnHitIdeal ?? 0);
            w.Write(r?.SidekickTargetDPSIdeal ?? 0);

            w.Write(r?.GearXPScalar ?? 0);

            const int RecordSize = 468; // <-- FIX: was 224
            long wrote = w.BaseStream.Position - start;
            if (wrote > RecordSize) throw new InvalidDataException($"ExperienceTable record overflow ({wrote} > {RecordSize}).");
            if (wrote < RecordSize) w.Write(new byte[RecordSize - wrote]);
        }
    }


    public sealed class ExperienceTableJsonFile
    {
        public Header Header { get; set; } = new Header();
        public List<ExperienceTableRecord> Records { get; set; } = new();
    }

    public sealed class ExperienceTableRecord
    {
        public int Level { get; set; }
        public int Experience { get; set; }
        public int ParagonExperience { get; set; }
        public int LegendaryGemLevelReq { get; set; }

        public float GoldDropScalar { get; set; }
        public float MagicFindScalar { get; set; }
        public float LegendaryFindScalar { get; set; }

        public int EnchantCost { get; set; }
        public int GambleCost { get; set; }
        public float GemUpgradeChanceBonus { get; set; }

        public float CraftingMatScalar { get; set; }
        public float ReforgeCostScalar { get; set; }
        public float ImbueCostScalar { get; set; }

        public float BountyXPMult { get; set; }
        public float RiftXPMult { get; set; }
        public float GriftXPMult { get; set; }

        public float HellfireXPMult { get; set; }
        public float ShrineXPMult { get; set; }

        public float MonsterHPScalar { get; set; }
        public float MonsterDmgScalar { get; set; }

        public int PVPXPWin { get; set; }
        public int PVPNormalXPWin { get; set; }
        public int PVPTokensWin { get; set; }
        public int PVPAltXPWin { get; set; }

        public int PVPXPLoss { get; set; }
        public int PVPNormalXPLoss { get; set; }
        public int PVPTokensLoss { get; set; }
        public int PVPAltXPLoss { get; set; }

        public int PVPXPTie { get; set; }
        public int PVPNormalXPTie { get; set; }
        public int PVPTokensTie { get; set; }
        public int PVPAltXPTie { get; set; }

        public float GoldCostLevelScalar { get; set; }

        public int SidekickPrimaryStatIdeal { get; set; }
        public int SidekickVitalityIdeal { get; set; }
        public int SidekickTotalArmorIdeal { get; set; }
        public int SidekickTotalResistIdeal { get; set; }
        public int SidekickTargetLifeOnHitIdeal { get; set; }
        public int SidekickTargetDPSIdeal { get; set; }

        public float GearXPScalar { get; set; }
    }
}
