using System;
using System.Collections.Generic;
using System.Linq;
using NeoSharp.Core.Consensus.Messages;
using NeoSharp.Core.Cryptography;
using NeoSharp.Core.Extensions;
using NeoSharp.Core.Messaging.Messages;
using NeoSharp.Core.Models;
using NeoSharp.Core.Models.Transactions;
using NeoSharp.Types;

namespace NeoSharp.Core.Consensus
{
    public class ConsensusContext
    {
        public const uint Version = 0;
        public ConsensusState State;

        public UInt256 PreviousBlockHash;
        public uint BlockIndex;

        public byte ViewNumber;
        public uint Timestamp;
        public ulong Nonce; // TODO: fix very weak nonce generation mechanism
        public DateTime LastBlockRecieved;
        public int MyIndex;
        private readonly KeyPair PrivateKey;
        public uint SignaturesAcquired;

        public ConsensusProposal CurrentProposal;
        public readonly ObservedValidatorState[] Validators;

        public uint ValidatorCount => (uint) Validators.Length;
        public uint Quorum => ValidatorCount - (ValidatorCount - 1) / 3;
        public uint PrimaryIndex => (BlockIndex - ViewNumber + ValidatorCount) % ValidatorCount;
        public ConsensusState Role => MyIndex == PrimaryIndex ? ConsensusState.Primary : ConsensusState.Backup;
        public ObservedValidatorState MyState => MyIndex == -1 ? null : Validators[MyIndex];

        public ConsensusContext(KeyPair keyPair, IReadOnlyList<PublicKey> validators)
        {
            PrivateKey = keyPair;
            Validators = new ObservedValidatorState[validators.Count];
            for (var i = 0; i < Validators.Length; ++i)
            {
                Validators[i] = new ObservedValidatorState(validators[i]);
            }

            CurrentProposal = null;
        }

        private BlockHeader _memoizedHeader;

        private BlockHeader GetProposedHeader()
        {
            if (CurrentProposal?.TransactionHashes == null) return null;
            if (_memoizedHeader != null) return _memoizedHeader;
            return _memoizedHeader = new BlockHeader
            {
                Version = Version,
                PreviousBlockHash = PreviousBlockHash,
                MerkleRoot = MerkleTree.ComputeRoot(CurrentProposal.TransactionHashes),
                Timestamp = Timestamp,
                Index = BlockIndex,
                Nonce = Nonce
            };
        }

        public Block GetProposedBlock()
        {
            return GetProposedHeader()?.GetBlock(CurrentProposal.Transactions.Values.ToArray());
        }

        public void ResetState(UInt256 lastBlockHash, uint lastBlockIndex)
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
                if (Validators[i].PublicKey.CompareTo(PrivateKey?.PublicKey) == 0)
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

        public ConsensusPayload MakeChangeView()
        {
            return MakePayload(new ChangeView
            {
                NewViewNumber = Validators[MyIndex].ExpectedViewNumber
            });
        }

        private ConsensusPayload MakePayload(ConsensusPayloadCustomData message)
        {
            message.ViewNumber = ViewNumber;
            return new ConsensusPayload
            {
                Unsigned = new ConsensusPayloadUnsigned
                {
                    Version = Version,
                    PrevHash = PreviousBlockHash,
                    BlockIndex = BlockIndex,
                    ValidatorIndex = (ushort) MyIndex,
                    Timestamp = Timestamp,
                    Data = message.ToArray()
                }
            };
        }

        public ConsensusPayload MakePrepareRequest()
        {
            return MakePayload(new PrepareRequest
            {
                Nonce = Nonce,
                TransactionHashes = CurrentProposal.TransactionHashes,
                MinerTransaction =
                    (MinerTransaction) CurrentProposal.Transactions[CurrentProposal.TransactionHashes[0]],
                Signature = Validators[MyIndex].BlockSignature
            });
        }

        public ConsensusPayload MakePrepareResponse(byte[] signature)
        {
            return MakePayload(new PrepareResponse
            {
                Signature = signature
            });
        }
    }
}