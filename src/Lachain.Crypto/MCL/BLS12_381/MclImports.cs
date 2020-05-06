using System.Runtime.InteropServices;
using System.Text;

namespace Lachain.Crypto.MCL.BLS12_381
{
    internal static class MclImports
    {
        private const string Libmcl = "mclbn384_256.so";

        [DllImport(Libmcl)]
        internal static extern int mclBn_init(int curve, int maxUnitSize);

        /* ====== Fr ====== */
        [DllImport(Libmcl)]
        internal static extern void mclBnFr_clear(ref Fr x);

        [DllImport(Libmcl)]
        internal static extern void mclBnFr_setInt32(ref Fr y, int x);

        [DllImport(Libmcl)]
        internal static extern int mclBnFr_setByCSPRNG(ref Fr y);

        [DllImport(Libmcl)]
        public static extern int mclBnFr_setStr(
            ref Fr x,
            [In] [MarshalAs(UnmanagedType.LPArray)]
            byte[] buf,
            long bufSize, int ioMode
        );

        [DllImport(Libmcl)]
        internal static extern int mclBnFr_getStr([Out] StringBuilder buf, long maxBufSize, ref Fr x, int ioMode);

        [DllImport(Libmcl)]
        internal static extern int mclBnFr_isValid(ref Fr x);

        [DllImport(Libmcl)]
        internal static extern int mclBnFr_isEqual(ref Fr x, ref Fr y);

        [DllImport(Libmcl)]
        internal static extern int mclBnFr_isZero(ref Fr x);

        [DllImport(Libmcl)]
        internal static extern int mclBnFr_isOne(ref Fr x);

        [DllImport(Libmcl)]
        internal static extern void mclBnFr_neg(ref Fr y, ref Fr x);

        [DllImport(Libmcl)]
        internal static extern void mclBnFr_inv(ref Fr y, ref Fr x);

        [DllImport(Libmcl)]
        internal static extern void mclBnFr_add(ref Fr z, ref Fr x, ref Fr y);

        [DllImport(Libmcl)]
        internal static extern void mclBnFr_sub(ref Fr z, ref Fr x, ref Fr y);

        [DllImport(Libmcl)]
        internal static extern void mclBnFr_mul(ref Fr z, ref Fr x, ref Fr y);

        [DllImport(Libmcl)]
        internal static extern void mclBnFr_div(ref Fr z, ref Fr x, ref Fr y);

        /* ====== G1 ====== */
        [DllImport(Libmcl)]
        internal static extern void mclBnG1_clear(ref G1 x);

        [DllImport(Libmcl)]
        internal static extern int mclBnG1_isValid(ref G1 x);

        [DllImport(Libmcl)]
        internal static extern int mclBnG1_isEqual(ref G1 x, ref G1 y);

        [DllImport(Libmcl)]
        internal static extern int mclBnG1_isZero(ref G1 x);
        
        [DllImport(Libmcl)]
        internal static extern unsafe long mclBnG2_serialize(byte* buf, long maxBufSize, G2* x);
        
        [DllImport(Libmcl)]
        internal static extern unsafe long mclBnG2_deserialize(ref G2 x, byte* buf, long bufSize);

        [DllImport(Libmcl)]
        internal static extern unsafe long mclBnG1_serialize(byte* buf, long maxBufSize, G1* x);

        [DllImport(Libmcl)]
        internal static extern unsafe long mclBnG1_deserialize(ref G1 x, byte* buf, long bufSize);

        [DllImport(Libmcl)]
        internal static extern unsafe long mclBnFr_serialize(byte* buf, long maxBufSize, Fr* fr);

        [DllImport(Libmcl)]
        internal static extern unsafe long mclBnFr_deserialize(ref Fr fr, byte* buf, long bufSize);
        
        [DllImport(Libmcl)]
        internal static extern long mclBnG1_getStr([Out] StringBuilder buf, long maxBufSize, ref G1 x, int ioMode);

        [DllImport(Libmcl)]
        internal static extern void mclBnG1_neg(ref G1 y, ref G1 x);

        [DllImport(Libmcl)]
        internal static extern void mclBnG1_dbl(ref G1 y, ref G1 x);

        [DllImport(Libmcl)]
        internal static extern void mclBnG1_add(ref G1 z, ref G1 x, ref G1 y);

        [DllImport(Libmcl)]
        internal static extern void mclBnG1_sub(ref G1 z, ref G1 x, ref G1 y);

        [DllImport(Libmcl)]
        internal static extern void mclBnG1_mul(ref G1 z, ref G1 x, ref Fr y);

        [DllImport(Libmcl)]
        internal static extern void mclBnG1_hashAndMapTo(
            ref G1 z,
            [In] [MarshalAs(UnmanagedType.LPArray)]
            byte[] buf,
            uint bufSize
        );

        /* ====== G2 ====== */
        [DllImport(Libmcl)]
        internal static extern void mclBnG2_clear(ref G2 x);

        [DllImport(Libmcl)]
        internal static extern int mclBnG2_isValid(ref G2 x);

        [DllImport(Libmcl)]
        internal static extern int mclBnG2_isEqual(ref G2 x, ref G2 y);

