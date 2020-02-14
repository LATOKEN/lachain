using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Phorkus.Consensus;
using Phorkus.Consensus.BinaryAgreement;
using Phorkus.Consensus.CommonCoin;
using Phorkus.Consensus.CommonSubset;
using Phorkus.Consensus.HoneyBadger;
using Phorkus.Consensus.Messages;
using Phorkus.Consensus.ReliableBroadcast;
using Phorkus.Consensus.TPKE;
using Phorkus.Core.Blockchain;
using Phorkus.Crypto;
using Phorkus.Logger;
using Phorkus.Networking;
using Phorkus.Proto;
using MessageEnvelope = Phorkus.Consensus.Messages.MessageEnvelope;

namespace Phorkus.Core.Consensus
{
    public class RootProtocolId : IProtocolIdentifier
    {
        public bool Equals(IProtocolIdentifier other)
        {
            if (other == null) return false;
            if (GetType() != other.GetType()) return false;
            return Era == other.Era;
        }

        public long Era { get; }
        
        public IEnumerable<byte> ToByteArray()
        {
            throw new NotImplementedException();
        }

        public RootProtocolId(long era)
        {
            Era = era;
        }
    }
}