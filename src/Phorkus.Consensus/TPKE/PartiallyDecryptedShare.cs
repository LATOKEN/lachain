using System;
using System.Collections.Generic;
using Phorkus.Crypto.MCL.BLS12_381;

namespace Phorkus.Consensus.TPKE
{
    public class PartiallyDecryptedShare
    {
        public G1 Ui { get; }
        public int Id { get; }

        public int ShareId { get; }

        public PartiallyDecryptedShare(G1 _ui, int _id, int shareId)
        {
            Ui = _ui;
            Id = _id;
            ShareId = shareId;
        }
    }
    
}