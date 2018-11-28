using Google.Protobuf;

namespace Phorkus.Hestia
{
    public interface IStorageManager
    {
        long Version { get; }

        TValue Get<TKey, TValue>(ulong version, TKey key)
            where TKey : IMessage
            where TValue : IMessage;
        
        IState NewState(ulong version);
    }
}