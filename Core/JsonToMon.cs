using System;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;

namespace D3Edit.Core
{
    public static class JsonToMon
    {
        public static int Convert(string inPath, string outPath)
        {
            var text = File.ReadAllText(inPath, new UTF8Encoding(false));
            var root = JObject.Parse(text);

            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true);

            var hdr = root["Header"] as JObject ?? new JObject();
            WriteI32(bw, GetI32(hdr, "DeadBeef", unchecked((int)0xDEADBEEF)));
            WriteI32(bw, GetI32(hdr, "SnoType", 1488));       // Monster
            WriteI32(bw, GetI32(hdr, "Unknown1", 0));
            WriteI32(bw, GetI32(hdr, "Unknown2", 0));
            WriteI32(bw, GetI32(hdr, "SNOId", 0));
            WriteI32(bw, GetI32(hdr, "Unknown3", 0));
            WriteI32(bw, GetI32(hdr, "Unknown4", 0));
            WriteI32(bw, GetI32(hdr, "BalanceType", 0));
            WriteI32(bw, GetI32(hdr, "I0", 0));
            WriteI32(bw, GetI32(hdr, "I1", 0));

            WriteI32(bw, GetI32(root, "Flags", 0));
            WriteI32(bw, GetI32(root, "ActorSNO", -1));
            WriteI32(bw, GetI32(root, "LookIndex", 0));

            WriteI32(bw, GetI32(root, "Type", -1));
            WriteI32(bw, GetI32(root, "Race", -1));
            WriteI32(bw, GetI32(root, "Size", -1));

            var md = root["MonsterDef"] as JObject ?? new JObject();
            WriteF32(bw, GetF32(md, "IdleRadius", 0));
            WriteF32(bw, GetF32(md, "CombatRadius", 0));
            WriteF32(bw, GetF32(md, "TargetAbandonTime", 0));
            WriteF32(bw, GetF32(md, "WarnOthersRadius", 0));
            WriteI32(bw, GetI32(md, "RequireLOSforAllTargets", 0));

            WriteI32(bw, GetI32(root, "Resists", 0));
            WriteI32(bw, GetI32(root, "DefaultCountMin", 0));
            WriteI32(bw, GetI32(root, "DefaultCountDelta", 0));

            var attr = root["AttributeModifiers"] as JArray ?? new JArray();
            for (int i = 0; i < 146; i++) WriteF32(bw, GetF32At(attr, i, 0f));

            WriteF32(bw, GetF32(root, "HPChampion", 0));
            WriteF32(bw, GetF32(root, "HPDeltaChampion", 0));
            WriteF32(bw, GetF32(root, "HPRare", 0));
            WriteF32(bw, GetF32(root, "HPDeltaRare", 0));
            WriteF32(bw, GetF32(root, "HPMinion", 0));
            WriteF32(bw, GetF32(root, "HPDeltaMinion", 0));

            WriteI32(bw, GetI32(root, "GoldGranted", 0));

            WriteHealth(bw, root["HealthDropNormal"] as JObject);
            WriteHealth(bw, root["HealthDropChampion"] as JObject);
            WriteHealth(bw, root["HealthDropRare"] as JObject);
            WriteHealth(bw, root["HealthDropMinion"] as JObject);

            WriteI32(bw, GetI32(root, "SNOSkillKit", -1));

            var sDecls = root["SkillDeclarations"] as JArray ?? new JArray();
            for (int i = 0; i < 8; i++)
            {
                var el = sDecls.ElementAtOrDefault(i) as JObject ?? new JObject();
                WriteI32(bw, GetI32(el, "SNOPower", -1));
                WriteI32(bw, GetI32(el, "LevelMod", 0));
            }

            var msDecls = root["MonsterSkillDeclarations"] as JArray ?? new JArray();
            for (int i = 0; i < 8; i++)
            {
                var el = msDecls.ElementAtOrDefault(i) as JObject ?? new JObject();
                WriteF32(bw, GetF32(el, "UseRangeMin", 0));
                WriteF32(bw, GetF32(el, "UseRangeMax", 0));
                WriteI32(bw, GetI32(el, "Weight", 0));
                WriteF32(bw, GetF32(el, "Timer", 0));
            }

            WriteI32(bw, GetI32(root, "SNOTreasureClassFirstKill", -1));
            WriteI32(bw, GetI32(root, "SNOTreasureClass", -1));
            WriteI32(bw, GetI32(root, "SNOTreasureClassRare", -1));
            WriteI32(bw, GetI32(root, "SNOTreasureClassChampion", -1));
            WriteI32(bw, GetI32(root, "SNOTreasureClassChampionLight", -1));

