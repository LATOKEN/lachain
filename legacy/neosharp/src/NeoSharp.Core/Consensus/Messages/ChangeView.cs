using System;
using System.IO;

namespace NeoSharp.Core.Consensus.Messages
{
    internal class ChangeView : ConsensusPayloadCustomData
    {
        public byte NewViewNumber;

        public ChangeView()
            : base(ConsensusMessageType.ChangeView)
        {
        }

        public override void Deserialize(BinaryReader reader)
        {
            base.Deserialize(reader);
            NewViewNumber = reader.ReadByte();
            if (NewViewNumber == 0) throw new FormatException();
        }

        public override void Serialize(BinaryWriter writer)
        {
            base.Serialize(writer);
            writer.Write(NewViewNumber);
        }
    }
}
