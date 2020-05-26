using System;
using System.Runtime.InteropServices;
using System.Text;
using Lachain.Utility.Serialization;

namespace Lachain.Crypto.MCL.BLS12_381
{
    [StructLayout(LayoutKind.Explicit, Size = 144)]
    public struct G1 : IEquatable<G1>, IFixedWidth
    {
        public const int ByteSize = 48;

        public static G1 GetGenerator()
        {
            // Some fixed generator can be obtained via hashing any message
            // (all non trivial elements are generators since group has prime order)
            var res = new G1();
            res.SetHashOf(new byte[] {0xde, 0xad, 0xbe, 0xef});
            return res;
        }

        public static G1 GetZero()
        {
            var res = new G1();
            res.Clear();
            return res;
        }

        public static G1 Generator = GetGenerator();
        public static G1 Zero = GetZero();

        public void Clear()
        {
            MclImports.mclBnG1_clear(ref this);
        }

        public void SetHashOf(byte[] array)
        {
            MclImports.mclBnG1_hashAndMapTo(ref this, array, (uint) array.Length);
        }

        public bool IsValid()
        {
            return MclImports.mclBnG1_isValid(ref this) == 1;
        }

        public bool IsZero()
        {
            return MclImports.mclBnG1_isZero(ref this) == 1;
        }

        public string GetStr(int ioMode)
        {
            var sb = new StringBuilder(1024);
            var size = MclImports.mclBnG1_getStr(sb, sb.Capacity, ref this, ioMode);
            if (size == 0)
            {
                throw new InvalidOperationException("mclBnG1_getStr:");
            }

            return sb.ToString();
        }

        public void Neg(G1 x)
        {
            MclImports.mclBnG1_neg(ref this, ref x);
        }

        public void Dbl(G1 x)
        {
            MclImports.mclBnG1_dbl(ref this, ref x);
        }

        public void Add(G1 x, G1 y)
        {
            MclImports.mclBnG1_add(ref this, ref x, ref y);
        }

        public void Sub(G1 x, G1 y)
        {
            MclImports.mclBnG1_sub(ref this, ref x, ref y);
        }

        public void Mul(G1 x, Fr y)
        {
            MclImports.mclBnG1_mul(ref this, ref x, ref y);
        }

        public static G1 operator +(G1 x, G1 y)
        {
            var z = new G1();
            z.Add(x, y);
            return z;
        }

        public static G1 operator -(G1 x, G1 y)
        {
            var z = new G1();
            z.Sub(x, y);
            return z;
        }

        public static G1 operator *(G1 x, Fr y)
        {
            var z = new G1();
            z.Mul(x, y);
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
                fixed (G1* thisPtr = &this)
                {
                    len = MclImports.mclBnG1_serialize((byte*) handle.Pointer, ByteSize, thisPtr);
                }
            }


            if (len != ByteSize) throw new Exception("Failed to serialize G1");
        }

        public static G1 FromBytes(ReadOnlyMemory<byte> bytes)
        {
            var g = new G1();
            long ret;
            unsafe
            {
                using var handle = bytes.Pin();
                ret = MclImports.mclBnG1_deserialize(ref g, (byte*) handle.Pointer, bytes.Length);
            }

            if (ret == 0) throw new Exception("Failed to deserialize G1");
            return g;
        }

        public bool Equals(G1 other)
        {
            return MclImports.mclBnG1_isEqual(ref this, ref other) == 1;
        }

        public override bool Equals(object? obj)
        {
            return obj is G1 other && Equals(other);
        }

        public override int GetHashCode()
        {
            unsafe
            {
                fixed (G1* ptr = &this)
                {
                    return ((int*) ptr)[0];
                }
            }
        }
    }
}