using Google.Protobuf;
using Phorkus.Storage.Treap;

namespace Phorkus.Storage.Repositories
{
    public interface IPersistentMapRepository<TKey, TValue>
        where TKey : IMessage 
        where TValue : IMessage
    {
        PersistentTreeMapNode<TKey, TValue> GetNode(IPersistentTreeMap id);
        bool WriteNode(IPersistentTreeMap id, PersistentTreeMapNode<TKey, TValue> node);
    }
}