            WriteF32(bw, GetF32(root, "NoDropScalar", 1));
            WriteF32(bw, GetF32(root, "FleeChance", 0));
            WriteF32(bw, GetF32(root, "FleeCooldownMin", 0));
            WriteF32(bw, GetF32(root, "FleeCooldownDelta", 0));

            WriteI32(bw, GetI32(root, "SummonCountPer", 1));
            WriteF32(bw, GetF32(root, "SummonLifetime", 0));
            WriteI32(bw, GetI32(root, "SummonMaxConcurrent", 0));
            WriteI32(bw, GetI32(root, "SummonMaxTotal", 0));

            WriteI32(bw, GetI32(root, "SNOInventory", -1));
            WriteI32(bw, GetI32(root, "SNOSecondaryInventory", -1));
            WriteI32(bw, GetI32(root, "SNOLore", -1));

            WriteI32Array(bw, root["AIBehavior"] as JArray, 6, -1);
            WriteI32Array(bw, root["GBIdMovementStyles"] as JArray, 8, -1);
            WriteI32Array(bw, root["SNOSummonActor"] as JArray, 6, -1);

            WriteI32(bw, GetI32(root, "RandomAffixes", 0));
            WriteI32Array(bw, root["GBIdAffixes"] as JArray, 4, -1);
            WriteI32Array(bw, root["GBIdDisallowedAffixes"] as JArray, 6, -1);

            WriteI32(bw, GetI32(root, "AITargetStyleNormal", 0));
            WriteI32(bw, GetI32(root, "AITargetStyleChampion", 0));
            WriteI32(bw, GetI32(root, "AITargetStyleRare", 0));
            WriteI32(bw, GetI32(root, "PowerType", -1));

            WriteI32(bw, 0); WriteI32(bw, 0); WriteI32(bw, 0);

            long tagMapHeaderPos = bw.BaseStream.Position;
            for (int i = 0; i < 8; i++) WriteI32(bw, 0);



            EnsurePosition(bw, 1196); // MinionSpawnGroupCount @ 0x4B8
            var minionGroups = root["MonsterMinionSpawnGroups"] as JArray ?? new JArray();
            int minionCount = GetI32(root, "MinionSpawnGroupCount", minionGroups.Count);
            WriteI32(bw, minionCount);

            WriteI32(bw, 0); WriteI32(bw, 0); WriteI32(bw, 0);

            long minionHeaderPos = bw.BaseStream.Position; // should be 1212
            WriteI32(bw, 0); // ptr placeholder
            WriteI32(bw, 0); // size placeholder

            int champCount = GetI32(root, "ChampionSpawnGroupCount", (root["MonsterChampionSpawnGroups"] as JArray)?.Count ?? 0);
            WriteI32(bw, champCount);

            WriteI32(bw, 0); WriteI32(bw, 0); WriteI32(bw, 0); WriteI32(bw, 0);

            long champHeaderPos = bw.BaseStream.Position; // should be 1236
            WriteI32(bw, 0); // ptr placeholder
            WriteI32(bw, 0); // size placeholder

            EnsurePosition(bw, 1244);
            WriteAsciiZ(bw, (string?)root["Name"] ?? string.Empty, 128);

            WriteI32(bw, GetI32(root, "DoesNotDropNecroCorpse", 0));
            WriteI32(bw, GetI32(root, "PadTail", 0));
            WriteI32(bw, GetI32(root, "snoAIStateAttackerCapReached", 0));


            int tagPtrWritten = 0, tagSizeWritten = 0;
            {
                var tag = root["TagMap"] as JObject;
                if (TryBuildTagMapBytes(tag, out var tagBytes) && tagBytes.Length > 0)
                {
                    bool requirePreserve = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MON_PRESERVE_OFFSETS"));
                    int requestedPtr = (tag?["Offset"] as JValue)?.ToObject<int?>() ?? 0;
                    int requestedSize = (tag?["Size"] as JValue)?.ToObject<int?>() ?? tagBytes.Length;

                    if (requestedSize != tagBytes.Length) requestedSize = tagBytes.Length;

                    if (requestedPtr > 0)
                    {
                        if (requestedPtr < bw.BaseStream.Position)
                        {
                            if (requirePreserve)
                                throw new InvalidOperationException($"TagMap.Offset {requestedPtr} is behind current write position {bw.BaseStream.Position}. Enable relocation or remove MON_PRESERVE_OFFSETS.");
                            requestedPtr = 0;
                        }
                    }

                    int actualPtr;
                    if (requestedPtr > 0)
                    {
                        EnsurePosition(bw, requestedPtr);
                        actualPtr = requestedPtr;
                    }
                    else
                    {
                        actualPtr = (int)bw.BaseStream.Position;
                    }

                    bw.Write(tagBytes);
                    tagPtrWritten = actualPtr;
                    tagSizeWritten = requestedSize;

                    long save = bw.BaseStream.Position;
                    bw.BaseStream.Position = tagMapHeaderPos + (3 * 4); WriteI32(bw, tagPtrWritten);
                    bw.BaseStream.Position = tagMapHeaderPos + (4 * 4); WriteI32(bw, tagSizeWritten);
                    bw.BaseStream.Position = save;
                }
                else
                {
                }
            }

