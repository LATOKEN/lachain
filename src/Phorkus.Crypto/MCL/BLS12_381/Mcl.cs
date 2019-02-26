using System;

namespace Phorkus.Crypto.MCL.BLS12_381
{
    public static class Mcl
    {
        private static bool _initCalled;

        public static void Init()
        {
            if (_initCalled) return; // TODO: make singleton or whatsoever
            const int curveBls12381 = 5;
            const int compileTimeVar = 46;
            var error = MclImports.mclBn_init(curveBls12381, compileTimeVar);
            if (error != 0)
            {
                throw new InvalidOperationException("mclBn_init returned error " + error);
            }

            _initCalled = true;
        }

        public static G2 LagrangeInterpolateG2(Fr[] xs, G2[] ys)
        {
            if (xs.Length != ys.Length) throw new ArgumentException("arrays are unequal length");
            var res = new G2();
            MclImports.mclBn_G2LagrangeInterpolation(ref res, xs, ys, xs.Length);
            return res;
        }
        
        public static G1 LagrangeInterpolateG1(Fr[] xs, G1[] ys)
        {
            if (xs.Length != ys.Length) throw new ArgumentException("arrays are unequal length");
            var res = new G1();
            MclImports.mclBn_G1LagrangeInterpolation(ref res, xs, ys, xs.Length);
            return res;
        }

        public static GT Pairing(G1 x, G2 y)
        {
            var res = new GT();
            MclImports.mclBn_pairing(ref res, ref x, ref y);
            return res;
        }
    }
}