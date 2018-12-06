using System.Numerics;

namespace Phorkus.CrossChain.Ethereum
{
    public class EthereumConfig
    {
        public static readonly string InitV = "17";
        public static readonly string RpcUri = @"http://localhost:8545";
        public static readonly BigInteger GasTransfer = 21000;
        public static readonly string NullData = "00";
        public static readonly int Decimals = 18;
    }
}