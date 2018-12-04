namespace Phorkus.Hermes.Signer
{
    public interface ISignerMessage
    {
        void fromByteArray(byte[] buffer);
        
        byte[] ToByteArray();
    }
}