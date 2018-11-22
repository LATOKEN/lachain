using System;
using System.IO;
using NeoSharp.Core.Caching;
using NeoSharp.Core.Types;

namespace NeoSharp.Core.Consensus.Messages
{
    public enum ConsensusMessageType : byte
    {
        [ReflectionCache(typeof(ChangeView))]
        ChangeView = 0x00,

        [ReflectionCache(typeof(PrepareRequest))]
        PrepareRequest = 0x20,

        [ReflectionCache(typeof(PrepareResponse))]
        PrepareResponse = 0x21,
    }

    public abstract class ConsensusPayloadCustomData : ISerializable
    {
        private static ReflectionCache<byte> ReflectionCache =
            ReflectionCache<byte>.CreateFromEnum<ConsensusMessageType>();

        public readonly ConsensusMessageType Type;
        public byte ViewNumber;

        public int Size => sizeof(ConsensusMessageType) + sizeof(byte);

        protected ConsensusPayloadCustomData(ConsensusMessageType type)
        {
            this.Type = type;
        }

        public static ConsensusPayloadCustomData DeserializeFrom(byte[] data)
        {
            ConsensusPayloadCustomData message = ReflectionCache.CreateInstance<ConsensusPayloadCustomData>(data[0]);
            if (message == null) throw new FormatException();
            using (MemoryStream ms = new MemoryStream(data, false))
            using (BinaryReader r = new BinaryReader(ms))
            {
                message.Deserialize(r);
            }

            return message;
        }

        public virtual void Deserialize(BinaryReader reader)
        {
            if (Type != (ConsensusMessageType) reader.ReadByte())
                throw new FormatException();
            ViewNumber = reader.ReadByte();
        }

        public virtual void Serialize(BinaryWriter writer)
        {
            writer.Write((byte) Type);
            writer.Write(ViewNumber);
        }
    }
}