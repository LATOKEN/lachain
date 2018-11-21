using Phorkus.Core.Blockchain;
using Phorkus.Core.Proto;

namespace Phorkus.Core.Storage
{
    public interface IBalanceRepository
    {
        Money GetBalance(UInt160 owner, UInt160 asset);
        
        Money AddBalance(UInt160 owner, UInt160 asset, Money value);
        
        Money SubBalance(UInt160 owner, UInt160 asset, Money value);
    }
}