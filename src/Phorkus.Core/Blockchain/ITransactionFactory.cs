using Phorkus.Proto;

namespace Phorkus.Core.Blockchain
{
    public interface ITransactionFactory
    {
        Transaction TransferMoney(UInt160 from, UInt160 to, UInt160 asset, Money value);
    }
}