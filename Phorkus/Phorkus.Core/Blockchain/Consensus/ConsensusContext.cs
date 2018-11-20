using System;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;
using Phorkus.Core.Cryptography;
using Phorkus.Core.Proto;
using Phorkus.Core.Utils;

namespace Phorkus.Core.Blockchain.Consensus
{
    public class ConsensusContext
    {
        public const uint Version = 0;
        public ConsensusState State;

        public UInt256 PreviousBlockHash;
        public ulong BlockIndex;

        public byte ViewNumber;
        public ulong Timestamp;
        public ulong Nonce; // TODO: fix very weak nonce generation mechanism
        public DateTime LastBlockRecieved;
        public long MyIndex;
        public readonly KeyPair KeyPair;
        public uint SignaturesAcquired;

        public ConsensusProposal CurrentProposal;
        public readonly ObservedValidatorState[] Validators;

        public uint ValidatorCount => (uint) Validators.Length;
        public uint Quorum => ValidatorCount - (ValidatorCount - 1) / 3;
        public long PrimaryIndex => (long) ((BlockIndex - ViewNumber + ValidatorCount) % ValidatorCount);
        public ConsensusState Role => MyIndex == PrimaryIndex ? ConsensusState.Primary : ConsensusState.Backup;
        public ObservedValidatorState MyState => MyIndex == -1 ? null : Validators[MyIndex];

        public ConsensusContext(KeyPair keyPair, IReadOnlyList<PublicKey> validators)
        {
            KeyPair = keyPair;
            Validators = new ObservedValidatorState[validators.Count];
            for (var i = 0; i < Validators.Length; ++i)
                Validators[i] = new ObservedValidatorState(validators[i]);
            CurrentProposal = null;
        }

        private BlockHeader _memoizedHeader;

        public BlockHeader GetProposedHeader()
        {
            if (CurrentProposal?.TransactionHashes == null) return null;
            if (_memoizedHeader != null) return _memoizedHeader;
            return _memoizedHeader = new BlockHeader()
            {
                Version = Version,
                PrevBlockHash = PreviousBlockHash,
                MerkleRoot = MerkleTree.ComputeRoot(CurrentProposal.TransactionHashes),
                Timestamp = Timestamp,
                Index = BlockIndex,
                Nonce = Nonce
            };
        }

        public Block GetProposedBlock()
        {
            var block = new Block
            {
                Header = GetProposedHeader(),
                Hash = GetProposedHeader().ToHash256()
            };
            block.Multisig.Quorum = Quorum;
            block.Multisig.Signatures.AddRange(Validators.Select(v => new MultiSig.Types.SignatureByValidator
            {
                Key = v.PublicKey,
                Value = v.BlockSignature
            }));
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
                {
                    MyIndex = i;
                }
            }

            _memoizedHeader = null;
        }

        public void ChangeView(byte view)
        {
            State &= ConsensusState.SignatureSent;
            ViewNumber = view;
            CurrentProposal = null;
            SignaturesAcquired = 0;
            if (MyIndex >= 0) Validators[MyIndex].ExpectedViewNumber = view;
            _memoizedHeader = null;
        }

        private ConsensusPayload MakePayload()
        {
            return new ConsensusPayload
            {
                Version = Version,
                PrevHash = PreviousBlockHash,
                BlockIndex = BlockIndex,
                ValidatorIndex = (uint) MyIndex,
                Timestamp = (ulong) DateTime.UtcNow.ToTimestamp().Seconds
            };
        }

        public ConsensusPayload MakeChangeView()
        {
            var payload = MakePayload();
            payload.ChangeView = new ConsensusChangeView
            {
                NewViewNumber = Validators[MyIndex].ExpectedViewNumber
            };
            return payload;
        }

        public ConsensusPayload MakePrepareRequest(BlockWithTransactions block, Signature signature)
        {
            var payload = MakePayload();
            payload.PrepareRequest = new ConsensusPrepareRequest
            {
                Nonce = block.Block.Header.Nonce,
                MinerTransaction = block.Transactions.First().Transaction,
                Signature = signature,
                
            };
            payload.PrepareRequest.TransactionHashes.AddRange(block.Transactions.Select(tx => tx.Hash));
            return payload;
        }

        public ConsensusPayload MakePrepareResponse(Signature signature)
        {
            var payload = MakePayload();
            payload.PrepareResponse = new ConsensusPrepareResponse
            {
                Signature = signature
            };
            return payload;
        }

        public void UpdateCurrentProposal(BlockWithTransactions block)
        {
            CurrentProposal = new ConsensusProposal
            {
                TransactionHashes = block.Transactions.Select(t => t.Hash).ToArray(),
                Transactions = block.Transactions.ToDictionary(transaction => transaction.Hash)
            };
        }
    }
}