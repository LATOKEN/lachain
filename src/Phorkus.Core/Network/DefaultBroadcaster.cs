namespace Phorkus.Core.Network
{
    public class DefaultBroadcaster : IBroadcaster
    {
        private readonly INetworkContext _networkContext;

        public DefaultBroadcaster(INetworkContext networkContext)
        {
            _networkContext = networkContext;
        }
        
        public IBlockchainService BlockchainService { get; }
        public IConsensusService ConsensusService { get; }
    }
}