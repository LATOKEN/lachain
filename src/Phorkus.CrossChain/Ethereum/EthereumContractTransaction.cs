using System.Numerics;

namespace Phorkus.CrossChain.Ethereum
{
    public class EthereumContractTransaction: IContractTransaction
    {
        public BlockchainType BlockchainType { get; } = BlockchainType.Ethereum;

        public byte[] From { get; set; }

        public AddressFormat AddressFormat { get; } = AddressFormat.Ripmd160;

        public BigInteger Value { get; }
        
        public EthereumContractTransaction(byte[] from, BigInteger value)
        {
            From = from;
            Value = value;
        }
    }
}