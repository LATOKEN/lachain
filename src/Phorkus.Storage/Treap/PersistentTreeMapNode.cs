using Google.Protobuf;

namespace Phorkus.Storage.Treap
{
    public class PersistentTreeMapNode<TKey, TValue> 
        where TKey : IMessage 
        where TValue : IMessage
    {
        public readonly IPersistentTreeMap LeftSon;
        public readonly IPersistentTreeMap RightSon;
        public readonly TKey Key;
        public readonly TValue Value;

        public PersistentTreeMapNode(IPersistentTreeMap leftSon, IPersistentTreeMap rightSon, TKey key, TValue value)
        {
            LeftSon = leftSon;
            RightSon = rightSon;
            Key = key;
            Value = value;
        }
    }
}