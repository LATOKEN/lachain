using Phorkus.Proto;
using Phorkus.Utility;

namespace Phorkus.Core.Blockchain.State
{
    public interface IBalanceSnapshot
    {
        Money GetBalance(UInt160 owner, UInt160 asset);
        void TransferBalance(UInt160 from, UInt160 to, UInt160 asset, Money value);
        Money AddBalance(UInt160 owner, UInt160 asset, Money value);
        Money SubBalance(UInt160 owner, UInt160 asset, Money value);
    }
}