using System;
using Lachain.Consensus.Messages;

namespace Lachain.Consensus
{
    public interface IConsensusProtocol
    {
        IProtocolIdentifier Id { get; }
        void ReceiveMessage(MessageEnvelope message);

        void Start();

        void StartThread();
        void WaitFinish();
        bool WaitFinish(TimeSpan timeout);
        void WaitResult();

        void Terminate();

        bool Terminated { get; }
    }
}