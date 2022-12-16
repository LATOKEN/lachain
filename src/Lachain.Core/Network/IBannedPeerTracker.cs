namespace Lachain.Core.Network
{
    public interface IBannedPeerTracker
    {
        uint ThresholdForBan { get; }
        void Start();
    }
}