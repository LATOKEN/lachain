using System;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf.WellKnownTypes;
using Phorkus.Core.Blockchain;
using Phorkus.Proto;
using Phorkus.Core.Utils;
using Phorkus.Crypto;
using Phorkus.Utility.Utils;

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
        public long MyIndex;
        public readonly KeyPair KeyPair;
        public uint SignaturesAcquired;

        public ConsensusProposal CurrentProposal;
        public readonly ObservedValidatorState[] Validators;

        public uint ValidatorCount => (uint) Validators.Length;
        public uint Quorum => 2; //ValidatorCount - (ValidatorCount - 1) / 3;

        public long PrimaryIndex =>
            ((long) (BlockIndex - ViewNumber + ValidatorCount) % ValidatorCount + ValidatorCount) % ValidatorCount;

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

        public BlockHeader GetProposedHeader()
        {
            if (CurrentProposal?.TransactionHashes == null) return null;
            var result = new BlockHeader
            {
                Version = Version,
                PrevBlockHash = PreviousBlockHash,
                MerkleRoot = MerkleTree.ComputeRoot(CurrentProposal.TransactionHashes),
                Timestamp = CurrentProposal.Timestamp,
                Index = BlockIndex,
                Nonce = CurrentProposal.Nonce
            };
            return result;
        }

        public Block GetProposedBlock()
        {
            var header = GetProposedHeader();
            var block = new Block
            {
                Header = header,
                TransactionHashes = { CurrentProposal.TransactionHashes },
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
                {
                    MyIndex = i;
                }
            }
        }

        public void ChangeView(byte view)
        {
            State &= ConsensusState.SignatureSent;
            ViewNumber = view;
            CurrentProposal = null;
            SignaturesAcquired = 0;
            if (MyIndex >= 0)
                Validators[MyIndex].ExpectedViewNumber = view;
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

        public ChangeViewRequest MakeChangeViewRequest()
        {
            return new ChangeViewRequest
            {
                Validator = MakeValidator(),
                NewViewNumber = Validators[MyIndex].ExpectedViewNumber
            };
        }

        public ChangeViewReply MakeChangeViewReply()
        {
            return new ChangeViewReply
            {
                Validator = MakeValidator(),
                ViewNumber = Validators[MyIndex].ExpectedViewNumber
            };
        }
        
        public BlockPrepareRequest MakePrepareRequest(BlockWithTransactions block, Signature signature)
        {
            return new BlockPrepareRequest
            {
                Validator = MakeValidator(),
                Nonce = block.Block.Header.Nonce,
                TransactionHashes =
                {
                    block.Transactions.Select(tx => tx.Hash)
                },
                MinerTransaction = block.Transactions.First().Transaction,
                Signature = signature,
                Timestamp = block.Block.Header.Timestamp
            };
        }

        public BlockPrepareReply MakePrepareResponse(Signature signature)
        {
            return new BlockPrepareReply
            {
                Validator = MakeValidator(),
                Signature = signature
            };
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