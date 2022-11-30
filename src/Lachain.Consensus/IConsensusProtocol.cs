using System;
using Lachain.Consensus.Messages;
using Lachain.Proto;

namespace Lachain.Consensus
{
    public interface IConsensusProtocol
    {
        IProtocolIdentifier Id { get; }
        void ReceiveMessage(MessageEnvelope message);

        void Start();
        
        void StartThread();
        bool HasThreadStarted();
        void WaitFinish();
        bool WaitFinish(TimeSpan timeout);
        void WaitResult();

        void Terminate();

        bool Terminated { get; }

        event EventHandler<(int from, ConsensusMessage msg)>? _receivedExternalMessage;
    }
}