            int minionPtr = 0, minionSize = 0;
            if (minionCount > 0 && minionGroups.Count > 0)
            {
                minionPtr = (int)bw.BaseStream.Position;
                minionSize = WriteGroupsBlock(bw, minionGroups, isChampion: false);
            }
            BackpatchPtrSize(bw, minionHeaderPos, minionPtr, minionSize);

            int champPtr = 0, champSize = 0;
            var champGroups = root["MonsterChampionSpawnGroups"] as JArray ?? new JArray();
            if (champCount > 0 && champGroups.Count > 0)
            {
                champPtr = (int)bw.BaseStream.Position;
                champSize = WriteGroupsBlock(bw, champGroups, isChampion: true);
            }
            BackpatchPtrSize(bw, champHeaderPos, champPtr, champSize);

            using (var fs = File.Create(outPath))
            {
                ms.Position = 0;
                ms.CopyTo(fs);
            }
            Console.WriteLine("OK: JSON -> Monster (.mon).");
            return 0;
        }


        private static int WriteGroupsBlock(BinaryWriter bw, JArray groups, bool isChampion)
        {
            long start = bw.BaseStream.Position;
            int n = groups.Count;

            long[] recPos = new long[n];
            for (int i = 0; i < n; i++)
            {
                recPos[i] = bw.BaseStream.Position;
                WriteI32(bw, 0); WriteI32(bw, 0);           // 0x00, 0x04
                WriteI32(bw, 0); WriteI32(bw, 0);           // 0x08 (ptr), 0x0C (size)
                WriteI32(bw, 0);                             // 0x10
                WriteF32(bw, 0f);                            // 0x14
            }

            for (int i = 0; i < n; i++)
            {
                var g = groups[i] as JObject ?? new JObject();

                float weight = (float)GetF32(g, "Weight", 0f);
                int spawnCount = GetI32(g, "SpawnItemCount", 0);

                var items = g["Items"] as JArray ?? new JArray();
                string itemStruct = (string?)g["ItemStruct"] ?? GuessItemStruct(items, isChampion);

                byte[] payload = Array.Empty<byte>();
                if (items.Count > 0)
                {
                    if (itemStruct == "Minion20")
                        payload = BuildMinion20(items);
                    else if (itemStruct == "ActorCount8")
                        payload = BuildActorCount8(items);
                    else
                        payload = Array.Empty<byte>();
                }

                int itemsPtr = 0, itemsSizeHeader = 0;
                if (payload.Length > 0)
                {
                    int headerPos = (int)bw.BaseStream.Position;
                    WriteI32(bw, headerPos);
                    WriteI32(bw, payload.Length);
                    bw.Write(payload);
                    itemsPtr = headerPos;
                    itemsSizeHeader = 8; // group points to the header (reader follows it)
                }

                long here = bw.BaseStream.Position;
                bw.BaseStream.Position = recPos[i] + 0x08; WriteI32(bw, itemsPtr);
                bw.BaseStream.Position = recPos[i] + 0x0C; WriteI32(bw, itemsSizeHeader);
                bw.BaseStream.Position = recPos[i] + 0x14; WriteF32(bw, weight);
                bw.BaseStream.Position = recPos[i] + 0x04; WriteI32(bw, spawnCount);
                bw.BaseStream.Position = here;
            }

            return (int)(bw.BaseStream.Position - start);
        }

        private static string GuessItemStruct(JArray items, bool isChampion)
        {
            if (isChampion) return "ActorCount8"; // champions use (SNOActor, SpawnCount)
            if (items.Count == 0) return "Minion20"; // default choice when empty (harmless)
            var first = items[0] as JObject ?? new JObject();
            if (first["SNOSpawn"] != null) return "Minion20";
            if (first["SNOActor"] != null) return "ActorCount8";
            return "Minion20";
        }

