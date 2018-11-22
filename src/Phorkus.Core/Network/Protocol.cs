namespace Phorkus.Core.Network
{
    public enum Protocol : byte
    {
        Unknown = 0,
        Tcp = 1,
        TcpWithTls = 2,
        Ws = 3,
        WsWithTls = 3
    }
}