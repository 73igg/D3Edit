using System;
using System.IO;
using D3Edit.Core;

namespace D3Edit
{
    internal static class Program
    {
        static int Main(string[] args)
        {
            if (args.Length != 2)
            {
                Console.Error.WriteLine("Usage: D3Edit <input> <output>");
                return 2;
            }

            string inPath = args[0];
            string outPath = args[1];

            try
            {
                string inExt = Path.GetExtension(inPath).ToLowerInvariant();
                string outExt = Path.GetExtension(outPath).ToLowerInvariant();

                if (inExt == ".gam" && outExt == ".json")
                    return GamToJson.Convert(inPath, outPath);
                if (inExt == ".json" && outExt == ".gam")
                    return JsonToGam.Convert(inPath, outPath);

                if (inExt == ".qst" && outExt == ".json")
                    return QstToJson.Convert(inPath, outPath);
                if (inExt == ".json" && outExt == ".qst") 
                    return JsonToQst.Convert(inPath, outPath);

                if (inExt == ".acr" && outExt == ".json")
                    return AcrToJson.Convert(inPath, outPath);

                if (inExt == ".mon" && outExt == ".json")
                    return MonToJson.Convert(inPath, outPath);
                if (inExt == ".json" && outExt == ".mon")
                    return JsonToMon.Convert(inPath, outPath);

                Console.Error.WriteLine("Please read the ReadMe for supported filetypes and conversions.");
                return 2;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("FAIL: " + ex.Message);
                return 1;
            }
        }
    }
}
