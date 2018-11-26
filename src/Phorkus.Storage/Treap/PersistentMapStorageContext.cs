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
        private readonly IPersistentTreeMapFactory _factory;

        public PersistentMapStorageContext(IPersistentMapRepository<TKey, TValue> repository, IPersistentTreeMapFactory factory)
        {
            _repository = repository;
            _factory = factory;
        }

        public IPersistentTreeMap NullIDentifier => _factory.NullIdentifier;

        public PersistentTreeMapNode<TKey, TValue> GetNodeById(IPersistentTreeMap id)
        {
            return _repository.GetNode(id);
        }

        public IPersistentTreeMap NewNode(IPersistentTreeMap leftSon, IPersistentTreeMap rightSon, TKey key, TValue value)
        {
            var newId = _factory.NewVersionId();
            _repository.WriteNode(
                newId,
                new PersistentTreeMapNode<TKey, TValue>(leftSon, rightSon, key, value)
            );
            return newId;
        }
    }
}