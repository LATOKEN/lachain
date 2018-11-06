using System.Threading.Tasks;
using NeoSharp.Core.Network;

namespace NeoSharp.Core.Messaging
{
    public interface IMessageHandler
    {
        bool CanHandle(Message message);
    }
    
    public abstract class MessageHandler<TMessage> : IMessageHandler
        where TMessage : Message
    {
        public virtual bool CanHandle(Message message) => message is TMessage;
        
        public abstract Task Handle(TMessage message, IPeer sender);
    }
}