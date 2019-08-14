using System;
using Phorkus.Crypto.MCL.BLS12_381;

namespace Phorkus.Consensus.TPKE
{
    public class TPKEPrivKey
    {
        public Fr x;
        public G1 Y;
        public int Id { get; }

        public TPKEPrivKey(Fr _x, int id)
        {
            x = _x;
            Y = new G1();
            Y.Mul(G1.Generator, x);
            Id = id;
        }
        
        public PartiallyDecryptedShare Decrypt(EncryptedShare share)
        {
            G2 H = TPKEUtils.H(share.U, share.V);
            G1 Ui = new G1();
            if (!Mcl.Pairing(G1.Generator, share.W).Equals(Mcl.Pairing(share.U, H)))
            {
                // todo add appropriate catch
                throw new Exception("Invalid share!");
            }
            
            Ui.Mul(share.U, x);

            return new PartiallyDecryptedShare(Ui, Id, share.Id);
        }
    }
}