using System;
using System.Collections.Generic;
using Phorkus.Crypto.MCL.BLS12_381;

namespace Phorkus.Consensus.TPKE
{
    public class EncryptedShare
    {
        public G1 U { get; }
        public byte[] V { get; }
        public G2 W { get; }
        public int Id { get; }

        public EncryptedShare(G1 _U, byte[] _V, G2 _W, int id)
        {
            U = _U;
            V = _V;
            W = _W;
            Id = id;
        }
        
        
        public bool Equals(EncryptedShare other)
        {
            throw new System.NotImplementedException();
        }

        public int CompareTo(EncryptedShare other)
        {
            throw new System.NotImplementedException();
        }

        public byte[] ToBytes()
        {
            throw new System.NotImplementedException();
        }
        
    }
}