using System.Collections.Generic;
using System.IO;
using System.Threading;
using Phorkus.Core.Network.Proto;

namespace Phorkus.Core.Network
{
    public interface ITransport
    {
        void WriteMessages(IEnumerable<Message> messages, Stream stream, CancellationToken cancellationToken);
        
        void WriteMessage(Message message, Stream stream, CancellationToken cancellationToken);

        IEnumerable<Message> ReadMessages(Stream strem, CancellationToken cancellationToken);
        
        Message ReadMessage(Stream strem, CancellationToken cancellationToken);
    }
}