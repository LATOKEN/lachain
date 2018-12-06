using System.Numerics;
using Phorkus.Proto;
using Phorkus.Utility;

namespace Phorkus.CrossChain.Bitcoin
{
    public class BitcoinContractTransaction: IContractTransaction
    {
        public BlockchainType BlockchainType { get; set; }

        public byte[] From { get; set; }

        public AddressFormat AddressFormat { get; set; }

        public byte[] TransactionHash { get; }
        
        public Money Value { get; }
        
        public ulong Timestamp { get; }
        
        public BitcoinContractTransaction(BlockchainType blockchainType, byte[] from, AddressFormat addressFormat,
            BigInteger value)
        {
            BlockchainType = blockchainType;
            From = from;
            AddressFormat = addressFormat;
            Value = MoneyFormatter.FormatMoney(value, BitcoinConfig.Decimals);
        }
    }
}