namespace Lachain.Consensus.RequestProtocols.Messages
{
    public enum RequestType : byte
    {
        // the lower valaue assigned, the more priority for each protocol
        
        // messages for BB
        Bval = 0,
        Aux = 1,
        Conf = 2,

        // messages for CC
        Coin = 3,

        // messages for Root
        SignedHeader = 4,

        // messages for RBC
        Val = 5,
        Echo = 6,
        Ready = 7,

        // messages for HB
        Decrypted = 8,
    }
}