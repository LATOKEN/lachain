using System;
using System.Threading.Tasks;
using NeoSharp.Core.Models;

namespace NeoSharp.Core.Blockchain.Processing
{
    public interface ITransactionProcessor : IDisposable
    {
        void Run();
        Task AddTransaction(Transaction transaction);
    }
}