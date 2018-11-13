using System.Threading.Tasks;
using NeoSharp.Core.Cryptography;
using NeoSharp.Core.Models;
using NeoSharp.Types;

namespace NeoSharp.Core.Blockchain
{
    public interface ITransactionManager
    {
        Task<Transaction> CreateContractTransaction(UInt160 asset, UInt160 from, UInt160 to, UInt256 value);

        Task<Signature> SignTransaction(Transaction transaction, KeyPair privateKey);
    }
}