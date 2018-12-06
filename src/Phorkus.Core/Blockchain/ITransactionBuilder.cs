using Phorkus.Proto;
using Phorkus.Utility;

namespace Phorkus.Core.Blockchain
{
    public interface ITransactionBuilder
    {
        Transaction TransferTransaction(UInt160 from, UInt160 to, UInt160 asset, Money value);
        
        Transaction MinerTransaction(UInt160 from);

        Transaction DepositTransaction(UInt160 from, BlockchainType blockchainType, Money value, AddressFormat addressFormat, ulong timestamp);
    }
}