using System.Numerics;
using Phorkus.Proto;
using Phorkus.Utility;
using Phorkus.Utility.Utils;

namespace Phorkus.CrossChain.Bitcoin
{
    public class BitcoinContractTransaction: IContractTransaction
    {
        public BlockchainType BlockchainType { get; } = BlockchainType.Bitcoin;

        public UInt160 Recipient { get; set; }

        public AddressFormat AddressFormat { get; } = AddressFormat.Ripemd160;

        public byte[] TransactionHash { get; }
        
        public Money Value { get; }
        
        public ulong Timestamp { get; }
        
        public BitcoinContractTransaction(byte[] from, BigInteger value, byte[] transactionHash, ulong timestamp)
        {
            Recipient = from.ToUInt160();
            Value = MoneyFormatter.FormatMoney(value, BitcoinConfig.Decimals);
            TransactionHash = transactionHash;
            Timestamp = timestamp;
        }
    }
}