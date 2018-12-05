using System.Numerics;

namespace Phorkus.CrossChain.Bitcoin
{
    public class BitcoinContractTransaction: IContractTransaction
    {
        public BlockchainType BlockchainType { get; set; }

        public byte[] From { get; set; }

        public AddressFormat AddressFormat { get; set; }

        public BigInteger Value { get; }

        public BitcoinContractTransaction(BlockchainType blockchainType, byte[] from, AddressFormat addressFormat,
            BigInteger value)
        {
            BlockchainType = blockchainType;
            From = from;
            AddressFormat = addressFormat;
            Value = value;
        }
    }
}