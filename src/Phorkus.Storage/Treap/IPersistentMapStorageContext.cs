using Google.Protobuf;

namespace Phorkus.Storage.Treap
{
    public interface IPersistentMapStorageContext<TKey, TValue>
        where TKey : IMessage 
        where TValue : IMessage
    {
        PersistentTreeMapNode<TKey, TValue> GetNodeById(IPersistentTreeMap id);
        IPersistentTreeMap PersistNode(IPersistentTreeMap id, PersistentTreeMapNode<TKey, TValue> value);
    }
}