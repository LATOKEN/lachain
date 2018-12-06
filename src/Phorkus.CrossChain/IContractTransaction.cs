using System.Numerics;

namespace Phorkus.CrossChain
{
    public interface IContractTransaction
    {
        BlockchainType BlockchainType { get; }

        byte[] From { get; }
        
        AddressFormat AddressFormat { get; }

        BigInteger Value { get; }
    }
}