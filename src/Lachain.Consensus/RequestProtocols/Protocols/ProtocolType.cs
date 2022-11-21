namespace Lachain.Consensus.RequestProtocols.Protocols
{
    public enum ProtocolType : byte
    {
        Root = 0,
        HoneyBadger = 1,
        ReliableBroadcast = 2,
        BinaryBroadcast = 3,
        CommonCoin = 4,
        CommonSubset = 5,
        BinaryAgreement = 6,
    }
}