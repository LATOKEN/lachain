﻿using System;
using Phorkus.Crypto;
using Phorkus.Proto;
using Phorkus.Utility;

namespace Phorkus.Core.Blockchain.OperationManager
{
    public interface IBlockManager
    {
        event EventHandler<Block> OnBlockPersisted;
        event EventHandler<Block> OnBlockSigned;
        
        Block GetByHeight(ulong blockHeight);
        
        Block GetByHash(UInt256 blockHash);
        
        OperatingError Persist(Block block);
        
        Signature Sign(BlockHeader block, KeyPair keyPair);
        
        OperatingError VerifySignature(BlockHeader blockHeader, Signature signature, PublicKey publicKey);
        
        OperatingError VerifySignatures(Block block);
        
        OperatingError Verify(Block block);

        Money CalcEstimatedFee(UInt256 blockHash);
        
        Money CalcEstimatedFee();
    }
}