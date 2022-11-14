namespace Lachain.Consensus.RequestProtocols.Messages
{
    public enum RequestType : byte
    {
        Aux = 0,
        Bval = 1,
        Conf = 2,
        Coin = 3,
        Val = 4,
        SignedHeader = 5,
        Ready = 6,
        Decrypted = 7,
        Echo = 8,
    }
}