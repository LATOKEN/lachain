using System;
using System.Collections.Generic;

namespace Phorkus.Consensus.CommonSubset
{
    public interface ICommonSubsetProtocol
    {
        void ProvideInput(int id, IReadOnlyCollection<byte[]> transactionsHashes);
        event EventHandler<IReadOnlyCollection<byte[]>> CommonSubsetAcquired;
    }
}