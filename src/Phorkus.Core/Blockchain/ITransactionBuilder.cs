using Phorkus.Proto;
using Phorkus.Utility;

namespace Phorkus.Core.Blockchain
{
    public interface ITransactionBuilder
    {
        Transaction RegisterTransaction(AssetType type, string name, Money supply, uint decimals, UInt160 owner);
        
        Transaction TransferTransaction(UInt160 from, UInt160 to, UInt160 asset, Money value);

        Transaction MinerTransaction(UInt160 from);

        Transaction DepositTransaction(UInt160 from, UInt160 recipient, BlockchainType blockchainType, Money value,
            byte[] transactionHash, AddressFormat addressFormat, ulong timestamp);

        Transaction ConfirmTransaction(UInt160 from, UInt160 recipient, BlockchainType blockchainType, Money value,
            byte[] transactionHash, AddressFormat addressFormat, ulong timestamp);

        Transaction WithdrawTransaction(UInt160 from, UInt160 recipient, BlockchainType blockchainType, Money value,
            byte[] transactionHash, AddressFormat addressFormat, ulong timestamp);

        Transaction ContractTransaction(UInt160 from, UInt160 to, Asset asset, UInt256 value, UInt256 fee,
            byte[] script);
    }
}