        private static byte[] BuildMinion20(JArray items)
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true);
            foreach (var itok in items)
            {
                var o = itok as JObject ?? new JObject();
                WriteI32(bw, GetI32(o, "SNOSpawn", -1));
                WriteI32(bw, GetI32(o, "SpawnCountMin", 0));
                WriteI32(bw, GetI32(o, "SpawnCountMax", 0));
                WriteI32(bw, GetI32(o, "SpawnSpreadMin", 0));
                WriteI32(bw, GetI32(o, "SpawnSpreadMax", 0));
            }
            return ms.ToArray();
        }

        private static byte[] BuildActorCount8(JArray items)
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true);
            foreach (var itok in items)
            {
                var o = itok as JObject ?? new JObject();
                WriteI32(bw, GetI32(o, "SNOActor", -1));
                WriteI32(bw, GetI32(o, "SpawnCount", 0));
            }
            return ms.ToArray();
        }


        private static void BackpatchPtrSize(BinaryWriter bw, long headerPos, int ptr, int size)
        {
            long save = bw.BaseStream.Position;
            bw.BaseStream.Position = headerPos;
            WriteI32(bw, ptr);
            WriteI32(bw, size);
            bw.BaseStream.Position = save;
        }

        private static void EnsurePosition(BinaryWriter bw, int absolute)
        {
            while (bw.BaseStream.Position < absolute) bw.Write((byte)0);
        }

        private static void WriteHealth(BinaryWriter bw, JObject? h)
        {
            h ??= new JObject();
            WriteF32(bw, GetF32(h, "DropChance", 0));
            WriteI32(bw, GetI32(h, "GBID", -1));
            WriteI32(bw, GetI32(h, "HealthDropStyle", 0));
        }

        private static void WriteAsciiZ(BinaryWriter bw, string s, int totalBytes)
        {
            var bytes = Encoding.ASCII.GetBytes(s ?? "");
            int n = Math.Min(bytes.Length, totalBytes - 1);
            bw.Write(bytes, 0, n);
            for (int i = n; i < totalBytes; i++) bw.Write((byte)0);
        }

        private static void WriteI32Array(BinaryWriter bw, JArray? arr, int count, int def)
        {
            arr ??= new JArray();
            for (int i = 0; i < count; i++)
                WriteI32(bw, GetI32At(arr, i, def));
        }

        private static int GetI32At(JArray arr, int idx, int def) =>
            (arr.ElementAtOrDefault(idx) as JValue)?.ToObject<int?>() ?? def;

        private static float GetF32At(JArray arr, int idx, float def) =>
            (arr.ElementAtOrDefault(idx) as JValue)?.ToObject<float?>() ?? def;

        private static int GetI32(JObject obj, string name, int def) =>
            (obj[name] as JValue)?.ToObject<int?>() ?? def;

        private static float GetF32(JObject obj, string name, float def) =>
            (obj[name] as JValue)?.ToObject<float?>() ?? def;

        private static void WriteI32(BinaryWriter bw, int v) => bw.Write(v);
        private static void WriteF32(BinaryWriter bw, float v) => bw.Write(v);

        private static bool TryBuildTagMapBytes(JObject? tag, out byte[] data)
        {
            data = Array.Empty<byte>();
            if (tag == null) return false;

            if (tag["BytesHex"] is JArray bh && bh.Count > 0)
            {
                var buf = new byte[bh.Count];
                for (int i = 0; i < bh.Count; i++)
                {
                    var hex = (string?)bh[i] ?? "00";
                    buf[i] = System.Convert.ToByte(hex, 16);
                }
                data = buf;
                return true;
            }

            if (tag["U32Hex"] != null)
            {
                if (tag["U32Hex"] is JArray ua && ua.Count > 0)
                {
                    using var ms = new MemoryStream();
                    using var bw = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true);
                    foreach (var t in ua)
                    {
                        uint u = 0;
                        if (t is JValue v)
                        {
                            if (v.Type == JTokenType.Integer) u = System.Convert.ToUInt32((long)v);
                            else if (v.Type == JTokenType.String)
                            {
                                var s = (string?)v ?? "0";
                                u = s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                                    ? System.Convert.ToUInt32(s.Substring(2), 16)
                                    : System.Convert.ToUInt32(s);
                            }
                        }
                        bw.Write(u);
                    }
                    data = ms.ToArray();
                    return true;
                }

                if (tag["U32Hex"] is JValue raw && raw.Type == JTokenType.String)
                {
                    var s = ((string?)raw) ?? "";
                    var toks = System.Text.RegularExpressions.Regex
                                .Matches(s, @"0x([0-9A-Fa-f]{1,8})")
                                .Cast<System.Text.RegularExpressions.Match>()
                                .Select(m => System.Convert.ToUInt32(m.Groups[1].Value, 16));

                    using var ms = new MemoryStream();
                    using var bw = new BinaryWriter(ms);
                    foreach (var u in toks) bw.Write(u);
                    data = ms.ToArray();
                    return data.Length > 0;
                }
            }

            return false;
        }
    }
}
