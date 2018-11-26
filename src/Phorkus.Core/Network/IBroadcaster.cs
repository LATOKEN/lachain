namespace Phorkus.Core.Network
{
    public interface IBroadcaster
    {
        IBlockchainService BlockchainService { get; }

        IConsensusService ConsensusService { get; }
    }
}