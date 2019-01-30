﻿using System;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf.WellKnownTypes;
using NBitcoin;
using Org.BouncyCastle.Bcpg;
using Phorkus.Core.Blockchain;
using Phorkus.Proto;
using Phorkus.Core.Utils;
using Phorkus.Crypto;
using Phorkus.Utility.Utils;
using Block = Phorkus.Proto.Block;
using BlockHeader = Phorkus.Proto.BlockHeader;

namespace Phorkus.Core.Consensus
{
    public class ConsensusContext
    {
        public const uint Version = 0;
        public ConsensusState State;

        public UInt256 PreviousBlockHash;
        public ulong BlockIndex;

        public byte ViewNumber;
        public DateTime LastBlockRecieved;
        public int MyIndex;
        public readonly KeyPair KeyPair;
        public uint SignaturesAcquired;

        public ConsensusProposal CurrentProposal;
        public readonly ObservedValidatorState[] Validators;

        public uint ValidatorCount => (uint) Validators.Length;
        public uint Quorum => ValidatorCount - (ValidatorCount - 1) / 3;

        public long PrimaryIndex =>
            ((long) (BlockIndex - ViewNumber + ValidatorCount) % ValidatorCount + ValidatorCount) % ValidatorCount;

        public ConsensusState Role => MyIndex == PrimaryIndex ? ConsensusState.Primary : ConsensusState.Backup;
        public ObservedValidatorState MyState => MyIndex < 0 || MyIndex >= Validators.Length ? null : Validators[MyIndex];

        public ConsensusContext(KeyPair keyPair, IReadOnlyList<PublicKey> validators)
        {
            KeyPair = keyPair;
            Validators = new ObservedValidatorState[validators.Count];
            for (var i = 0; i < Validators.Length; ++i)
                Validators[i] = new ObservedValidatorState(validators[i]);
            CurrentProposal = null;
        }

        public BlockHeader GetProposedHeader()
        {
            if (CurrentProposal?.TransactionHashes == null)
                return null;
            var result = new BlockHeader
            {
                Version = Version,
                PrevBlockHash = PreviousBlockHash,
                MerkleRoot = MerkleTree.ComputeRoot(CurrentProposal.TransactionHashes) ?? UInt256Utils.Zero,
                Timestamp = CurrentProposal.Timestamp,
                Index = BlockIndex,
                Validator = Validators[PrimaryIndex].PublicKey,
                Nonce = CurrentProposal.Nonce
            };
            return result;
        }

        public IEnumerable<AcceptedTransaction> GetProposedTransactions()
        {
            return CurrentProposal?.Transactions?.Values;
        }

        public Block GetProposedBlock()
        {
            var header = GetProposedHeader();
            var block = new Block
            {
                Header = header,
                TransactionHashes = {CurrentProposal.TransactionHashes},
                Hash = header.ToHash256(),
                Multisig = new MultiSig()
            };
            block.Multisig.Quorum = Quorum;
            foreach (var validator in Validators)
            {
                if (validator.BlockSignature is null || validator.BlockSignature.IsZero())
                    continue;
                if (block.Multisig.Signatures.Select(e => e.Key).Contains(validator.PublicKey))
                    continue;
                var entry = new MultiSig.Types.SignatureByValidator
                {
                    Key = validator.PublicKey,
                    Value = validator.BlockSignature
                };
                block.Multisig.Signatures.Add(entry);
            }

            block.Multisig.Validators.AddRange(Validators.Select(v => v.PublicKey));
            return block;
        }

        public void ResetState(UInt256 lastBlockHash, ulong lastBlockIndex)
        {
            State = ConsensusState.Initial;
            SignaturesAcquired = 0;
            PreviousBlockHash = lastBlockHash;
            BlockIndex = lastBlockIndex + 1;
            ViewNumber = 0;
            MyIndex = -1;
            CurrentProposal = null;
            for (var i = 0; i < Validators.Length; ++i)
            {
                Validators[i].Reset();
                if (Validators[i].PublicKey.Equals(KeyPair?.PublicKey))
                    MyIndex = i;
            }
        }

        public void ChangeView(byte view)
        {
            State &= ConsensusState.SignatureSent;
            ViewNumber = view;
            CurrentProposal = null;
            SignaturesAcquired = 0;
            if (MyState != null)
                MyState.ExpectedViewNumber = view;
        }

        private Validator MakeValidator()
        {
            return new Validator
            {
                Version = Version,
                PrevHash = PreviousBlockHash,
                BlockIndex = BlockIndex,
                ValidatorIndex = (uint) MyIndex,
                ViewNumber = ViewNumber,
                Timestamp = (ulong) DateTime.UtcNow.ToTimestamp().Seconds
            };
        }

        public ConsensusMessage MakeChangeViewRequest()
        {
            return new ConsensusMessage
            {
                ChangeViewRequest = new ChangeViewRequest
                {
                    Validator = MakeValidator(),
                    NewViewNumber = Validators[MyIndex].ExpectedViewNumber
                }
            };
        }

        public ConsensusMessage MakePrepareRequest(BlockWithTransactions block, Signature signature)
        {
            var blockPrepareRequest = new BlockPrepareRequest
            {
                Validator = MakeValidator(),
                Nonce = block.Block.Header.Nonce,
                Signature = signature,
                Timestamp = block.Block.Header.Timestamp
            };
            if (block.Transactions.Count > 0)
                blockPrepareRequest.TransactionHashes.AddRange(block.Block.TransactionHashes);
            return new ConsensusMessage
            {
                BlockPrepareRequest = blockPrepareRequest 
            };
        }

        public ConsensusMessage MakePrepareResponse(Signature signature)
        {
            return new ConsensusMessage
            {
                BlockPrepareReply = new BlockPrepareReply
                {
                    Validator = MakeValidator(),
                    Signature = signature
                }
            };
        }

        public void UpdateCurrentProposal(BlockWithTransactions block)
        {
            CurrentProposal = new ConsensusProposal
            {
                TransactionHashes = block.Transactions.Distinct().Select(t => t.Hash).ToArray(),
                Transactions = block.Transactions.Distinct().ToDictionary(transaction => transaction.Hash)
            };
        }
    }
}