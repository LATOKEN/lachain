using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Lachain.Crypto.MCL.BLS12_381
{
    [StructLayout(LayoutKind.Explicit, Size = 288)]
    public struct G2
    {
        public const int BYTE_SIZE = 96;
        public static G2 GetGenerator()
        {
            // Some fixed generator can be obtained via hashing any message
            // (all non trivial elements are generators since group has prime order)
            var res = new G2();
            res.SetHashOf(new byte[] {0xfe, 0xed, 0xfa, 0xce});
            return res;
        }
        
        public static G2 GetZero()
        {
            var res = new G2();
            res.Clear();
            return res;
        }
        
        public static byte[] ToBytes(G2 g)
        {
            const int BUF_SIZE = 200;
            var buf = new byte[BUF_SIZE];
            var len = MclImports.mclBnG2_serialize(buf, BUF_SIZE, ref g);
            if (len == 0)
            {
                throw new Exception("Failed to serialize G2");
            }
            return buf.Take((int) len).ToArray();
        }

        public static G2 FromBytes(byte[] buf)
        {
            var g = new G2();
            if (MclImports.mclBnG2_deserialize(ref g, buf, buf.Length) == 0)
            {
                throw new Exception("Failed to deserialize G2");
            }

            return g;
        }
        
        public static byte[] ToBytesDelimited(G2 g)
        {
            var buffer = ToBytes(g);
            return BitConverter.GetBytes(buffer.Length).Concat(buffer).ToArray();
        }

        public static G2 FromBytesDelimited(byte[] buffer)
        {
            var len = BitConverter.ToInt32(buffer, 0);
            return FromBytes(buffer.Skip(4).Take(len).ToArray());
        }

        public static G2 Generator = GetGenerator();
        public static G2 Zero = GetZero();

        public void Clear()
        {
            MclImports.mclBnG2_clear(ref this);
        }

        public void SetHashOf(byte[] array)
        {
            MclImports.mclBnG2_hashAndMapTo(ref this, array, (uint) array.Length);
        }

        public bool IsValid()
        {
            return MclImports.mclBnG2_isValid(ref this) == 1;
        }

        public bool Equals(G2 rhs)
        {
            return MclImports.mclBnG2_isEqual(ref this, ref rhs) == 1;
        }

        public bool IsZero()
        {
            return MclImports.mclBnG2_isZero(ref this) == 1;
        }

        public byte[] ToBytes()
        {
            return ToBytes(this);
//            var bytes = new byte[96];
//            var size = MclImports.mclBnG2_getStr(bytes, bytes.Length, ref this, 512);
//            if (size == 0)
//            {
//                throw new InvalidOperationException("mclBnG2_getStr:");
//            }

//            return bytes;
        }

        public string ToHex(bool prefix = true)
        {
            var bytes = new byte[256];
            var size = MclImports.mclBnG2_getStr(bytes, bytes.Length, ref this, 2048);
            if (size == 0)
            {
                throw new InvalidOperationException("mclBnG2_getStr:");
            }

            return (prefix ? "0x" : "") + Encoding.ASCII.GetString(bytes.Take((int) size).ToArray());
        }

        public static G2 FromHex(string s)
        {
            if (s.StartsWith("0x")) s = s.Substring(2);
            var bytes = Encoding.ASCII.GetBytes(s);
            var res = new G2();
            MclImports.mclBnG2_setStr(ref res, bytes, bytes.Length, 2048);
            return res;
        }

//        public static G2 FromBytes(byte[] bytes)
//        {
//            var res = new G2();
//            MclImports.mclBnG2_setStr(ref res, bytes, bytes.Length, 512);
//            return res;
//        }

        public void Neg(G2 x)
        {
            MclImports.mclBnG2_neg(ref this, ref x);
        }

        public void Dbl(G2 x)
        {
            MclImports.mclBnG2_dbl(ref this, ref x);
        }

        public void Add(G2 x, G2 y)
        {
            MclImports.mclBnG2_add(ref this, ref x, ref y);
        }

        public void Sub(G2 x, G2 y)
        {
            MclImports.mclBnG2_sub(ref this, ref x, ref y);
        }

        public void Mul(G2 x, Fr y)
        {
            MclImports.mclBnG2_mul(ref this, ref x, ref y);
        }

        public static G2 operator +(G2 x, G2 y)
        {
            var z = new G2();
            z.Add(x, y);
            return z;
        }

        public static G2 operator -(G2 x, G2 y)
        {
            var z = new G2();
            z.Sub(x, y);
            return z;
        }

        public static G2 operator *(G2 x, Fr y)
        {
            var z = new G2();
            z.Mul(x, y);
            return z;
        }
    }
}