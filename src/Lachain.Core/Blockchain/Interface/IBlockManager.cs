using System;
using System.Collections.Generic;
using Lachain.Core.Blockchain.Error;
using Lachain.Core.Blockchain.Operations;
using Lachain.Core.Blockchain.VM;
using Lachain.Crypto.ECDSA;
using Lachain.Proto;

namespace Lachain.Core.Blockchain.Interface
{
    public interface IBlockManager
    {
        event EventHandler<Block> OnBlockPersisted;
        event EventHandler<InvocationContext> OnSystemContractInvoked;

        ulong GetHeight();
        Block LatestBlock();
        Block? GetByHeight(ulong blockHeight);
        Block? GetByHash(UInt256 blockHash);

        bool TryBuildGenesisBlock();

        Tuple<OperatingError, List<TransactionReceipt>, UInt256, List<TransactionReceipt>> Emulate(Block block,
            IEnumerable<TransactionReceipt> transactions);

        OperatingError Execute(Block block, IEnumerable<TransactionReceipt> transactions, bool checkStateHash,
            bool commit);

        Signature Sign(BlockHeader block, EcdsaKeyPair keyPair);
        OperatingError VerifySignature(BlockHeader blockHeader, Signature signature, ECDSAPublicKey publicKey);
        OperatingError VerifySignatures(Block block);
        OperatingError Verify(Block block);
        ulong CalcEstimatedFee(UInt256 blockHash);
        ulong CalcEstimatedFee();
    }
}