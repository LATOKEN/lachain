using System.Collections.Generic;
using System.IO;
using Phorkus.Core.Network.Proto;

namespace Phorkus.Core.Network
{
    public interface ITransport
    {
        void WriteMessages(IEnumerable<Message> messages, Stream stream);

        IEnumerable<Message> ReadMessages(Stream strem);
    }
}