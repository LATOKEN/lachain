using System.Collections.Generic;
using System.IO;
using System.Threading;
using Phorkus.Core.Network.Proto;

namespace Phorkus.Core.Network
{
    public class DefaultTransport : ITransport
    {
        public void WriteMessages(IEnumerable<Message> messages, Stream stream, CancellationToken cancellationToken)
        {
            throw new System.NotImplementedException();
        }

        public void WriteMessage(Message message, Stream stream, CancellationToken cancellationToken)
        {
            throw new System.NotImplementedException();
        }

        public IEnumerable<Message> ReadMessages(Stream strem, CancellationToken cancellationToken)
        {
            throw new System.NotImplementedException();
        }

        public Message ReadMessage(Stream strem, CancellationToken cancellationToken)
        {
            throw new System.NotImplementedException();
        }
    }
}