using Phorkus.Proto;

namespace Phorkus.CrossChain
{
    public interface IContractTransaction
    {
        BlockchainType BlockchainType { get; }

        byte[] From { get; }
        
        AddressFormat AddressFormat { get; }

        UInt256 Value { get; }
    }
}