using System.Numerics;
using Phorkus.Proto;
using Phorkus.Utility;
using Phorkus.Utility.Utils;

namespace Phorkus.CrossChain.Ethereum
{
    public class EthereumContractTransaction: IContractTransaction
    {
        public BlockchainType BlockchainType { get; } = BlockchainType.Ethereum;

        public UInt160 Recipient { get; set; }

        public AddressFormat AddressFormat { get; } = AddressFormat.Ripemd160;

        public byte[] TransactionHash { get; }
        
        public Money Value { get; }
        
        public ulong Timestamp { get; }
        
        public EthereumContractTransaction(byte[] from, BigInteger value, byte[] transactionHash, ulong timestamp)
        {
            Recipient = from.ToUInt160();
            Value = MoneyFormatter.FormatMoney(value, EthereumConfig.Decimals);
            TransactionHash = transactionHash;
            Timestamp = timestamp;
        }
    }
}