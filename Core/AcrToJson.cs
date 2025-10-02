using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace D3Edit.Core
{
    public static class AcrToJson
    {
        public static int Convert(string inPath, string outPath)
        {
            if (!File.Exists(inPath))
                throw new FileNotFoundException("Input file not found.", inPath);

            var bytes = File.ReadAllBytes(inPath);
            using var ms = new MemoryStream(bytes, writable: false);
            using var br = new BinaryReader(ms, Encoding.UTF8, leaveOpen: true);

            var header = Header.Read(br);

            if (br.BaseStream.Position < 0x20)
                br.BaseStream.Position = 0x20;

            int flags = br.ReadInt32();
            int appearanceSno = br.ReadInt32();
            int physMeshSno = br.ReadInt32();

            var cyl = new JObject
            {
                ["Position"] = new JObject
                {
                    ["X"] = br.ReadSingle(),
                    ["Y"] = br.ReadSingle(),
                    ["Z"] = br.ReadSingle(),
                },
                ["Ax1"] = br.ReadSingle(),
                ["Ax2"] = br.ReadSingle(),
            };

            var sphere = new JObject
            {
                ["Position"] = new JObject
                {
                    ["X"] = br.ReadSingle(),
                    ["Y"] = br.ReadSingle(),
                    ["Z"] = br.ReadSingle(),
                },
                ["Radius"] = br.ReadSingle(),
            };

            var aabb = new JObject
            {
                ["Min"] = new JObject
                {
                    ["X"] = br.ReadSingle(),
                    ["Y"] = br.ReadSingle(),
                    ["Z"] = br.ReadSingle(),
                },
                ["Max"] = new JObject
                {
                    ["X"] = br.ReadSingle(),
                    ["Y"] = br.ReadSingle(),
                    ["Z"] = br.ReadSingle(),
                },
            };

            int tagMapOffset = br.ReadInt32();
            int tagMapSize = br.ReadInt32();

            int tagPad0 = br.ReadInt32();
            int tagPad1 = br.ReadInt32();

            int animSetSno = br.ReadInt32();
            int monsterSno = br.ReadInt32();

            int msgEvtSize = br.ReadInt32();
            int msgEvtOffset = br.ReadInt32();

            int aniimTreeSno = br.ReadInt32();

            int unkAfterMsg0 = br.ReadInt32();
            int unkAfterMsg1 = br.ReadInt32();
            int unkAfterMsg2 = br.ReadInt32();

            var locPower = new JObject
            {
                ["X"] = br.ReadSingle(),
                ["Y"] = br.ReadSingle(),
                ["Z"] = br.ReadSingle(),
            };

            var looks = new JArray();
            for (int i = 0; i < 8; i++)
            {
                var raw = br.ReadBytes(64);
                int term = Array.IndexOf(raw, (byte)0);
                string look = term >= 0 ? Encoding.ASCII.GetString(raw, 0, term) : Encoding.ASCII.GetString(raw);
                int i0 = br.ReadInt32();
                looks.Add(new JObject { ["LookLink"] = look, ["Int0"] = i0 });
            }

            int physicsSno = br.ReadInt32();
            int physicsFlags = br.ReadInt32();
            int material = br.ReadInt32();
            float explosionFactor = br.ReadSingle();
            float windFactor = br.ReadSingle();
            float partialRagdoll = br.ReadSingle();

            var collFlags = new JArray { br.ReadInt32(), br.ReadInt32(), br.ReadInt32(), br.ReadInt32() };
            int collisionShape = br.ReadInt32();

            var collCyl = new JObject
            {
                ["Position"] = new JObject
                {
                    ["X"] = br.ReadSingle(),
                    ["Y"] = br.ReadSingle(),
                    ["Z"] = br.ReadSingle(),
                },
                ["Ax1"] = br.ReadSingle(),
                ["Ax2"] = br.ReadSingle(),
            };

            var collAabb = new JObject
            {
                ["Min"] = new JObject
                {
                    ["X"] = br.ReadSingle(),
                    ["Y"] = br.ReadSingle(),
                    ["Z"] = br.ReadSingle(),
                },
                ["Max"] = new JObject
                {
                    ["X"] = br.ReadSingle(),
                    ["Y"] = br.ReadSingle(),
                    ["Z"] = br.ReadSingle(),
                },
            };

            float movingRadiusScalar = br.ReadSingle();

            var invImages = new JArray();
            for (int i = 0; i < 7; i++) invImages.Add(br.ReadInt32());

            var invPad = new JArray();
            for (int i = 0; i < 7; i++) invPad.Add(br.ReadInt32());

            int socketedImage = br.ReadInt32();

            var socketPad = new JArray();
            for (int i = 0; i < 5; i++) socketPad.Add(br.ReadInt32());

            int castingNotesOffset = br.ReadInt32();
            int castingNotesSize = br.ReadInt32();
            int voiceRoleOffset = br.ReadInt32();
            int voiceRoleSize = br.ReadInt32();

            string castingNotes = ReadSerializedString(bytes, castingNotesOffset, castingNotesSize);
            string voiceOverRole = ReadSerializedString(bytes, voiceRoleOffset, voiceRoleSize);

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
            else
            {
                tagMap["Format"] = "Missing";
            }

            var msg = new JObject
            {
                ["Offset"] = msgEvtOffset,
                ["SizeBytes"] = msgEvtSize
            };
            if (IsInside(bytes.Length, msgEvtOffset, msgEvtSize))
            {
                bool wordAligned = (msgEvtSize & 3) == 0;
                if (wordAligned)
                {
                    msg["Format"] = "U32Hex+Strings";
                    msg["U32Hex"] = ToU32HexInlineJRaw(bytes, msgEvtOffset, msgEvtSize);
                }
                else
                {
                    msg["Format"] = "BytesHex+Strings";
                    msg["BytesHex"] = ToBytesHexInlineJRaw(bytes, msgEvtOffset, msgEvtSize);
                }

                msg["Strings"] = ExtractAsciiStrings(bytes, msgEvtOffset, msgEvtSize, minLen: 3);
            }
            else
            {
                msg["Format"] = "Missing";
            }

            int fileLen = bytes.Length;

            int tagEnd = SafeEnd(tagMapOffset, tagMapSize, fileLen);
            int msgEnd = SafeEnd(msgEvtOffset, msgEvtSize, fileLen);
            int castEnd = SafeEnd(castingNotesOffset, castingNotesSize, fileLen);
            int voiceEnd = SafeEnd(voiceRoleOffset, voiceRoleSize, fileLen);

            int preStart = tagEnd;
            int preEnd = (msgEvtOffset > 0) ? msgEvtOffset : tagEnd;
            int preSize = Math.Max(0, preEnd - preStart);
            var preMsgRaw = BuildRawSection(bytes, preStart, preSize, includeStrings: true);

            int postStart = Math.Max(tagEnd, msgEnd);
            int nextStringStart = FirstPositive(castingNotesOffset, voiceRoleOffset);
            int postEnd = (nextStringStart > 0) ? nextStringStart : fileLen;
            int postSize = Math.Max(0, postEnd - postStart);
            var postMsgRaw = BuildRawSection(bytes, postStart, postSize, includeStrings: true);

            int lastStrEnd = Math.Max(castEnd, voiceEnd);
            int tailStart = Math.Min(Math.Max(0, lastStrEnd), fileLen);
            int tailSize = Math.Max(0, fileLen - tailStart);
            var tailRaw = BuildRawSection(bytes, tailStart, tailSize, includeStrings: true);

            var root = new JObject
            {
                ["Header"] = JObject.FromObject(header),
                ["ActorType"] = header.Unknown4,

                ["Flags"] = flags,
                ["AppearanceSNO"] = appearanceSno,
                ["PhysMeshSNO"] = physMeshSno,
                ["Cylinder"] = cyl,
                ["Sphere"] = sphere,
                ["AABBBounds"] = aabb,

                ["TagMap"] = tagMap,
                ["TagMapPad0"] = tagPad0,
                ["TagMapPad1"] = tagPad1,

                ["AnimSetSNO"] = animSetSno,
                ["MonsterSNO"] = monsterSno,

                ["MsgTriggeredEvents"] = msg,

                ["PreMsgRaw"] = preMsgRaw,
                ["PostMsgRaw"] = postMsgRaw,
                ["TailRaw"] = tailRaw,

                ["AniimTreeSNO"] = aniimTreeSno,
                ["UnknownAfterMsg"] = new JArray { unkAfterMsg0, unkAfterMsg1, unkAfterMsg2 },

                ["LocationPowerSrc"] = locPower,
                ["Looks"] = looks,

                ["PhysicsSNO"] = physicsSno,
                ["PhysicsFlags"] = physicsFlags,
                ["Material"] = material,
                ["ExplosionFactor"] = explosionFactor,
                ["WindFactor"] = windFactor,
                ["PartialRagdollResponsiveness"] = partialRagdoll,

                ["ActorCollisionData"] = new JObject
                {
                    ["CollFlags"] = collFlags,
                    ["CollisiionShape"] = collisionShape,
                    ["Cylinder"] = collCyl,
                    ["AABBCollision"] = collAabb,
                    ["MovingRadiusScalar"] = movingRadiusScalar
                },

                ["InventoryImages"] = invImages,
                ["InventoryPad"] = invPad,
                ["SocketedImage"] = socketedImage,
                ["SocketPad"] = socketPad,

                ["CastingNotesHeader"] = new JObject { ["Offset"] = castingNotesOffset, ["Size"] = castingNotesSize },
                ["VoiceOverRoleHeader"] = new JObject { ["Offset"] = voiceRoleOffset, ["Size"] = voiceRoleSize },
                ["CastingNotes"] = castingNotes,
                ["VoiceOverRole"] = voiceOverRole
            };

            File.WriteAllText(outPath, JsonConvert.SerializeObject(root, Formatting.Indented), new UTF8Encoding(false));
            Console.WriteLine("OK: Actor (.acr) -> JSON (structured, full hex, +Pre/Post/Tail) with overlap-safe PostMsgRaw.");
            return 0;
        }


        private static bool IsInside(int total, int offset, int size)
        {
            if (offset <= 0 || size <= 0) return false;
            long end = (long)offset + (long)size;
            return offset >= 0 && size > 0 && end <= total;
        }

        private static JObject BuildRawSection(byte[] data, int offset, int size, bool includeStrings)
        {
            var obj = new JObject
            {
                ["Offset"] = offset,
                ["SizeBytes"] = size
            };

            if (size <= 0 || offset < 0 || offset + size > data.Length)
            {
                obj["Format"] = "Missing";
                return obj;
            }

            bool aligned = (size & 3) == 0;
            if (aligned)
            {
                obj["Format"] = includeStrings ? "U32Hex+Strings" : "U32Hex";
                obj["U32Hex"] = ToU32HexInlineJRaw(data, offset, size);
            }
            else
            {
                obj["Format"] = includeStrings ? "BytesHex+Strings" : "BytesHex";
                obj["BytesHex"] = ToBytesHexInlineJRaw(data, offset, size);
            }

            if (includeStrings)
                obj["Strings"] = ExtractAsciiStrings(data, offset, size, minLen: 3);

            return obj;
        }

        private static JRaw ToU32HexInlineJRaw(byte[] data, int offset, int size)
        {
            int count = size / 4;
            var sb = new StringBuilder(count * 11 + 2); // ~"0xFFFFFFFF, " per word
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
            var sb = new StringBuilder(size * 5 + 2); // ~"AA, " per byte
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

        private static JArray ExtractAsciiStrings(byte[] data, int offset, int size, int minLen)
        {
            var strings = new List<string>();
            int end = offset + size;
            int i = offset;
            while (i < end)
            {
                int start = i;
                while (i < end && data[i] >= 0x20 && data[i] <= 0x7E) i++;

                if (i > start + (minLen - 1) && i < end && data[i] == 0x00)
                {
                    string s = Encoding.ASCII.GetString(data, start, i - start);
                    strings.Add(s);
                    i++; // skip null
                    continue;
                }
                i = (i == start) ? i + 1 : i + 1;
            }

            var seen = new HashSet<string>();
            var outArr = new JArray();
            foreach (var s in strings)
            {
                if (seen.Add(s)) outArr.Add(s);
            }
            return outArr;
        }

        private static string ReadSerializedString(byte[] bytes, int offset, int size)
        {
            if (size <= 0 || offset <= 0 || offset + size > bytes.Length) return "";
            int end = Array.IndexOf(bytes, (byte)0, offset, size);
            if (end < 0) end = offset + size;
            return Encoding.UTF8.GetString(bytes, offset, end - offset);
        }

        private static int SafeEnd(int offset, int size, int totalLen)
        {
            if (offset <= 0 || size <= 0 || offset + size > totalLen) return Math.Max(0, offset);
            return offset + size;
        }

        private static int FirstPositive(params int[] values)
        {
            int best = int.MaxValue;
            foreach (var v in values) if (v > 0 && v < best) best = v;
            return best == int.MaxValue ? 0 : best;
        }
    }
}
