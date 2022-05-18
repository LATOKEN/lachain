using System.Collections.Generic;
using Lachain.Core.Blockchain.Error;
using Lachain.Proto;



namespace Lachain.Core.Network.FastSynchronizerBatch
{
    public interface IBlockRequestManager
    {
        ulong MaxBlock { get; }
        void Initialize();
        bool TryGetBatch(out ulong fromBlock, out ulong toBlock);
        bool Done();
        void HandleResponse(ulong fromBlock, ulong toBlock, List<Block> response, ECDSAPublicKey? peer);
        OperatingError VerifySignatures(Block? block);
    }
}