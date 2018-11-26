using Google.Protobuf;

namespace Phorkus.Storage.Treap
{
    public interface IPersistentMapStorageContext<TKey, TValue>
        where TKey : IMessage 
        where TValue : IMessage
    {
        IPersistentTreeMap NullIDentifier { get; }
        
        PersistentTreeMapNode<TKey, TValue> GetNodeById(IPersistentTreeMap id);
        IPersistentTreeMap NewNode(IPersistentTreeMap leftSon, IPersistentTreeMap rightSon, TKey key, TValue value);
    }
}