using Phorkus.Proto;
using Phorkus.Utility;

namespace Phorkus.CrossChain
{
    public interface IContractTransaction
    {
        BlockchainType BlockchainType { get; }

        byte[] From { get; }
        
        AddressFormat AddressFormat { get; }

        Money Value { get; }

        ulong Timestamp { get; }
    }
}