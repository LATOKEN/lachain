using Phorkus.Core.Blockchain;
using Phorkus.Proto;

namespace Phorkus.Core.Storage
{
    public interface IBalanceRepository
    {
        Money GetBalance(UInt160 owner, UInt160 asset);

        void TransferBalance(UInt160 from, UInt160 to, UInt160 asset, Money value);
        
        Money AddBalance(UInt160 owner, UInt160 asset, Money value);
        
        Money SubBalance(UInt160 owner, UInt160 asset, Money value);
    }
}