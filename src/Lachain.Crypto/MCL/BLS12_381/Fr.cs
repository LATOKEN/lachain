using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Lachain.Utility.Serialization;

namespace Lachain.Crypto.MCL.BLS12_381
{
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct Fr : IEquatable<Fr>, IFixedWidth
    {
        public const int ByteSize = 32;

        public static Fr Zero = FromInt(0);
        public static Fr One = FromInt(1);

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

        public static bool operator ==(Fr x, Fr y)
        {
            return MclImports.mclBnFr_isEqual(ref x, ref y) != 0;
        }

        public static bool operator !=(Fr x, Fr y)
        {
            return !(x == y);
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

        public static int Width()
        {
            return ByteSize;
        }

        public readonly void Serialize(Memory<byte> bytes)
        {
            long len;
            unsafe
            {
                using var handle = bytes.Pin();
                fixed (Fr* thisPtr = &this)
                {
                    len = MclImports.mclBnFr_serialize((byte*) handle.Pointer, ByteSize, thisPtr);
                }
            }

            if (len != ByteSize) throw new Exception("Failed to serialize Fr");
        }

        public static Fr FromBytes(ReadOnlyMemory<byte> bytes)
        {
            var fr = new Fr();
            long ret;
            unsafe
            {
                using var handle = bytes.Pin();
                ret = MclImports.mclBnFr_deserialize(ref fr, (byte*) handle.Pointer, bytes.Length);
            }

            if (ret == 0) throw new Exception("Failed to deserialize Fr");
            return fr;
        }

        public bool Equals(Fr other)
        {
            return MclImports.mclBnFr_isEqual(ref this, ref other) == 1;
        }

        public override bool Equals(object? obj)
        {
            return obj is Fr other && Equals(other);
        }

        public override int GetHashCode()
        {
            unsafe
            {
                fixed (Fr* ptr = &this)
                {
                    return ((int*) ptr)[0];
                }
            }
        }
    }
}