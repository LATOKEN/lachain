using System;
using System.Threading.Tasks;
using NeoSharp.Core.Models;

namespace NeoSharp.Core.Blockchain.Processing
{
    public interface ITransactionProcessor : IDisposable
    {
        event EventHandler<Transaction> OnTransactionProcessed;
        
        void Run();
        Task AddTransaction(Transaction transaction);
    }
}