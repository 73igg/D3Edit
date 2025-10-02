
using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace D3Edit.Core
{
    public static class MonToJson
    {
        private const int LEGACY_MINION_BLOCK_BASE = 0x4B8; // MinionSpawnGroupCount @ 0x4B8
        private const int LEGACY_NAME_ABS_OFFSET = 0x4D0; // 128-byte ASCII-Z

        public static int Convert(string inPath, string outPath)
        {
            if (!File.Exists(inPath))
                throw new FileNotFoundException("Input file not found.", inPath);

            var bytes = File.ReadAllBytes(inPath);
            using var ms = new MemoryStream(bytes, writable: false);
            using var br = new BinaryReader(ms, Encoding.UTF8, leaveOpen: true);

            var header = Header.Read(br);

            int flags = br.ReadInt32();
            int actorSno = br.ReadInt32();
            int lookIndex = br.ReadInt32();

            int type = br.ReadInt32();     // MonsterType
            int race = br.ReadInt32();     // MonsterRace
            int size = br.ReadInt32();     // MonsterSize

            var mdef = new JObject
            {
                ["IdleRadius"] = br.ReadSingle(),
                ["CombatRadius"] = br.ReadSingle(),
                ["TargetAbandonTime"] = br.ReadSingle(),
                ["WarnOthersRadius"] = br.ReadSingle(),
                ["RequireLOSforAllTargets"] = br.ReadInt32()
            };

            int resists = br.ReadInt32();
            int defaultCountMin = br.ReadInt32();
            int defaultCountDelta = br.ReadInt32();

            var attrMods = new JArray();
            for (int i = 0; i < 146; i++) attrMods.Add(br.ReadSingle());

            float hpChampion = br.ReadSingle();
            float hpDeltaChampion = br.ReadSingle();
            float hpRare = br.ReadSingle();
            float hpDeltaRare = br.ReadSingle();
            float hpMinion = br.ReadSingle();
            float hpDeltaMinion = br.ReadSingle();

            int goldGranted = br.ReadInt32();

            var healthNormal = ReadHealthDrop(br);
            var healthChampion = ReadHealthDrop(br);
            var healthRare = ReadHealthDrop(br);
            var healthMinion = ReadHealthDrop(br);

            int snoSkillKit = br.ReadInt32();

            var skillDecls = new JArray();
            for (int i = 0; i < 8; i++)
            {
                var sd = new JObject
                {
                    ["SNOPower"] = br.ReadInt32(),
                    ["LevelMod"] = br.ReadInt32()
                };
                skillDecls.Add(sd);
            }

            var monSkillDecls = new JArray();
            for (int i = 0; i < 8; i++)
            {
                var md = new JObject
                {
                    ["UseRangeMin"] = br.ReadSingle(),
                    ["UseRangeMax"] = br.ReadSingle(),
                    ["Weight"] = br.ReadInt32(),
                    ["Timer"] = br.ReadSingle()
                };
                monSkillDecls.Add(md);
            }

            int snoTreasureFirstKill = br.ReadInt32();
            int snoTreasure = br.ReadInt32();
            int snoTreasureRare = br.ReadInt32();
            int snoTreasureChampion = br.ReadInt32();
            int snoTreasureChampionLight = br.ReadInt32();

            float noDropScalar = br.ReadSingle();
            float fleeChance = br.ReadSingle();
            float fleeCooldownMin = br.ReadSingle();
            float fleeCooldownDelta = br.ReadSingle();

            int summonCountPer = br.ReadInt32();
            float summonLifetime = br.ReadSingle();
            int summonMaxConcurrent = br.ReadInt32();
            int summonMaxTotal = br.ReadInt32();

            int snoInventory = br.ReadInt32();
            int snoSecondaryInventory = br.ReadInt32();
            int snoLore = br.ReadInt32();

            var aiBehavior = new JArray(); for (int i = 0; i < 6; i++) aiBehavior.Add(br.ReadInt32());
            var gbidMovementStyles = new JArray(); for (int i = 0; i < 8; i++) gbidMovementStyles.Add(br.ReadInt32());
            var snoSummonActor = new JArray(); for (int i = 0; i < 6; i++) snoSummonActor.Add(br.ReadInt32());

            int randomAffixes = br.ReadInt32();
            var gbidAffixes = new JArray(); for (int i = 0; i < 4; i++) gbidAffixes.Add(br.ReadInt32());
            var gbidDisallowedAffixes = new JArray(); for (int i = 0; i < 6; i++) gbidDisallowedAffixes.Add(br.ReadInt32());
            int aiTargetStyleNormal = br.ReadInt32();
            int aiTargetStyleChampion = br.ReadInt32();
            int aiTargetStyleRare = br.ReadInt32();
            int powerType = br.ReadInt32();

            br.BaseStream.Position += 12; // pad
            int tagPad0 = br.ReadInt32();
            int tagPad1 = br.ReadInt32();
            int tagPad2 = br.ReadInt32();
            int tagMapOffset = br.ReadInt32();
            int tagMapSize = br.ReadInt32();
            int tagPad3 = br.ReadInt32();
            int tagPad4 = br.ReadInt32();
            int tagPad5 = br.ReadInt32();

            var tagMap = new JObject
            {
                ["Offset"] = tagMapOffset,
                ["Size"] = tagMapSize
            };
            if (IsInside(bytes.Length, tagMapOffset, tagMapSize))
            {
                if ((tagMapSize & 3) == 0)
                {
                    tagMap["Format"] = "U32Hex";
                    tagMap["U32Hex"] = ToU32HexInlineJRaw(bytes, tagMapOffset, tagMapSize);
                }
                else
                {
                    tagMap["Format"] = "BytesHex";
                    tagMap["BytesHex"] = ToBytesHexInlineJRaw(bytes, tagMapOffset, tagMapSize);
                }
            }
            else tagMap["Format"] = "Missing";

            bool forceLegacy = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MON_PARSER_LEGACY"));
            JObject root;
            if (forceLegacy)
            {
                root = ParseLegacy(bytes, header,
                                   flags, actorSno, lookIndex, type, race, size,
                                   mdef, resists, defaultCountMin, defaultCountDelta, attrMods,
                                   hpChampion, hpDeltaChampion, hpRare, hpDeltaRare, hpMinion, hpDeltaMinion,
                                   goldGranted, healthNormal, healthChampion, healthRare, healthMinion,
                                   snoSkillKit, skillDecls, monSkillDecls,
                                   snoTreasureFirstKill, snoTreasure, snoTreasureRare, snoTreasureChampion, snoTreasureChampionLight,
                                   noDropScalar, fleeChance, fleeCooldownMin, fleeCooldownDelta,
                                   summonCountPer, summonLifetime, summonMaxConcurrent, summonMaxTotal,
                                   snoInventory, snoSecondaryInventory, snoLore,
                                   aiBehavior, gbidMovementStyles, snoSummonActor,
                                   randomAffixes, gbidAffixes, gbidDisallowedAffixes,
                                   aiTargetStyleNormal, aiTargetStyleChampion, aiTargetStyleRare, powerType,
                                   tagMap);
            }
            else
            {
                root = ParseSequentialWithFallback(br, bytes, header,
                                   flags, actorSno, lookIndex, type, race, size,
                                   mdef, resists, defaultCountMin, defaultCountDelta, attrMods,
                                   hpChampion, hpDeltaChampion, hpRare, hpDeltaRare, hpMinion, hpDeltaMinion,
                                   goldGranted, healthNormal, healthChampion, healthRare, healthMinion,
                                   snoSkillKit, skillDecls, monSkillDecls,
                                   snoTreasureFirstKill, snoTreasure, snoTreasureRare, snoTreasureChampion, snoTreasureChampionLight,
                                   noDropScalar, fleeChance, fleeCooldownMin, fleeCooldownDelta,
                                   summonCountPer, summonLifetime, summonMaxConcurrent, summonMaxTotal,
                                   snoInventory, snoSecondaryInventory, snoLore,
                                   aiBehavior, gbidMovementStyles, snoSummonActor,
                                   randomAffixes, gbidAffixes, gbidDisallowedAffixes,
                                   aiTargetStyleNormal, aiTargetStyleChampion, aiTargetStyleRare, powerType,
                                   tagMap);
            }

            File.WriteAllText(outPath, JsonConvert.SerializeObject(root, Formatting.Indented), new UTF8Encoding(false));
            Console.WriteLine(forceLegacy ? "OK: Monster (.mon) -> JSON [LEGACY]."
                                          : "OK: Monster (.mon) -> JSON [SEQUENTIAL].");
            return 0;
        }

        private static JObject ParseSequentialWithFallback(
            BinaryReader br, byte[] bytes, object header,
            int flags, int actorSno, int lookIndex, int type, int race, int size,
            JObject mdef, int resists, int defaultCountMin, int defaultCountDelta, JArray attrMods,
            float hpChampion, float hpDeltaChampion, float hpRare, float hpDeltaRare, float hpMinion, float hpDeltaMinion,
            int goldGranted, JObject healthNormal, JObject healthChampion, JObject healthRare, JObject healthMinion,
            int snoSkillKit, JArray skillDecls, JArray monSkillDecls,
            int snoTreasureFirstKill, int snoTreasure, int snoTreasureRare, int snoTreasureChampion, int snoTreasureChampionLight,
            float noDropScalar, float fleeChance, float fleeCooldownMin, float fleeCooldownDelta,
            int summonCountPer, float summonLifetime, int summonMaxConcurrent, int summonMaxTotal,
            int snoInventory, int snoSecondaryInventory, int snoLore,
            JArray aiBehavior, JArray gbidMovementStyles, JArray snoSummonActor,
            int randomAffixes, JArray gbidAffixes, JArray gbidDisallowedAffixes,
            int aiTargetStyleNormal, int aiTargetStyleChampion, int aiTargetStyleRare, int powerType,
            JObject tagMap)
        {
            long basePos = br.BaseStream.Position;

            bool found = false;
            int minionSpawnGroupCount = 0, championSpawnGroupCount = 0;
            int minionGroupsOffset = 0, minionGroupsSize = 0;
            int nameOffset = 0;
            string name = "";
            int doesNotDropNecroCorpse = 0, padTail = 0, snoAIStateAttackerCapReached = 0;

            int bestScore = int.MinValue;
            (int shift, int mCount, int cCount, int gOff, int gSize, int nOff, string nVal, int dnd, int pad, int cap) best = default;

            for (int shift = 0; shift <= 24; shift += 4)
            {
                br.BaseStream.Position = basePos + shift;
                int mCount = ReadInt32Safe(br, bytes.Length);
                int _res0 = ReadInt32Safe(br, bytes.Length);
                int _res1 = ReadInt32Safe(br, bytes.Length);
                int cCount = ReadInt32Safe(br, bytes.Length);
                int gOff = ReadInt32Safe(br, bytes.Length);
                int gSize = ReadInt32Safe(br, bytes.Length);

                int nOff = (int)br.BaseStream.Position;
                string nVal = ReadFixedAsciiZ(br, 128);

                int dnd = ReadInt32Safe(br, bytes.Length);
                int pad = ReadInt32Safe(br, bytes.Length);
                int cap = ReadInt32Safe(br, bytes.Length);

                bool nameOk = LooksPrintable(nVal);
                bool groupsOk = (gSize == 0) || (IsInside(bytes.Length, gOff, gSize) && (gSize % 24 == 0));

                int score = 0;
                if (nameOk) score += 50;
                if (groupsOk) score += 30;
                if (nOff == LEGACY_NAME_ABS_OFFSET) score += 20;
                score -= shift; // prefer earlier alignment slightly

                if (score > bestScore)
                {
                    bestScore = score;
                    best = (shift, mCount, cCount, gOff, gSize, nOff, nVal, dnd, pad, cap);
                }

                if (nameOk && groupsOk && (nOff == LEGACY_NAME_ABS_OFFSET))
                {
                    break;
                }
            }

            if (bestScore >= 50)
            {
                found = true;
                minionSpawnGroupCount = best.mCount;
                championSpawnGroupCount = best.cCount;
                minionGroupsOffset = best.gOff;
                minionGroupsSize = best.gSize;
                nameOffset = best.nOff;
                name = best.nVal;
                doesNotDropNecroCorpse = best.dnd;
                padTail = best.pad;
                snoAIStateAttackerCapReached = best.cap;
            }

            if (!found)
            {
                Console.Error.WriteLine("[MonToJson] Sequential parse looked wrong (alignment scan failed), falling back to legacy.");
                return ParseLegacy(bytes, header,
                                   flags, actorSno, lookIndex, type, race, size,
                                   mdef, resists, defaultCountMin, defaultCountDelta, attrMods,
                                   hpChampion, hpDeltaChampion, hpRare, hpDeltaRare, hpMinion, hpDeltaMinion,
                                   goldGranted, healthNormal, healthChampion, healthRare, healthMinion,
                                   snoSkillKit, skillDecls, monSkillDecls,
                                   snoTreasureFirstKill, snoTreasure, snoTreasureRare, snoTreasureChampion, snoTreasureChampionLight,
                                   noDropScalar, fleeChance, fleeCooldownMin, fleeCooldownDelta,
                                   summonCountPer, summonLifetime, summonMaxConcurrent, summonMaxTotal,
                                   snoInventory, snoSecondaryInventory, snoLore,
                                   aiBehavior, gbidMovementStyles, snoSummonActor,
                                   randomAffixes, gbidAffixes, gbidDisallowedAffixes,
                                   aiTargetStyleNormal, aiTargetStyleChampion, aiTargetStyleRare, powerType,
                                   tagMap);
            }

            var minionGroups = ParseMinionGroups(bytes, minionGroupsOffset, minionGroupsSize);

            int championGroupsOffset = 0, championGroupsSize = 0;
            var championGroups = new JArray();
            if (championSpawnGroupCount > 0 && IsInside(bytes.Length, championGroupsOffset, championGroupsSize))
            {
            }

            return AssembleJson(header,
                                flags, actorSno, lookIndex, type, race, size,
                                mdef, resists, defaultCountMin, defaultCountDelta, attrMods,
                                hpChampion, hpDeltaChampion, hpRare, hpDeltaRare, hpMinion, hpDeltaMinion,
                                goldGranted, healthNormal, healthChampion, healthRare, healthMinion,
                                snoSkillKit, skillDecls, monSkillDecls,
                                snoTreasureFirstKill, snoTreasure, snoTreasureRare, snoTreasureChampion, snoTreasureChampionLight,
                                noDropScalar, fleeChance, fleeCooldownMin, fleeCooldownDelta,
                                summonCountPer, summonLifetime, summonMaxConcurrent, summonMaxTotal,
                                snoInventory, snoSecondaryInventory, snoLore,
                                aiBehavior, gbidMovementStyles, snoSummonActor,
                                randomAffixes, gbidAffixes, gbidDisallowedAffixes,
                                aiTargetStyleNormal, aiTargetStyleChampion, aiTargetStyleRare, powerType,
                                tagMap,
                                minionSpawnGroupCount,
                                new JObject { ["Offset"] = minionGroupsOffset, ["Size"] = minionGroupsSize },
                                minionGroups,
                                championSpawnGroupCount,
                                new JObject { ["Offset"] = championGroupsOffset, ["Size"] = championGroupsSize },
                                championGroups,
                                nameOffset, name,
                                doesNotDropNecroCorpse, padTail, snoAIStateAttackerCapReached);
        }

        private static JObject ParseLegacy(
            byte[] bytes, object header,
            int flags, int actorSno, int lookIndex, int type, int race, int size,
            JObject mdef, int resists, int defaultCountMin, int defaultCountDelta, JArray attrMods,
            float hpChampion, float hpDeltaChampion, float hpRare, float hpDeltaRare, float hpMinion, float hpDeltaMinion,
            int goldGranted, JObject healthNormal, JObject healthChampion, JObject healthRare, JObject healthMinion,
            int snoSkillKit, JArray skillDecls, JArray monSkillDecls,
            int snoTreasureFirstKill, int snoTreasure, int snoTreasureRare, int snoTreasureChampion, int snoTreasureChampionLight,
            float noDropScalar, float fleeChance, float fleeCooldownMin, float fleeCooldownDelta,
            int summonCountPer, float summonLifetime, int summonMaxConcurrent, int summonMaxTotal,
            int snoInventory, int snoSecondaryInventory, int snoLore,
            JArray aiBehavior, JArray gbidMovementStyles, JArray snoSummonActor,
            int randomAffixes, JArray gbidAffixes, JArray gbidDisallowedAffixes,
            int aiTargetStyleNormal, int aiTargetStyleChampion, int aiTargetStyleRare, int powerType,
            JObject tagMap)
        {
            int minionSpawnGroupCount = SafeReadI32(bytes, LEGACY_MINION_BLOCK_BASE + 0);
            int _res0 = SafeReadI32(bytes, LEGACY_MINION_BLOCK_BASE + 4);
            int _res1 = SafeReadI32(bytes, LEGACY_MINION_BLOCK_BASE + 8);
            int championSpawnGroupCount = SafeReadI32(bytes, LEGACY_MINION_BLOCK_BASE + 12);
            int minionGroupsOffset = SafeReadI32(bytes, LEGACY_MINION_BLOCK_BASE + 16);
            int minionGroupsSize = SafeReadI32(bytes, LEGACY_MINION_BLOCK_BASE + 20);

            string name = ReadAsciiZ(bytes, LEGACY_NAME_ABS_OFFSET, 128);
            int nameOffset = LEGACY_NAME_ABS_OFFSET;

            int tailBase = LEGACY_NAME_ABS_OFFSET + 128;
            int doesNotDropNecroCorpse = SafeReadI32(bytes, tailBase + 0);
            int padTail = SafeReadI32(bytes, tailBase + 4);
            int snoAIStateAttackerCapReached = SafeReadI32(bytes, tailBase + 8);

            var minionGroups = ParseMinionGroups(bytes, minionGroupsOffset, minionGroupsSize);
            int championGroupsOffset = 0, championGroupsSize = 0;
            var championGroups = new JArray();

            return AssembleJson(header,
                                flags, actorSno, lookIndex, type, race, size,
                                mdef, resists, defaultCountMin, defaultCountDelta, attrMods,
                                hpChampion, hpDeltaChampion, hpRare, hpDeltaRare, hpMinion, hpDeltaMinion,
                                goldGranted, healthNormal, healthChampion, healthRare, healthMinion,
                                snoSkillKit, skillDecls, monSkillDecls,
                                snoTreasureFirstKill, snoTreasure, snoTreasureRare, snoTreasureChampion, snoTreasureChampionLight,
                                noDropScalar, fleeChance, fleeCooldownMin, fleeCooldownDelta,
                                summonCountPer, summonLifetime, summonMaxConcurrent, summonMaxTotal,
                                snoInventory, snoSecondaryInventory, snoLore,
                                aiBehavior, gbidMovementStyles, snoSummonActor,
                                randomAffixes, gbidAffixes, gbidDisallowedAffixes,
                                aiTargetStyleNormal, aiTargetStyleChampion, aiTargetStyleRare, powerType,
                                tagMap,
                                minionSpawnGroupCount,
                                new JObject { ["Offset"] = minionGroupsOffset, ["Size"] = minionGroupsSize },
                                minionGroups,
                                championSpawnGroupCount,
                                new JObject { ["Offset"] = championGroupsOffset, ["Size"] = championGroupsSize },
                                championGroups,
                                nameOffset, name,
                                doesNotDropNecroCorpse, padTail, snoAIStateAttackerCapReached);
        }

        private static JArray ParseMinionGroups(byte[] bytes, int groupsOffset, int groupsSize)
        {
            var groups = new JArray();
            if (!IsInside(bytes.Length, groupsOffset, groupsSize) || groupsSize % 24 != 0)
                return groups;

            int gcount = groupsSize / 24;
            int goff = groupsOffset;

            for (int i = 0; i < gcount; i++, goff += 24)
            {
                float weightA = SafeReadF32(bytes, goff + 0x00);
                int count = SafeReadI32(bytes, goff + 0x04); // stable in both
                int ipA = SafeReadI32(bytes, goff + 0x10);
                int isA = SafeReadI32(bytes, goff + 0x14);

                float weightB = SafeReadF32(bytes, goff + 0x14);
                int ipB = SafeReadI32(bytes, goff + 0x08);
                int isB = SafeReadI32(bytes, goff + 0x0C);

                bool okA = TryResolveSerializedPayload(bytes, ipA, isA,
                                out int payloadPtrA, out int payloadSizeA,
                                out int headerPtrA, out int headerSizeA,
                                out int depthA, out string structTagA);

                bool okB = TryResolveSerializedPayload(bytes, ipB, isB,
                                out int payloadPtrB, out int payloadSizeB,
                                out int headerPtrB, out int headerSizeB,
                                out int depthB, out string structTagB);

                int strideA = (structTagA == "Minion20") ? 20 : (structTagA == "ActorCount8" ? 8 : 0);
                int strideB = (structTagB == "Minion20") ? 20 : (structTagB == "ActorCount8" ? 8 : 0);
                bool countMatchesA = okA && strideA > 0 && (payloadSizeA / strideA == count);
                bool countMatchesB = okB && strideB > 0 && (payloadSizeB / strideB == count);

                float weight;
                int chosenPayloadPtr = 0, chosenPayloadSize = 0;
                int chosenHeaderPtr = 0, chosenHeaderSize = 0, chosenDepth = 0;
                string structTag = "Unknown";

                if (okB && (countMatchesB || !okA || !countMatchesA))
                {
                    weight = weightB;
                    chosenPayloadPtr = payloadPtrB;
                    chosenPayloadSize = payloadSizeB;
                    chosenHeaderPtr = headerPtrB;
                    chosenHeaderSize = headerSizeB;
                    chosenDepth = depthB;
                    structTag = structTagB;
                }
                else if (okA)
                {
                    weight = weightA;
                    chosenPayloadPtr = payloadPtrA;
                    chosenPayloadSize = payloadSizeA;
                    chosenHeaderPtr = headerPtrA;
                    chosenHeaderSize = headerSizeA;
                    chosenDepth = depthA;
                    structTag = structTagA;
                }
                else
                {
                    weight = IsReasonableWeight(weightB) ? weightB : weightA;
                }

                var groupObj = new JObject
                {
                    ["Weight"] = weight,
                    ["SpawnItemCount"] = count,
                    ["ItemsOffset"] = chosenPayloadPtr,
                    ["ItemsSize"] = chosenPayloadSize,
                    ["Items"] = new JArray()
                };

                if (chosenPayloadPtr > 0)
                {
                    groupObj["ItemsHeaderOffset"] = chosenHeaderPtr;
                    groupObj["ItemsHeaderSize"] = chosenHeaderSize;
                    groupObj["ItemsContainerDepth"] = chosenDepth;
                    groupObj["ItemStruct"] = structTag;

                    var items = (JArray)groupObj["Items"];

                    if (structTag == "Minion20" && chosenPayloadSize % 20 == 0)
                    {
                        int icount = chosenPayloadSize / 20;
                        int ioff = chosenPayloadPtr;
                        for (int j = 0; j < icount; j++, ioff += 20)
                        {
                            int snoSpawn = SafeReadI32(bytes, ioff + 0);
                            int spawnCountMin = SafeReadI32(bytes, ioff + 4);
                            int spawnCountMax = SafeReadI32(bytes, ioff + 8);
                            int spawnSpreadMin = SafeReadI32(bytes, ioff + 12);
                            int spawnSpreadMax = SafeReadI32(bytes, ioff + 16);
                            items.Add(new JObject
                            {
                                ["SNOSpawn"] = snoSpawn,
                                ["SpawnCountMin"] = spawnCountMin,
                                ["SpawnCountMax"] = spawnCountMax,
                                ["SpawnSpreadMin"] = spawnSpreadMin,
                                ["SpawnSpreadMax"] = spawnSpreadMax
                            });
                        }
                    }
                    else if (structTag == "ActorCount8" && chosenPayloadSize % 8 == 0)
                    {
                        int icount = chosenPayloadSize / 8;
                        int ioff = chosenPayloadPtr;
                        for (int j = 0; j < icount; j++, ioff += 8)
                        {
                            int snoActor = SafeReadI32(bytes, ioff + 0);
                            int spawnCount = SafeReadI32(bytes, ioff + 4);
                            items.Add(new JObject
                            {
                                ["SNOActor"] = snoActor,
                                ["SpawnCount"] = spawnCount
                            });
                        }
                    }
                }

                groups.Add(groupObj);
            }

            return groups;
        }

        private static bool TryResolveSerializedPayload(
            byte[] data, int ptr, int size,
            out int payloadPtr, out int payloadSize,
            out int headerPtr, out int headerSize,
            out int depth, out string structTag)
        {
            payloadPtr = payloadSize = headerPtr = headerSize = 0;
            depth = 0;
            structTag = "Unknown";

            if (ptr <= 0 || ptr >= data.Length) return false;

            int curr = ptr;
            for (int d = 0; d < 3; d++) // defensive depth limit
            {
                if (curr + 8 > data.Length) break;

                uint p = unchecked((uint)SafeReadI32(data, curr + 0));
                int s = SafeReadI32(data, curr + 4);

                if (s == 0)
                {
                    curr += 8;
                    continue;
                }

                if (p == (uint)curr && s > 0 && (curr + 8 + s) <= data.Length)
                {
                    payloadPtr = curr + 8;
                    payloadSize = s;
                    headerPtr = curr;
                    headerSize = 8;
                    depth = d + 1;
                }
                else if (p < (uint)data.Length && (p + (uint)s) <= (uint)data.Length)
                {
                    payloadPtr = (int)p;
                    payloadSize = s;
                    headerPtr = curr;
                    headerSize = 8;
                    depth = d + 1;
                }
                else
                {
                    break;
                }

                if (payloadSize % 20 == 0) structTag = "Minion20";
                else if (payloadSize % 8 == 0) structTag = "ActorCount8";
                else structTag = "Unknown";

                return true;
            }

            return false;
        }

        private static bool IsReasonableWeight(float w)
        {
            return w > 1e-6f && w < 1e6f && !float.IsNaN(w) && !float.IsInfinity(w);
        }


        private static JObject AssembleJson(
            object header,
            int flags, int actorSno, int lookIndex, int type, int race, int size,
            JObject mdef, int resists, int defaultCountMin, int defaultCountDelta, JArray attrMods,
            float hpChampion, float hpDeltaChampion, float hpRare, float hpDeltaRare, float hpMinion, float hpDeltaMinion,
            int goldGranted, JObject healthNormal, JObject healthChampion, JObject healthRare, JObject healthMinion,
            int snoSkillKit, JArray skillDecls, JArray monSkillDecls,
            int snoTreasureFirstKill, int snoTreasure, int snoTreasureRare, int snoTreasureChampion, int snoTreasureChampionLight,
            float noDropScalar, float fleeChance, float fleeCooldownMin, float fleeCooldownDelta,
            int summonCountPer, float summonLifetime, int summonMaxConcurrent, int summonMaxTotal,
            int snoInventory, int snoSecondaryInventory, int snoLore,
            JArray aiBehavior, JArray gbidMovementStyles, JArray snoSummonActor,
            int randomAffixes, JArray gbidAffixes, JArray gbidDisallowedAffixes,
            int aiTargetStyleNormal, int aiTargetStyleChampion, int aiTargetStyleRare, int powerType,
            JObject tagMap,
            int minionSpawnGroupCount, JObject minionHeader, JArray minionGroups,
            int championSpawnGroupCount, JObject championHeader, JArray championGroups,
            int nameOffset, string name,
            int doesNotDropNecroCorpse, int padTail, int snoAIStateAttackerCapReached)
        {
            return new JObject
            {
                ["Header"] = JObject.FromObject(header),

                ["Flags"] = flags,
                ["ActorSNO"] = actorSno,
                ["LookIndex"] = lookIndex,
                ["Race"] = race,
                ["Size"] = size,
                ["Type"] = type,

                ["MonsterDef"] = mdef,
                ["Resists"] = resists,
                ["DefaultCountMin"] = defaultCountMin,
                ["DefaultCountDelta"] = defaultCountDelta,
                ["AttributeModifiers"] = attrMods,

                ["HPChampion"] = hpChampion,
                ["HPDeltaChampion"] = hpDeltaChampion,
                ["HPRare"] = hpRare,
                ["HPDeltaRare"] = hpDeltaRare,
                ["HPMinion"] = hpMinion,
                ["HPDeltaMinion"] = hpDeltaMinion,

                ["GoldGranted"] = goldGranted,
                ["HealthDropNormal"] = healthNormal,
                ["HealthDropChampion"] = healthChampion,
                ["HealthDropRare"] = healthRare,
                ["HealthDropMinion"] = healthMinion,

                ["SNOSkillKit"] = snoSkillKit,
                ["SkillDeclarations"] = skillDecls,
                ["MonsterSkillDeclarations"] = monSkillDecls,

                ["SNOTreasureClassFirstKill"] = snoTreasureFirstKill,
                ["SNOTreasureClass"] = snoTreasure,
                ["SNOTreasureClassRare"] = snoTreasureRare,
                ["SNOTreasureClassChampion"] = snoTreasureChampion,
                ["SNOTreasureClassChampionLight"] = snoTreasureChampionLight,

                ["NoDropScalar"] = noDropScalar,
                ["FleeChance"] = fleeChance,
                ["FleeCooldownMin"] = fleeCooldownMin,
                ["FleeCooldownDelta"] = fleeCooldownDelta,

                ["SummonCountPer"] = summonCountPer,
                ["SummonLifetime"] = summonLifetime,
                ["SummonMaxConcurrent"] = summonMaxConcurrent,
                ["SummonMaxTotal"] = summonMaxTotal,

                ["SNOInventory"] = snoInventory,
                ["SNOSecondaryInventory"] = snoSecondaryInventory,
                ["SNOLore"] = snoLore,

                ["AIBehavior"] = aiBehavior,
                ["GBIdMovementStyles"] = gbidMovementStyles,
                ["SNOSummonActor"] = snoSummonActor,

                ["RandomAffixes"] = randomAffixes,
                ["GBIdAffixes"] = gbidAffixes,
                ["GBIdDisallowedAffixes"] = gbidDisallowedAffixes,

                ["AITargetStyleNormal"] = aiTargetStyleNormal,
                ["AITargetStyleChampion"] = aiTargetStyleChampion,
                ["AITargetStyleRare"] = aiTargetStyleRare,
                ["PowerType"] = powerType,

                ["TagMap"] = tagMap,

                ["MinionSpawnGroupCount"] = minionSpawnGroupCount,
                ["MinionSpawnGroupsHeader"] = minionHeader,
                ["MonsterMinionSpawnGroups"] = minionGroups,

                ["ChampionSpawnGroupCount"] = championSpawnGroupCount,
                ["ChampionSpawnGroupsHeader"] = championHeader,
                ["MonsterChampionSpawnGroups"] = championGroups,

                ["NameOffset"] = nameOffset,
                ["Name"] = name,

                ["DoesNotDropNecroCorpse"] = doesNotDropNecroCorpse,
                ["PadTail"] = padTail,
                ["snoAIStateAttackerCapReached"] = snoAIStateAttackerCapReached
            };
        }


        private static JObject ReadHealthDrop(BinaryReader br) =>
            new JObject
            {
                ["DropChance"] = br.ReadSingle(),
                ["GBID"] = br.ReadInt32(),
                ["HealthDropStyle"] = br.ReadInt32()
            };

        private static string ReadFixedAsciiZ(BinaryReader br, int totalBytes)
        {
            var raw = br.ReadBytes(totalBytes);
            int term = Array.IndexOf(raw, (byte)0);
            if (term < 0) term = raw.Length;
            return Encoding.ASCII.GetString(raw, 0, term);
        }

        private static string ReadAsciiZ(byte[] data, int offset, int length)
        {
            int end = Math.Min(offset + length, data.Length);
            int term = offset;
            while (term < end && data[term] != 0) term++;
            return Encoding.ASCII.GetString(data, offset, term - offset);
        }

        private static int ReadInt32Safe(BinaryReader br, int totalSize)
        {
            if (br.BaseStream.Position + 4 > totalSize) return 0;
            return br.ReadInt32();
        }

        private static bool IsInside(int total, int offset, int size)
        {
            if (offset <= 0 || size <= 0) return false;
            long end = (long)offset + (long)size;
            return offset >= 0 && size > 0 && end <= total;
        }

        private static bool LooksPrintable(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            int printable = 0;
            foreach (char c in s)
            {
                if (c == 0) break;
                if (c >= 32 && c < 127) printable++;
            }
            return printable >= 2; // at least 2 visible chars
        }

        private static JRaw ToU32HexInlineJRaw(byte[] data, int offset, int size)
        {
            int count = size / 4;
            var sb = new StringBuilder(count * 11 + 2);
            sb.Append('[');
            for (int i = 0; i < count; i++)
            {
                uint v = BitConverter.ToUInt32(data, offset + i * 4);
                if (i > 0) sb.Append(", ");
                sb.Append("0x");
                sb.Append(v.ToString("X8"));
            }
            sb.Append(']');
            return new JRaw(sb.ToString());
        }

        private static JRaw ToBytesHexInlineJRaw(byte[] data, int offset, int size)
        {
            var sb = new StringBuilder(size * 5 + 2);
            sb.Append('[');
            for (int i = 0; i < size; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append('"');
                sb.Append(data[offset + i].ToString("X2"));
                sb.Append('"');
            }
            sb.Append(']');
            return new JRaw(sb.ToString());
        }

        private static int SafeReadI32(byte[] data, int offset)
        {
            if ((uint)(offset + 4) > data.Length) return 0;
            return BitConverter.ToInt32(data, offset);
        }

        private static float SafeReadF32(byte[] data, int offset)
        {
            if ((uint)(offset + 4) > data.Length) return 0f;
            return BitConverter.ToSingle(data, offset);
        }
    }
}
