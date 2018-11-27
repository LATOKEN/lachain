using Google.Protobuf;

namespace Phorkus.Core.Storage
{    
    public interface IStorageManager
    {
        TValue Get<TKey, TValue>(ulong version, TKey key)
            where TKey : IMessage
            where TValue : IMessage;
        
        IState NewState(ulong version);
    }
}