using NeoSharp.Core.Models;

namespace NeoSharp.Core.Blockchain.Processing.TranscationProcessing
{
    public interface ITransactionVerifier
    {
        bool Verify(Transaction transaction);
    }
}