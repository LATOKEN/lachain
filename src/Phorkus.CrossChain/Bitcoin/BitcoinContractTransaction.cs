using System.Numerics;
using Phorkus.Proto;
using Phorkus.Utility;

namespace Phorkus.CrossChain.Bitcoin
{
    public class BitcoinContractTransaction: IContractTransaction
    {
        public BlockchainType BlockchainType { get; } = BlockchainType.Bitcoin;

        public byte[] From { get; set; }

        public AddressFormat AddressFormat { get; } = AddressFormat.Ripmd160;

        public byte[] TransactionHash { get; }
        
        public Money Value { get; }
        
        public ulong Timestamp { get; }
        
        public BitcoinContractTransaction(byte[] from, BigInteger value, byte[] transactionHash, ulong timestamp)
        {
            From = from;
            Value = MoneyFormatter.FormatMoney(value, BitcoinConfig.Decimals);
            TransactionHash = transactionHash;
            Timestamp = timestamp;
        }
    }
}