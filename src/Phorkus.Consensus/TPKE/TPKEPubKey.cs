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
        static int LEN = 1000;
        public int t;

        public TPKEPubKey(G1 _Y, int _t)
        {
            Y = _Y;
            t = _t;
        }

        public EncryptedShare Encrypt(IRawShare rawShare)
        {
            // todo replace with cryptographic random
            var rand = new Random();
            var temp = new byte[LEN];
            rand.NextBytes(temp);
            var r = Fr.FromBytes(temp);

            G1 U = new G1();
            U.Mul(G1.Generator, r);

            G1 T = new G1();
            T.Mul(Y, r);
            byte[] V = TPKEUtils.XOR(TPKEUtils.G(T), rawShare.ToBytes());
            
            G2 W = new G2();
            W.Mul(TPKEUtils.H(U, V), r);
            
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

        public IRawShare FullDecrypt(EncryptedShare share, ISet<PartiallyDecryptedShare> us)
        {
            if (us.Count < t)
            {
                throw new Exception("Unsufficient number of shares!");
            } 
            
            // todo add lagrange interpolation
            throw new NotImplementedException();
        }
    }
}