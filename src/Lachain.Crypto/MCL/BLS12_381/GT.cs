using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Lachain.Crypto.MCL.BLS12_381
{
    // ReSharper disable once InconsistentNaming
    [StructLayout(LayoutKind.Explicit, Size = 576)]
    public struct GT
    {
        public void Clear()
        {
            MclImports.mclBnGT_clear(ref this);
        }

        public bool Equals(GT rhs)
        {
            return MclImports.mclBnGT_isEqual(ref this, ref rhs) == 1;
        }

        public bool IsZero()
        {
            return MclImports.mclBnGT_isZero(ref this) == 1;
        }

        public bool IsOne()
        {
            return MclImports.mclBnGT_isOne(ref this) == 1;
        }

        public string GetStr(int ioMode)
        {
            var sb = new StringBuilder(1024);
            var size = MclImports.mclBnGT_getStr(sb, sb.Capacity, ref this, ioMode);
            if (size == 0)
            {
                throw new InvalidOperationException("MclImports.mclBnGT_getStr:");
            }

            return sb.ToString();
        }

        public void Neg(GT x)
        {
            MclImports.mclBnGT_neg(ref this, ref x);
        }

        public void Inv(GT x)
        {
            MclImports.mclBnGT_inv(ref this, ref x);
        }

        public void Add(GT x, GT y)
        {
            MclImports.mclBnGT_add(ref this, ref x, ref y);
        }

        public void Sub(GT x, GT y)
        {
            MclImports.mclBnGT_sub(ref this, ref x, ref y);
        }

        public void Mul(GT x, GT y)
        {
            MclImports.mclBnGT_mul(ref this, ref x, ref y);
        }

        public void Div(GT x, GT y)
        {
            MclImports.mclBnGT_div(ref this, ref x, ref y);
        }

        public static GT operator -(GT x)
        {
            var y = new GT();
            y.Neg(x);
            return y;
        }

        public static GT operator +(GT x, GT y)
        {
            var z = new GT();
            z.Add(x, y);
            return z;
        }

        public static GT operator -(GT x, GT y)
        {
            var z = new GT();
            z.Sub(x, y);
            return z;
        }

        public static GT operator *(GT x, GT y)
        {
            var z = new GT();
            z.Mul(x, y);
            return z;
        }

        public static GT operator /(GT x, GT y)
        {
            var z = new GT();
            z.Div(x, y);
            return z;
        }

        private void PowInternal(GT x, Fr y)
        {
            MclImports.mclBnGT_pow(ref this, ref x, ref y);
        }
        
        public static GT Pow(GT x, Fr y)
        {
            var g = new GT();
            g.PowInternal(x, y);
            return g;
        }

        public void Pairing(G1 x, G2 y)
        {
            MclImports.mclBn_pairing(ref this, ref x, ref y);
        }

        public void FinalExp(GT x)
        {
            MclImports.mclBn_finalExp(ref this, ref x);
        }

        public void MillerLoop(G1 x, G2 y)
        {
            MclImports.mclBn_millerLoop(ref this, ref x, ref y);
        }
    }
}