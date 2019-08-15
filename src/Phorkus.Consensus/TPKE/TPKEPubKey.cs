using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Phorkus.Crypto.MCL.BLS12_381;
using Phorkus.Proto;

namespace Phorkus.Consensus.TPKE
{
    public class TPKEPubKey 
    {
        public G1 Y;
        static int LEN = 32;
        public int t;

        public TPKEPubKey(G1 _Y, int _t)
        {
            Y = _Y;
            t = _t;
        }

        public EncryptedShare Encrypt(IRawShare rawShare)
        {
            var r = Fr.GetRandom();

            G1 U = G1.Generator * r;

            G1 T = Y * r;
            byte[] V = TPKEUtils.XOR(TPKEUtils.G(T), rawShare.ToBytes());

            G2 W = TPKEUtils.H(U, V) * r;
            
            return new EncryptedShare(U, V, W, rawShare.Id);
        }

        public PartiallyDecryptedShare Decode(DecMessage message)
        {
            throw new NotImplementedException();
        }

        public DecMessage Encode(PartiallyDecryptedShare share)
        {
            throw new NotImplementedException();
        }

        public IRawShare FullDecrypt(EncryptedShare share, List<PartiallyDecryptedShare> us)
        {
            if (us.Count < t)
            {
                throw new Exception("Unsufficient number of shares!");
            } 
            
            var ys = new List<G1>();
            var xs = new List<Fr>();

            foreach (var part in us)
            {
                xs.Add(Fr.FromInt(part.Id + 1));
                ys.Add(part.Ui);
            }

            var U = Mcl.LagrangeInterpolateG1(xs.ToArray(), ys.ToArray());
            return new RawShare(TPKEUtils.XOR(TPKEUtils.G(U), share.V), share.Id);
        }
    }
}