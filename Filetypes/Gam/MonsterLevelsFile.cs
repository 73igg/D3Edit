using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using D3Edit.Core;

namespace D3Edit.Filetypes.Gam
{
    public static class MonsterLevelsIO
    {
        public static MonsterLevelsJsonFile ReadGamFile(string filePath)
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
                if (blockLen <= 0) throw new InvalidDataException("MonsterLevels block pointer invalid.");
            }

            int preamble = DetectPreamble(br, blockOff, 32);
            fs.Position = blockOff + (preamble > 0 ? preamble : 0x10);

            var recs = new List<MonsterLevelRecord>();
            const int RecordSize = 240; // 1 int + 58 floats + 1 int = 240 bytes
            long end = Math.Min(fs.Length, blockOff + blockLen);

            while (fs.Position + RecordSize <= end)
            {
                recs.Add(ReadOne(br));
            }

            var outHeader = header;
            outHeader.BalanceType = balanceType;
            outHeader.I0 = i0;
            outHeader.I1 = i1;

            return new MonsterLevelsJsonFile { Header = outHeader, Records = recs };
        }

        public static void WriteGamFile(string filePath, MonsterLevelsJsonFile data)
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

            foreach (var r in data.Records ?? Enumerable.Empty<MonsterLevelRecord>())
                WriteOne(bw, r);

            long end = fs.Position;

            long save = fs.Position;
            fs.Position = 0x230 + 4;
            bw.Write(checked((int)(end - BLOCK_OFF)));
            fs.Position = save;
        }

        private static MonsterLevelRecord ReadOne(BinaryReader s)
        {
            var r = new MonsterLevelRecord();
            r.LvlMin = s.ReadInt32();
            r.Str = s.ReadSingle();
            r.Dex = s.ReadSingle();
            r.Int = s.ReadSingle();
            r.Vit = s.ReadSingle();
            r.HPMin = s.ReadSingle();
            r.HPDelta = s.ReadSingle();
            r.HPRegen = s.ReadSingle();
            r.ResourceBase = s.ReadSingle();
            r.ResourceRegen = s.ReadSingle();
            r.Armor = s.ReadSingle();
            r.Dmg = s.ReadSingle();
            r.DmgDelta = s.ReadSingle();
            r.DmgFire = s.ReadSingle();
            r.DmgDeltaFire = s.ReadSingle();
            r.DmgLightning = s.ReadSingle();
            r.DmgDeltaLightning = s.ReadSingle();
            r.DmgCold = s.ReadSingle();
            r.DmgDeltaCold = s.ReadSingle();
            r.DmgPoison = s.ReadSingle();
            r.DmgDeltaPoison = s.ReadSingle();
            r.DmgArcane = s.ReadSingle();
            r.DmgDeltaArcane = s.ReadSingle();
            r.DmgHoly = s.ReadSingle();
            r.DmgDeltaHoly = s.ReadSingle();
            r.DmgSiege = s.ReadSingle();
            r.DmgDeltaSiege = s.ReadSingle();
            r.HirelingHPMin = s.ReadSingle();
            r.HirelingHPDelta = s.ReadSingle();
            r.HirelingHPRegen = s.ReadSingle();
            r.HirelingDmg = s.ReadSingle();
            r.HirelingDmgRange = s.ReadSingle();
            r.HirelingRetrainCost = s.ReadSingle();
            r.GetHitDamage = s.ReadSingle();
            r.GetHitScalar = s.ReadSingle();
            r.GetHitMax = s.ReadSingle();
            r.GetHitRecovery = s.ReadSingle();
            r.WalkSpd = s.ReadSingle();
            r.RunSpd = s.ReadSingle();
            r.SprintSpd = s.ReadSingle();
            r.StrafeSpd = s.ReadSingle();
            r.AttSpd = s.ReadSingle();
            r.ProjSpd = s.ReadSingle();
            r.Exp = s.ReadSingle();
            r.ResistPhysical = s.ReadSingle();
            r.ResistFire = s.ReadSingle();
            r.ResistLightning = s.ReadSingle();
            r.ResistCold = s.ReadSingle();
            r.ResistPoison = s.ReadSingle();
            r.ResistArcane = s.ReadSingle();
            r.ResistSiege = s.ReadSingle();
            r.ResistChill = s.ReadSingle();
            r.ResistStun = s.ReadSingle();
            r.ConsoleHealthScalar = s.ReadSingle();
            r.ConsoleDamageScalar = s.ReadSingle();
            r.Monster1AffixWeight = s.ReadSingle();
            r.Monster2AffixWeight = s.ReadSingle();
            r.Monster3AffixWeight = s.ReadSingle();
            r.Monster4AffixWeight = s.ReadSingle();
            r.Pad = s.ReadInt32();
            return r;
        }

        private static void WriteOne(BinaryWriter w, MonsterLevelRecord r)
        {
            w.Write(r?.LvlMin ?? 0);
            w.Write(r?.Str ?? 0);
            w.Write(r?.Dex ?? 0);
            w.Write(r?.Int ?? 0);
            w.Write(r?.Vit ?? 0);
            w.Write(r?.HPMin ?? 0);
            w.Write(r?.HPDelta ?? 0);
            w.Write(r?.HPRegen ?? 0);
            w.Write(r?.ResourceBase ?? 0);
            w.Write(r?.ResourceRegen ?? 0);
            w.Write(r?.Armor ?? 0);
            w.Write(r?.Dmg ?? 0);
            w.Write(r?.DmgDelta ?? 0);
            w.Write(r?.DmgFire ?? 0);
            w.Write(r?.DmgDeltaFire ?? 0);
            w.Write(r?.DmgLightning ?? 0);
            w.Write(r?.DmgDeltaLightning ?? 0);
            w.Write(r?.DmgCold ?? 0);
            w.Write(r?.DmgDeltaCold ?? 0);
            w.Write(r?.DmgPoison ?? 0);
            w.Write(r?.DmgDeltaPoison ?? 0);
            w.Write(r?.DmgArcane ?? 0);
            w.Write(r?.DmgDeltaArcane ?? 0);
            w.Write(r?.DmgHoly ?? 0);
            w.Write(r?.DmgDeltaHoly ?? 0);
            w.Write(r?.DmgSiege ?? 0);
            w.Write(r?.DmgDeltaSiege ?? 0);
            w.Write(r?.HirelingHPMin ?? 0);
            w.Write(r?.HirelingHPDelta ?? 0);
            w.Write(r?.HirelingHPRegen ?? 0);
            w.Write(r?.HirelingDmg ?? 0);
            w.Write(r?.HirelingDmgRange ?? 0);
            w.Write(r?.HirelingRetrainCost ?? 0);
            w.Write(r?.GetHitDamage ?? 0);
            w.Write(r?.GetHitScalar ?? 0);
            w.Write(r?.GetHitMax ?? 0);
            w.Write(r?.GetHitRecovery ?? 0);
            w.Write(r?.WalkSpd ?? 0);
            w.Write(r?.RunSpd ?? 0);
            w.Write(r?.SprintSpd ?? 0);
            w.Write(r?.StrafeSpd ?? 0);
            w.Write(r?.AttSpd ?? 0);
            w.Write(r?.ProjSpd ?? 0);
            w.Write(r?.Exp ?? 0);
            w.Write(r?.ResistPhysical ?? 0);
            w.Write(r?.ResistFire ?? 0);
            w.Write(r?.ResistLightning ?? 0);
            w.Write(r?.ResistCold ?? 0);
            w.Write(r?.ResistPoison ?? 0);
            w.Write(r?.ResistArcane ?? 0);
            w.Write(r?.ResistSiege ?? 0);
            w.Write(r?.ResistChill ?? 0);
            w.Write(r?.ResistStun ?? 0);
            w.Write(r?.ConsoleHealthScalar ?? 0);
            w.Write(r?.ConsoleDamageScalar ?? 0);
            w.Write(r?.Monster1AffixWeight ?? 0);
            w.Write(r?.Monster2AffixWeight ?? 0);
            w.Write(r?.Monster3AffixWeight ?? 0);
            w.Write(r?.Monster4AffixWeight ?? 0);
            w.Write(r?.Pad ?? 0);
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
    }

    public class MonsterLevelsJsonFile
    {
        public Header Header { get; set; } = Header.Default();
        public List<MonsterLevelRecord> Records { get; set; } = new List<MonsterLevelRecord>();
    }

    public class MonsterLevelRecord
    {
        public int LvlMin { get; set; }
        public float Str { get; set; }
        public float Dex { get; set; }
        public float Int { get; set; }
        public float Vit { get; set; }
        public float HPMin { get; set; }
        public float HPDelta { get; set; }
        public float HPRegen { get; set; }
        public float ResourceBase { get; set; }
        public float ResourceRegen { get; set; }
        public float Armor { get; set; }
        public float Dmg { get; set; }
        public float DmgDelta { get; set; }
        public float DmgFire { get; set; }
        public float DmgDeltaFire { get; set; }
        public float DmgLightning { get; set; }
        public float DmgDeltaLightning { get; set; }
        public float DmgCold { get; set; }
        public float DmgDeltaCold { get; set; }
        public float DmgPoison { get; set; }
        public float DmgDeltaPoison { get; set; }
        public float DmgArcane { get; set; }
        public float DmgDeltaArcane { get; set; }
        public float DmgHoly { get; set; }
        public float DmgDeltaHoly { get; set; }
        public float DmgSiege { get; set; }
        public float DmgDeltaSiege { get; set; }
        public float HirelingHPMin { get; set; }
        public float HirelingHPDelta { get; set; }
        public float HirelingHPRegen { get; set; }
        public float HirelingDmg { get; set; }
        public float HirelingDmgRange { get; set; }
        public float HirelingRetrainCost { get; set; }
        public float GetHitDamage { get; set; }
        public float GetHitScalar { get; set; }
        public float GetHitMax { get; set; }
        public float GetHitRecovery { get; set; }
        public float WalkSpd { get; set; }
        public float RunSpd { get; set; }
        public float SprintSpd { get; set; }
        public float StrafeSpd { get; set; }
        public float AttSpd { get; set; }
        public float ProjSpd { get; set; }
        public float Exp { get; set; }
        public float ResistPhysical { get; set; }
        public float ResistFire { get; set; }
        public float ResistLightning { get; set; }
        public float ResistCold { get; set; }
        public float ResistPoison { get; set; }
        public float ResistArcane { get; set; }
        public float ResistSiege { get; set; }
        public float ResistChill { get; set; }
        public float ResistStun { get; set; }
        public float ConsoleHealthScalar { get; set; }
        public float ConsoleDamageScalar { get; set; }
        public float Monster1AffixWeight { get; set; }
        public float Monster2AffixWeight { get; set; }
        public float Monster3AffixWeight { get; set; }
        public float Monster4AffixWeight { get; set; }
        public int Pad { get; set; }
    }
}
