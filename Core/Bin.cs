using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace D3Edit.Core
{
    internal static class Bin
    {
        public static int I32(ReadOnlySpan<byte> s) => BinaryPrimitives.ReadInt32LittleEndian(s);
        public static float F32(ReadOnlySpan<byte> s) => BitConverter.Int32BitsToSingle(I32(s));

        public static void WriteI32(Stream w, int v)
        {
            Span<byte> b = stackalloc byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(b, v);
            w.Write(b);
        }
        public static void WriteF32(Stream w, float v) => WriteI32(w, BitConverter.SingleToInt32Bits(v));

        public static string ZStr(ReadOnlySpan<byte> s)
        {
            int n = s.IndexOf((byte)0);
            if (n < 0) n = s.Length;
            return Encoding.ASCII.GetString(s.Slice(0, n));
        }

        public static void WriteFixedAsciiZ(Stream w, string s, int totalBytes)
        {
            var raw = Encoding.ASCII.GetBytes(s ?? "");
            int copy = Math.Min(raw.Length, totalBytes - 1);
            w.Write(raw, 0, copy);
            w.WriteByte(0);
            int pad = totalBytes - (copy + 1);
            if (pad > 0) WriteZeros(w, pad);
        }

        public static void WriteZeros(Stream w, int count)
        {
            Span<byte> z = stackalloc byte[256];
            z.Clear();
            while (count > 0)
            {
                int c = Math.Min(count, z.Length);
                w.Write(z[..c]);
                count -= c;
            }
        }
    }
}
