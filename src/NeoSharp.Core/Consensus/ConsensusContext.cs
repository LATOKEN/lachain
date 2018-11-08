using System;
using NeoSharp.Core.Cryptography;
using NeoSharp.Core.Models;
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
        public uint PrimaryIndex;
        public byte[] PrivateKey;

        public ConsensusProposal CurrentProposal;
        public ObservedValidatorState[] Validators;

        public int ValidatorCount => Validators.Length;
        public int Quorum => ValidatorCount - (ValidatorCount - 1) / 3;

        private BlockHeader _memoizedHeader = null;

        public BlockHeader GetProposedHeader()
        {
            if (CurrentProposal.TransactionHashes == null) return null;
            if (_memoizedHeader != null) return _memoizedHeader;
            return _memoizedHeader = new BlockHeader
            {
                Version = Version,
                PreviousBlockHash = PreviousBlockHash,
                MerkleRoot = MerkleTree.ComputeRoot(CurrentProposal.TransactionHashes),
                Timestamp = Timestamp,
                Index = BlockIndex,
                ConsensusData = Nonce
            };
        }
    }
}