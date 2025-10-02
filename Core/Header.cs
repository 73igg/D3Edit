using System;
using System.IO;

namespace D3Edit.Core
{
    public sealed class Header
    {
        public int DeadBeef { get; set; }
        public int SnoType { get; set; }
        public int Unknown1 { get; set; }
        public int Unknown2 { get; set; }
        public int SNOId { get; set; }
        public int Unknown3 { get; set; }
        public int Unknown4 { get; set; }

        public int BalanceType { get; set; }
        public int I0 { get; set; }
        public int I1 { get; set; }

        public static Header Read(ReadOnlySpan<byte> s)
        {
            if (s.Length < 28) throw new InvalidDataException("Header too small.");
            return new Header
            {
                DeadBeef = Bin.I32(s.Slice(0, 4)),
                SnoType = Bin.I32(s.Slice(4, 4)),
                Unknown1 = Bin.I32(s.Slice(8, 4)),
                Unknown2 = Bin.I32(s.Slice(12, 4)),
                SNOId = Bin.I32(s.Slice(16, 4)),
                Unknown3 = Bin.I32(s.Slice(20, 4)),
                Unknown4 = Bin.I32(s.Slice(24, 4)),
            };
        }

        public static Header Read(BinaryReader br)
        {
            return new Header
            {
                DeadBeef = br.ReadInt32(),
                SnoType = br.ReadInt32(),
                Unknown1 = br.ReadInt32(),
                Unknown2 = br.ReadInt32(),
                SNOId = br.ReadInt32(),
                Unknown3 = br.ReadInt32(),
                Unknown4 = br.ReadInt32()
            };
        }

        public void Write(Stream w)
        {
            Bin.WriteI32(w, DeadBeef);
            Bin.WriteI32(w, SnoType);
            Bin.WriteI32(w, Unknown1);
            Bin.WriteI32(w, Unknown2);
            Bin.WriteI32(w, SNOId);
            Bin.WriteI32(w, Unknown3);
            Bin.WriteI32(w, Unknown4);
        }

        public static void Write(BinaryWriter bw, Header h)
        {
            bw.Write(h.DeadBeef);
            bw.Write(h.SnoType);
            bw.Write(h.Unknown1);
            bw.Write(h.Unknown2);
            bw.Write(h.SNOId);
            bw.Write(h.Unknown3);
            bw.Write(h.Unknown4);
        }

        public static Header Default() => new Header
        {
            DeadBeef = unchecked((int)0xDEADBEEF),
            SnoType = 0,
            Unknown1 = 0,
            Unknown2 = 0,
            SNOId = 0,
            Unknown3 = 0,
            Unknown4 = 0
        };
    }
}
