using System;
using System.Collections.Generic;
using Phorkus.Consensus;
using Phorkus.Consensus.Messages;

namespace Phorkus.ConsensusTest
{
    public class InvokerId : IProtocolIdentifier
    {
        public ulong Era => 0;

        public bool Equals(IProtocolIdentifier other)
        {
            if (ReferenceEquals(this, other)) return true;
            return false;
        }

        public IEnumerable<byte> ToByteArray()
        {
            return new byte[] { };
        }
    }

    public class ProtocolInvoker<TId, TResult> : IConsensusProtocol where TId : IProtocolIdentifier
    {
        public IProtocolIdentifier Id { get; } = new InvokerId();
        public bool Terminated => false;

        public int ResultSet;
        public TResult Result;

        public void ReceiveMessage(MessageEnvelope message)
        {
            if (!message.External && message.InternalMessage is ProtocolResult<TId, TResult> result)
            {
                ResultSet++;
                Result = result.Result;
            }
        }

        public void Start()
        {
        }

        public void WaitFinish()
        {
        }
    }
}