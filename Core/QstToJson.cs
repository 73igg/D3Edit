using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using D3Edit.Filetypes.Qst;

namespace D3Edit.Core
{
    public static class QstToJson
    {
        public static int Convert(string inPath, string outPath)
        {
            if (!File.Exists(inPath))
                throw new FileNotFoundException("Input file not found.", inPath);

            byte[] file = File.ReadAllBytes(inPath);
            using var ms = new MemoryStream(file, writable: false);
            using var br = new BinaryReader(ms, Encoding.ASCII, leaveOpen: true);

            var q = new QstJsonFile();

            q.Header = Header.Read(br);

            q.QuestType = (QuestType)br.ReadInt32();
            q.NumberOfSteps = br.ReadInt32();
            q.NumberOfCompletionSteps = br.ReadInt32();
            q.I2 = br.ReadInt32();
            q.I3 = br.ReadInt32();
            q.I4 = br.ReadInt32();
            q.I5 = br.ReadInt32();

            q.UnassignedStep.ID = br.ReadInt32();
            q.UnassignedStep.I0 = br.ReadInt32();

            br.BaseStream.Position += 8;
            int unObjPtr = br.ReadInt32();
            int unObjSize = br.ReadInt32();

            br.BaseStream.Position += 8;
            int unFailPtr = br.ReadInt32();
            int unFailSize = br.ReadInt32();

            br.BaseStream.Position += 8;
            int stepsPtr = br.ReadInt32();
            int stepsSize = br.ReadInt32();

            br.BaseStream.Position += 8;
            int compPtr = br.ReadInt32();
            int compSize = br.ReadInt32();

            for (int i = 0; i < 18; i++) q.SNOs[i] = br.ReadInt32();
            q.WorldSNO = br.ReadInt32();
            q.Mode = (QuestMode)br.ReadInt32();

            q.Bounty.ActData = br.ReadInt32();
            q.Bounty.Type = br.ReadInt32();
            q.Bounty.I0 = br.ReadInt32();
            br.BaseStream.Position += 19 * 4;
            q.Bounty.F0 = br.ReadSingle();

            foreach (var s in ExtractPrintableStrings(file))
                q.Strings.Add(new D3Edit.Filetypes.Qst.StringAtOffset { Offset = s.offset, Value = s.value });

            var root = JObject.FromObject(q);

            root["StepsPtr"] = stepsPtr;
            root["StepsSize"] = stepsSize;
            root["CompPtr"] = compPtr;
            root["CompSize"] = compSize;

            if (stepsPtr > 0 && stepsSize > 0 && stepsPtr + stepsSize <= file.Length)
                root["QuestStepsRaw"] = System.Convert.ToBase64String(file, stepsPtr, stepsSize);
            if (compPtr > 0 && compSize > 0 && compPtr + compSize <= file.Length)
                root["QuestCompletionStepsRaw"] = System.Convert.ToBase64String(file, compPtr, compSize);

            int arraysEnd = Math.Max(stepsPtr + Math.Max(0, stepsSize), compPtr + Math.Max(0, compSize));
            if (arraysEnd > 0 && arraysEnd < file.Length)
            {
                root["TailPtr"] = arraysEnd;
                root["TailRaw"] = System.Convert.ToBase64String(file, arraysEnd, file.Length - arraysEnd);
            }

            File.WriteAllText(outPath, root.ToString(Formatting.Indented), new UTF8Encoding(false));
            Console.WriteLine($"OK: QST -> steps={q.NumberOfSteps}, comp={q.NumberOfCompletionSteps}");
            return 0;
        }

        private static IEnumerable<(int offset, string value)> ExtractPrintableStrings(byte[] file)
        {
            int i = 0, n = file.Length;
            while (i < n)
            {
                if (file[i] >= 0x20 && file[i] <= 0x7E)
                {
                    int j = i + 1;
                    while (j < n && file[j] >= 0x20 && file[j] <= 0x7E) j++;
                    int len = j - i;
                    if (len >= 4)
                    {
                        string s = Encoding.ASCII.GetString(file, i, len);
                        yield return (i, s);
                    }
                    i = j + 1;
                }
                else i++;
            }
        }
    }
}
