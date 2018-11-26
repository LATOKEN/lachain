using Google.Protobuf;
using Phorkus.Proto;

namespace Phorkus.Core.Consensus
{
    public interface IConsensusManager
    {
        void Start();
        
        void Stop();

        bool CanHandleConsensusMessage(Validator validator, IMessage message);

        void OnBlockPrepareRequestReceived(BlockPrepareRequest blockPrepare);

        void OnChangeViewReceived(ChangeViewRequest changeView);
    }
}