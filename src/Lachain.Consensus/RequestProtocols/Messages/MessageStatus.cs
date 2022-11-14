namespace Lachain.Consensus.RequestProtocols.Messages
{
    public enum MessageStatus : byte
    {
        Received = 0,
        Requested = 1,
        NotReceived = 2,
    }
}