namespace Phorkus.Storage
{
    public interface IPersistentMapStorageContext<TIDentifier, TKey, TValue>
    {
        TIDentifier NullIDentifier { get; }
        
        PersistentTreeMapNode<TIDentifier, TKey, TValue> GetNodeById(TIDentifier id);
        TIDentifier NewNode(TIDentifier leftSon, TIDentifier rightSon, TKey key, TValue value);
    }
}