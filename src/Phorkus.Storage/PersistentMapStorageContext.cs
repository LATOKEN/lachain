using System;
using Google.Protobuf;
using Phorkus.Core.Utils;
using Phorkus.Proto;

namespace Phorkus.Storage
{
    public class PersistentMapStorageContext<TKey, TValue> : IPersistentMapStorageContext<PersistentTreeMap, TKey, TValue>
    {
        private readonly TestRepository<TKey, TValue> _repository;
        private readonly Random _random;

        public PersistentMapStorageContext(TestRepository<TKey, TValue> repository)
        {
            _repository = repository;
            _random = new Random();
        }

        public PersistentTreeMap NullIDentifier => new PersistentTreeMap(0);

        public PersistentTreeMapNode<PersistentTreeMap, TKey, TValue> GetNodeById(PersistentTreeMap id)
        {
            return _repository.GetNode(id);
        }

        public PersistentTreeMap NewNode(PersistentTreeMap leftSon, PersistentTreeMap rightSon, TKey key, TValue value)
        {
            var newId = new PersistentTreeMap((ulong) _random.Next()); 
            _repository.WriteNode(
                newId, 
                new PersistentTreeMapNode<PersistentTreeMap, TKey, TValue>(leftSon, rightSon, key, value)
            );
            return newId;
        }
    }
}