using Phorkus.Proto;

namespace Phorkus.Core.Blockchain
{
    public interface ITransactionFactory
    {
        Transaction TransferTransaction(UInt160 from, UInt160 to, UInt160 asset, Money value);
        
        Transaction MinerTransaction(UInt160 from);
    }
}