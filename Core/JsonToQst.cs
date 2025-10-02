using System;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;
using D3Edit.Filetypes.Qst;

namespace D3Edit.Core
{
    public static class JsonToQst
    {
        public static int Convert(string inPath, string outPath)
        {
            var json = File.ReadAllText(inPath, Encoding.UTF8);

            var root = JObject.Parse(json);
            var q = root.ToObject<QstJsonFile>() ?? new QstJsonFile();

            byte[] stepsRaw = GetB64(root, "QuestStepsRaw");
            byte[] compRaw = GetB64(root, "QuestCompletionStepsRaw");
            byte[] tailRaw = GetB64(root, "TailRaw");

            int stepsPtr = (int?)root["StepsPtr"] ?? 0;
            int stepsSizeJson = (int?)root["StepsSize"] ?? 0;
            int compPtr = (int?)root["CompPtr"] ?? 0;
            int compSizeJson = (int?)root["CompSize"] ?? 0;
            int tailPtr = (int?)root["TailPtr"] ?? 0;

            if (stepsRaw.Length > 0 && stepsSizeJson == 0) stepsSizeJson = stepsRaw.Length;
            if (compRaw.Length > 0 && compSizeJson == 0) compSizeJson = compRaw.Length;

            using var fs = new FileStream(outPath, FileMode.Create, FileAccess.Write, FileShare.None);
            using var bw = new BinaryWriter(fs, Encoding.ASCII, leaveOpen: true);

            var h = q.Header ?? Header.Default();
            WriteI32(bw, h.DeadBeef == 0 ? unchecked((int)0xDEADBEEF) : h.DeadBeef);
            WriteI32(bw, h.SnoType == 0 ? 135 : h.SnoType);
            WriteI32(bw, h.Unknown1);
            WriteI32(bw, h.Unknown2);
            WriteI32(bw, h.SNOId);
            WriteI32(bw, h.Unknown3);
            WriteI32(bw, h.Unknown4);

            int numSteps = Math.Max(0, q.NumberOfSteps);
            int numComp = Math.Max(0, q.NumberOfCompletionSteps);
            WriteI32(bw, (int)q.QuestType);
            WriteI32(bw, numSteps);
            WriteI32(bw, numComp);
            WriteI32(bw, q.I2);
            WriteI32(bw, q.I3);
            WriteI32(bw, q.I4);
            WriteI32(bw, q.I5);

            WriteI32(bw, q.UnassignedStep?.ID ?? 0);
            WriteI32(bw, q.UnassignedStep?.I0 ?? 0);

            WritePad8(bw);
            long unObjPos = fs.Position; WriteI32(bw, 0); WriteI32(bw, 0);

            WritePad8(bw);
            long unFailPos = fs.Position; WriteI32(bw, 0); WriteI32(bw, 0);

            WritePad8(bw);
            long stepsContPos = fs.Position; WriteI32(bw, 0); WriteI32(bw, 0);

            WritePad8(bw);
            long compContPos = fs.Position; WriteI32(bw, 0); WriteI32(bw, 0);

            var snos = q.SNOs ?? new int[18];
            if (snos.Length != 18) Array.Resize(ref snos, 18);
            for (int i = 0; i < 18; i++) WriteI32(bw, snos[i]);
            WriteI32(bw, q.WorldSNO);
            WriteI32(bw, (int)q.Mode);

            var b = q.Bounty ?? new BountyDataJson();
            WriteI32(bw, b.ActData);
            WriteI32(bw, b.Type);
            WriteI32(bw, b.I0);
            for (int i = 0; i < 19; i++) WriteI32(bw, 0);
            bw.Write(b.F0);


            if (stepsPtr > 0 && stepsSizeJson > 0 && stepsRaw.Length == stepsSizeJson)
            {
                PadTo(bw, stepsPtr);
                bw.Write(stepsRaw);
                fs.Position = stepsContPos; WriteI32(bw, stepsPtr); WriteI32(bw, stepsSizeJson);
            }
            else
            {
                AlignTo(bw, 0x10);
                int fallbackPtr = (int)fs.Position;
                int fallbackSize = Math.Max(stepsRaw.Length, 176 * numSteps);
                if (stepsRaw.Length > 0) bw.Write(stepsRaw); else if (fallbackSize > 0) WriteZeros(bw, fallbackSize);
                fs.Position = stepsContPos; WriteI32(bw, fallbackPtr); WriteI32(bw, fallbackSize);
            }

            if (compPtr > 0 && compSizeJson > 0 && compRaw.Length == compSizeJson)
            {
                PadTo(bw, compPtr);
                bw.Write(compRaw);
                fs.Position = compContPos; WriteI32(bw, compPtr); WriteI32(bw, compSizeJson);
            }
            else
            {
                AlignTo(bw, 0x10);
                int fallbackPtr = (int)fs.Position;
                int fallbackSize = Math.Max(compRaw.Length, 24 * numComp); // NOTE: Math.max => Math.Max
            }

            {
                fs.Position = fs.Length;
            }

            {
                if (compPtr > 0 && compSizeJson > 0 && compRaw.Length == compSizeJson)
                {
                }
                else
                {
                    AlignTo(bw, 0x10);
                    int fbPtr = (int)fs.Position;
                    int fbSize = Math.Max(compRaw.Length, 24 * numComp);
                    if (compRaw.Length > 0) bw.Write(compRaw); else if (fbSize > 0) WriteZeros(bw, fbSize);
                    fs.Position = compContPos; WriteI32(bw, fbPtr); WriteI32(bw, fbSize);
                }
            }

            if (tailPtr > 0 && tailRaw.Length > 0)
            {
                PadTo(bw, tailPtr);
                bw.Write(tailRaw);
            }

            fs.Position = fs.Length;
            bw.Flush();
            return 0;
        }

        private static byte[] GetB64(JObject root, string key)
        {
            var s = (string?)root[key];
            if (string.IsNullOrEmpty(s)) return Array.Empty<byte>();
            try { return System.Convert.FromBase64String(s!); } catch { return Array.Empty<byte>(); }
        }

        private static void WriteI32(BinaryWriter bw, int v) => bw.Write(v);
        private static void WritePad8(BinaryWriter bw) => WriteZeros(bw, 8);
        private static void AlignTo(BinaryWriter bw, int alignment)
        {
            long pos = bw.BaseStream.Position;
            long pad = ((pos + alignment - 1) / alignment) * alignment - pos;
            if (pad > 0) WriteZeros(bw, (int)pad);
        }
        private static void PadTo(BinaryWriter bw, int absoluteOffset)
        {
            long pos = bw.BaseStream.Position;
            if (absoluteOffset < pos) return; // already past; caller should have validated pointers
            int pad = (int)(absoluteOffset - pos);
            if (pad > 0) WriteZeros(bw, pad);
        }
        private static void WriteZeros(BinaryWriter bw, int count)
        {
            Span<byte> z = stackalloc byte[256];
            z.Clear();
            while (count > 0)
            {
                int c = Math.Min(count, z.Length);
                bw.Write(z[..c]);
                count -= c;
            }
        }
    }
}
