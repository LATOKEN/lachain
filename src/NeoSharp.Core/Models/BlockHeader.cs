using System;
using NeoSharp.BinarySerialization;
using NeoSharp.Core.Cryptography;
using NeoSharp.Types;
using Newtonsoft.Json;

namespace NeoSharp.Core.Models
{
    /// <summary>
    /// Header
    /// </summary>
    [Serializable]
    public class BlockHeader
    {
        [BinaryProperty(0)]
        [JsonProperty("version")]
        public uint Version;

        [BinaryProperty(1)]
        [JsonProperty("previousblockhash")]
        public UInt256 PreviousBlockHash;

        [BinaryProperty(2)]
        [JsonProperty("merkleroot")]
        public UInt256 MerkleRoot;

        [BinaryProperty(3)]
        [JsonProperty("time")]
        public uint Timestamp;

        [BinaryProperty(4)]
        [JsonProperty("index")]
        public uint Index;

        [BinaryProperty(5)]
        [JsonProperty("nonce")]
        public ulong Nonce;

        /// <summary>
        /// Set the kind of the header
        /// </summary>
        [BinaryProperty(6)]
        public HeaderType Type { get; set; }
        
        [BinaryProperty(7)]
        [JsonProperty("multisig")]
        public MultiSig MultiSig = new MultiSig();
        
        [BinaryProperty(100)]
        [JsonProperty("txhashes")]
        public UInt256[] TransactionHashes { get; set; }

        [JsonProperty("txcount")]
        public int TransactionCount => TransactionHashes?.Length ?? 0;

        [JsonProperty("hash")]
        public UInt256 Hash { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        public BlockHeader()
        {
            Type = HeaderType.Header;
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="type">Type</param>
        public BlockHeader(HeaderType type)
        {
            Type = type;
        }

        /// <summary>
        /// Get block header trimmed
        /// </summary>
        /// <returns>Return block header</returns>
        public BlockHeader Trim()
        {
            if (Type == HeaderType.Header && TransactionHashes.Length == 0)
                return this;
            
            return new BlockHeader(HeaderType.Header)
            {
                Nonce = Nonce,
                Index = Index,
                Hash = Hash,
                MerkleRoot = MerkleRoot,
                TransactionHashes = new UInt256[0],
                PreviousBlockHash = PreviousBlockHash,
                MultiSig = MultiSig,
                Timestamp = Timestamp,
                Version = Version
            };
        }

        /// <summary>
        /// Get block
        /// </summary>
        /// <param name="txs">Transactions</param>
        /// <returns>Return block</returns>
        public Block GetBlock(Transaction[] txs)
        {
            return new Block
            {
                Nonce = Nonce,
                Index = Index,
                Hash = Hash,
                MerkleRoot = MerkleRoot,
                TransactionHashes = TransactionHashes,
                PreviousBlockHash = PreviousBlockHash,
                MultiSig = MultiSig,
                Timestamp = Timestamp,
                Version = Version,
                Transactions = txs
            };
        }
    }
}