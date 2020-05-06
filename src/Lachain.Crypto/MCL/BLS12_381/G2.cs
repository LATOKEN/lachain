using System;
using System.Runtime.InteropServices;
using Lachain.Utility.Serialization;

namespace Lachain.Crypto.MCL.BLS12_381
{
    [StructLayout(LayoutKind.Explicit, Size = 288)]
    public struct G2 : IEquatable<G2>, IFixedWidth
    {
        public const int ByteSize = 96;

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

        public bool IsZero()
        {
            return MclImports.mclBnG2_isZero(ref this) == 1;
        }

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
                fixed (G2* thisPtr = &this)
                {
                    len = MclImports.mclBnG2_serialize((byte*) handle.Pointer, ByteSize, thisPtr);
                }
            }


            if (len != ByteSize) throw new Exception("Failed to serialize G2");
        }

        public static G2 FromBytes(ReadOnlyMemory<byte> bytes)
        {
            var g = new G2();
            long ret;
            unsafe
            {
                using var handle = bytes.Pin();
                ret = MclImports.mclBnG2_deserialize(ref g, (byte*) handle.Pointer, bytes.Length);
            }

            if (ret == 0) throw new Exception("Failed to deserialize G2");
            return g;
        }
        
        public bool Equals(G2 other)
        {
            return MclImports.mclBnG2_isEqual(ref this, ref other) == 1;
        }

        public override bool Equals(object? obj)
        {
            return obj is G2 other && Equals(other);
        }

        public override int GetHashCode()
        {
            unsafe
            {
                fixed (G2* ptr = &this)
                {
                    return ((int*) ptr)[0];
                }
            }
        }
    }
}