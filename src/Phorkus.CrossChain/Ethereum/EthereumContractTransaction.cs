using System.Numerics;
using Phorkus.Proto;
using Phorkus.Utility;

namespace Phorkus.CrossChain.Ethereum
{
    public class EthereumContractTransaction: IContractTransaction
    {
        public BlockchainType BlockchainType { get; } = BlockchainType.Ethereum;

        public byte[] From { get; set; }

        public AddressFormat AddressFormat { get; } = AddressFormat.Ripmd160;

        public byte[] TransactionHash { get; }
        
        public Money Value { get; }
        
        public ulong Timestamp { get; }
        
        public EthereumContractTransaction(byte[] from, BigInteger value)
        {
            From = from;
            Value = MoneyFormatter.FormatMoney(value, EthereumConfig.Decimals);
        }
    }
}