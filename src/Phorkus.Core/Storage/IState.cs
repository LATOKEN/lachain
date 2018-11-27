using Google.Protobuf;

namespace Phorkus.Core.Storage
{
    public interface IState
    {
        ulong Add<TKey, TValue>(ulong version, TKey key, TValue value)
            where TKey : IMessage
            where TValue : IMessage;
        
        ulong Remove<TKey>(ulong version, TKey key)
            where TKey : IMessage;
        
        ulong Commit();
        ulong Cancel();
    }
}