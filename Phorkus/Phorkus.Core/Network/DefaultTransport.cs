using System.Collections.Generic;
using System.IO;
using Crc32;
using Phorkus.Core.Network.Proto;
using Phorkus.Core.Uilts;

namespace Phorkus.Core.Network
{
    public class DefaultTransport : ITransport
    {
        private readonly NetworkConfig _networkConfig;

        public DefaultTransport(NetworkConfig networkConfig)
        {
            _networkConfig = networkConfig;
        }

        public void WriteMessages(IEnumerable<Message> messages, Stream stream)
        {
            /* TODO: "possible attack on message size (be careful with limitations)" */
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(_networkConfig.Magic);
                var bytes = messages.ToByteArray();
                writer.Write(bytes.Length);
                writer.Write(bytes);
                var crc32 = Crc32Algorithm.Compute(bytes);
                writer.Write(crc32);
                writer.Flush();
            }
        }

        public IEnumerable<Message> ReadMessages(Stream strem)
        {
            using (var reader = new BinaryReader(strem))
            {
                var magic = reader.ReadUInt32();
                if (magic != _networkConfig.Magic)
                    throw new InvalidMagicException();
                var bytesLength = reader.ReadInt32();
                var bytes = reader.ReadBytes(bytesLength);
                var crc32 = reader.ReadUInt32();
                if (crc32 != Crc32Algorithm.Compute(bytes))
                    throw new ChecksumMismatchException();
                return bytes.ToMessageArray<Message>();
            }
        }
    }
}