        [DllImport(Libmcl)]
        internal static extern int mclBnG2_isZero(ref G2 x);

        [DllImport(Libmcl)]
        internal static extern long mclBnG2_getStr(
            [Out] [MarshalAs(UnmanagedType.LPArray)]
            byte[] buf,
            long maxBufSize, ref G2 x, int ioMode
        );

        [DllImport(Libmcl)]
        internal static extern int mclBnG2_setStr(
            ref G2 x,
            [In] [MarshalAs(UnmanagedType.LPArray)]
            byte[] buf,
            long bufSize, int ioMode
        );

        [DllImport(Libmcl)]
        internal static extern void mclBnG2_neg(ref G2 y, ref G2 x);

        [DllImport(Libmcl)]
        internal static extern void mclBnG2_dbl(ref G2 y, ref G2 x);

        [DllImport(Libmcl)]
        internal static extern void mclBnG2_add(ref G2 z, ref G2 x, ref G2 y);

        [DllImport(Libmcl)]
        internal static extern void mclBnG2_sub(ref G2 z, ref G2 x, ref G2 y);

        [DllImport(Libmcl)]
        internal static extern void mclBnG2_mul(ref G2 z, ref G2 x, ref Fr y);

        [DllImport(Libmcl)]
        internal static extern void mclBnG2_hashAndMapTo(ref G2 z,
            [In] [MarshalAs(UnmanagedType.LPArray)]
            byte[] buf,
            uint bufSize
        );

        /* ====== GT ====== */
        [DllImport(Libmcl)]
        internal static extern void mclBnGT_clear(ref GT x);

        [DllImport(Libmcl)]
        internal static extern int mclBnGT_isEqual(ref GT x, ref GT y);

        [DllImport(Libmcl)]
        internal static extern int mclBnGT_isZero(ref GT x);

        [DllImport(Libmcl)]
        internal static extern int mclBnGT_isOne(ref GT x);

        [DllImport(Libmcl)]
        internal static extern long mclBnGT_getStr([Out] StringBuilder buf, long maxBufSize, ref GT x, int ioMode);

        [DllImport(Libmcl)]
        internal static extern void mclBnGT_neg(ref GT y, ref GT x);

        [DllImport(Libmcl)]
        internal static extern void mclBnGT_inv(ref GT y, ref GT x);

        [DllImport(Libmcl)]
        internal static extern void mclBnGT_add(ref GT z, ref GT x, ref GT y);

        [DllImport(Libmcl)]
        internal static extern void mclBnGT_sub(ref GT z, ref GT x, ref GT y);

        [DllImport(Libmcl)]
        internal static extern void mclBnGT_mul(ref GT z, ref GT x, ref GT y);

        [DllImport(Libmcl)]
        internal static extern void mclBnGT_div(ref GT z, ref GT x, ref GT y);

        [DllImport(Libmcl)]
        internal static extern void mclBnGT_pow(ref GT z, ref GT x, ref Fr y);

        /* ====== Operations ====== */

        [DllImport(Libmcl)]
        internal static extern void mclBn_pairing(ref GT z, ref G1 x, ref G2 y);

        [DllImport(Libmcl)]
        internal static extern void mclBn_finalExp(ref GT y, ref GT x);

        [DllImport(Libmcl)]
        internal static extern void mclBn_millerLoop(ref GT z, ref G1 x, ref G2 y);

        [DllImport(Libmcl)]
        internal static extern int mclBn_G2LagrangeInterpolation(
            ref G2 res,
            [In] [MarshalAs(UnmanagedType.LPArray)]
            Fr[] xVec,
            [In] [MarshalAs(UnmanagedType.LPArray)]
            G2[] yVec,
            long k
        );
        
        [DllImport(Libmcl)]
        internal static extern int mclBn_G1LagrangeInterpolation(
            ref G1 res,
            [In] [MarshalAs(UnmanagedType.LPArray)]
            Fr[] xVec,
            [In] [MarshalAs(UnmanagedType.LPArray)]
            G1[] yVec,
            long k
        );

        
        [DllImport(Libmcl)]
        internal static extern int mclBn_FrLagrangeInterpolation(
            ref Fr res,
            [In] [MarshalAs(UnmanagedType.LPArray)]
            Fr[] xVec,
            [In] [MarshalAs(UnmanagedType.LPArray)]
            Fr[] yVec,
            long k
        );
        
        [DllImport(Libmcl)]
        internal static extern int mclBn_FrEvaluatePolynomial(
            ref Fr res,
            [In] [MarshalAs(UnmanagedType.LPArray)]
            Fr[] cVec,
            long k,
            ref Fr at
        );
        
        [DllImport(Libmcl)]
        internal static extern int mclBn_G1EvaluatePolynomial(
            ref G1 res,
            [In] [MarshalAs(UnmanagedType.LPArray)]
            G1[] cVec,
            long k,
            ref Fr at
        );
        
        [DllImport(Libmcl)]
        internal static extern int mclBn_G2EvaluatePolynomial(
            ref G2 res,
            [In] [MarshalAs(UnmanagedType.LPArray)]
            G2[] cVec,
            long k,
            ref Fr at
        );
    }
}