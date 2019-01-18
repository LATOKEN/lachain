namespace Phorkus.Party.Signer
{
    public interface ISignerMessage
    {
        void fromByteArray(byte[] buffer);
        
        byte[] ToByteArray();
    }
}