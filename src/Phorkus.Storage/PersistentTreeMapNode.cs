using System;

namespace Phorkus.Storage
{
    [Serializable]
    public class PersistentTreeMapNode<TIDentifier, TKey, TValue>
    {
        public readonly TIDentifier LeftSon;
        public readonly TIDentifier RightSon;
        public readonly TKey Key;
        public readonly TValue Value;

        public PersistentTreeMapNode(TIDentifier leftSon, TIDentifier rightSon, TKey key, TValue value)
        {
            LeftSon = leftSon;
            RightSon = rightSon;
            Key = key;
            Value = value;
        }
    }
}