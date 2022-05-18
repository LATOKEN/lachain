using System.Collections.Generic;
using Lachain.Proto;


namespace Lachain.Core.Network.FastSynchronizerBatch
{
    public interface IBlockRequestManager
    {
        ulong MaxBlock { get; }
        void SetMaxBlock(ulong maxBlock);
        bool TryGetBatch(out List<ulong> batch);
        bool Done();
        void HandleResponse(List<ulong> batch, List<Block> response);
    }
}