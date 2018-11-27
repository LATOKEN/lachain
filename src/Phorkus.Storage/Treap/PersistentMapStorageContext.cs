using Google.Protobuf;
using Phorkus.Storage.Repositories;

namespace Phorkus.Storage.Treap
{
    public class
        PersistentMapStorageContext<TKey, TValue> : IPersistentMapStorageContext<TKey, TValue>
        where TKey : IMessage 
        where TValue : IMessage
    {
        private readonly IPersistentMapRepository<TKey, TValue> _repository;
        
        public PersistentMapStorageContext(IPersistentMapRepository<TKey, TValue> repository)
        {
            _repository = repository;
        }

        public PersistentTreeMapNode<TKey, TValue> GetNodeById(IPersistentTreeMap id)
        {
            return _repository.GetNode(id);
        }

        public IPersistentTreeMap PersistNode(IPersistentTreeMap id, PersistentTreeMapNode<TKey, TValue> value)
        {
            _repository.WriteNode(id, value);
            return id;
        }
    }
}