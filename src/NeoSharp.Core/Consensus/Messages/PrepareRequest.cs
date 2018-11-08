using System;
using System.IO;
using System.Linq;
using NeoSharp.BinarySerialization;
using NeoSharp.Core.Models;
using NeoSharp.Types;

namespace NeoSharp.Core.Consensus.Messages
{
    internal class PrepareRequest : ConsensusPayloadCustomData
    {
        public ulong Nonce;
        public UInt256[] TransactionHashes;
        public MinerTransaction MinerTransaction;
        public byte[] Signature;

        public PrepareRequest() : base(ConsensusMessageType.PrepareRequest)
        {
        }

        public override void Deserialize(BinaryReader reader)
        {
            base.Deserialize(reader);
            Nonce = reader.ReadUInt64();
            TransactionHashes = SerializationHelper.ReadSerializableArray<UInt256>(reader);
            if (TransactionHashes.Distinct().Count() != TransactionHashes.Length)
                throw new FormatException("Cannot deserialize PrepareRequest: duplicate transactions");
            MinerTransaction = BinarySerializer.Default.Deserialize<MinerTransaction>(reader);
            if (MinerTransaction.Hash != TransactionHashes[0])
                throw new FormatException("Cannot deserialize PrepareRequest: miner transaction must be first");
            Signature = reader.ReadBytes(64);
        }

        public override void Serialize(BinaryWriter writer)
        {
            base.Serialize(writer);
            writer.Write(Nonce);
            SerializationHelper.WriteSerializableArray(writer, TransactionHashes);
            writer.Write(BinarySerializer.Default.Serialize(MinerTransaction));
            writer.Write(Signature);
        }
    }
}