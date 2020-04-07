using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Lachain.Crypto.MCL.BLS12_381
{
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct Fr
    {
        public static Fr Zero = FromInt(0);
        public static Fr One = FromInt(1);
        public static int ByteSize = 32;

        public static Fr FromBytes(IEnumerable<byte> array)
        {
            var res = new Fr();
            var bytes = array.ToArray();
            MclImports.mclBnFr_setStr(ref res, bytes, bytes.Length, 512);
            return res;
        }

        public static byte[] ToBytes(Fr fr)
        {
            var buf = new byte[ByteSize];
            var len = MclImports.mclBnFr_serialize(buf, ByteSize, ref fr);
            if (len != 32) throw new Exception("Failed to serialize Fr");
            return buf;
        }

        public static Fr FromBytes(byte[] buf)
        {
            var fr = new Fr();
            if (MclImports.mclBnFr_deserialize(ref fr, buf, buf.Length) == 0)
                throw new Exception("Failed to deserialize Fr");

            return fr;
        }

        public static Fr GetRandom()
        {
            // todo replace with cryptographic random
            var fr = new Fr();
            MclImports.mclBnFr_setByCSPRNG(ref fr);
            return fr;
        }

        public static Fr FromHex(string s)
        {
            var res = new Fr();
            var bytes = Encoding.ASCII.GetBytes(s).ToArray();
            MclImports.mclBnFr_setStr(ref res, bytes, bytes.Length, 16);
            return res;
        }

        public static Fr FromInt(int x)
        {
            var res = new Fr();
            res.SetInt(x);
            return res;
        }

        public void Clear()
        {
            MclImports.mclBnFr_clear(ref this);
        }

        public void SetInt(int x)
        {
            MclImports.mclBnFr_setInt32(ref this, x);
        }

        public bool IsValid()
        {
            return MclImports.mclBnFr_isValid(ref this) == 1;
        }

        public bool Equals(Fr rhs)
        {
            return MclImports.mclBnFr_isEqual(ref this, ref rhs) == 1;
        }

        public bool IsZero()
        {
            return MclImports.mclBnFr_isZero(ref this) == 1;
        }

        public bool IsOne()
        {
            return MclImports.mclBnFr_isOne(ref this) == 1;
        }

        public string GetStr(int ioMode)
        {
            var sb = new StringBuilder(1024);
            long size = MclImports.mclBnFr_getStr(sb, sb.Capacity, ref this, ioMode);
            if (size == 0)
            {
                throw new InvalidOperationException("mclBnFr_getStr:");
            }

            return sb.ToString();
        }

        public void Neg(Fr x)
        {
            MclImports.mclBnFr_neg(ref this, ref x);
        }

        public void Inv(Fr x)
        {
            MclImports.mclBnFr_inv(ref this, ref x);
        }

        public void Add(Fr x, Fr y)
        {
            MclImports.mclBnFr_add(ref this, ref x, ref y);
        }

        public void Sub(Fr x, Fr y)
        {
            MclImports.mclBnFr_sub(ref this, ref x, ref y);
        }

        public void Mul(Fr x, Fr y)
        {
            MclImports.mclBnFr_mul(ref this, ref x, ref y);
        }

        public void Div(Fr x, Fr y)
        {
            MclImports.mclBnFr_div(ref this, ref x, ref y);
        }

        public static Fr operator -(Fr x)
        {
            var y = new Fr();
            y.Neg(x);
            return y;
        }

        public static Fr operator +(Fr x, Fr y)
        {
            var z = new Fr();
            z.Add(x, y);
            return z;
        }

        public static Fr operator -(Fr x, Fr y)
        {
            var z = new Fr();
            z.Sub(x, y);
            return z;
        }

        public static Fr operator *(Fr x, Fr y)
        {
            var z = new Fr();
            z.Mul(x, y);
            return z;
        }

        public static Fr operator /(Fr x, Fr y)
        {
            var z = new Fr();
            z.Div(x, y);
            return z;
        }
    }
}