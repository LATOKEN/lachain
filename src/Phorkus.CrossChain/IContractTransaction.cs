using Phorkus.Proto;
using Phorkus.Utility;

namespace Phorkus.CrossChain
{
    public interface IContractTransaction
    {
        BlockchainType BlockchainType { get; }

        UInt160 Recipient { get; }
        
        AddressFormat AddressFormat { get; }

        byte[] TransactionHash { get; }
        
        Money Value { get; }

        ulong Timestamp { get; }
    }
}