namespace Phorkus.Consensus
{
    public interface IProtocolIdentifier
    {
        uint Epoch { get; }
        byte[] ToByteArray();
    }
}