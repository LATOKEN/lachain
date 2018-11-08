using System.IO;

namespace NeoSharp.Core.Consensus.Messages
{
    internal class PrepareResponse : ConsensusPayloadCustomData
    {
        public byte[] Signature;

        public PrepareResponse()
            : base(ConsensusMessageType.PrepareResponse)
        {
        }

        public override void Deserialize(BinaryReader reader)
        {
            base.Deserialize(reader);
            Signature = reader.ReadBytes(64);
        }

        public override void Serialize(BinaryWriter writer)
        {
            base.Serialize(writer);
            writer.Write(Signature);
        }
    }
}
