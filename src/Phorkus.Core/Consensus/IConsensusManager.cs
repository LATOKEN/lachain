using Phorkus.Proto;

namespace Phorkus.Core.Consensus
{
    public interface IConsensusManager
    {
        void Start();
        void Stop();
        void OnChangeViewReceived(ChangeViewRequest changeViewRequest);
        void OnPrepareResponseReceived(BlockPrepareReply prepareResponse);
        void OnPrepareRequestReceived(BlockPrepareRequest prepareRequest);
    }
}