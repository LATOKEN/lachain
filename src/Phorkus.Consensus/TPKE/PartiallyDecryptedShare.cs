using System;
using System.Collections.Generic;
using Phorkus.Crypto.MCL.BLS12_381;

namespace Phorkus.Consensus.TPKE
{
    public class PartiallyDecryptedShare
    {
        public G1 Ui { get; }
        public int DecryptorId { get; }

        public int ShareId { get; }


        public PartiallyDecryptedShare(G1 _ui, int decryptorId, int shareId)
        {
            Ui = _ui;
            DecryptorId = decryptorId;
            ShareId = shareId;
        }
    }
    
}