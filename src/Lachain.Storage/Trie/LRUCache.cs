using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Lachain.Storage.Trie
{
    internal class LRUCache
    {
        private int Capacity;
        private Dictionary<ulong, LinkedListNode<LRUCacheItem>> cacheMap = new Dictionary<ulong, LinkedListNode<LRUCacheItem>>();
        private LinkedList<LRUCacheItem> _lruList = new LinkedList<LRUCacheItem>();

        public LRUCache(int capacity)
        {
            if (capacity == 0)
            {
                throw new ArgumentException("capacity can't be 0");
            }
            Capacity = capacity;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public IHashTrieNode? Get(ulong key)
        {
            if (cacheMap.TryGetValue(key, out var node))
            {
                IHashTrieNode value = node.Value.Value;
                _lruList.Remove(node);
                _lruList.AddLast(node);
                return value;
            }
            return null;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Add(ulong key, IHashTrieNode value)
        {
            if(cacheMap.TryGetValue(key, out var oldNode)) 
            {
                _lruList.Remove(oldNode);
                cacheMap.Remove(key); 
            }
            if (cacheMap.Count >= Capacity)
            {
                RemoveFirst();
            }
            LRUCacheItem cacheItem = new LRUCacheItem(key, value);
            LinkedListNode<LRUCacheItem> node = new LinkedListNode<LRUCacheItem>(cacheItem);
            _lruList.AddLast(node);
            cacheMap.Add(key, node);
        }

        private void RemoveFirst()
        {
            LinkedListNode<LRUCacheItem> node = _lruList.First;
            _lruList.RemoveFirst();
            cacheMap.Remove(node.Value.Key);
        }
    }

    class LRUCacheItem
    {
        public ulong Key;
        public IHashTrieNode Value;
        public LRUCacheItem(ulong key, IHashTrieNode value)
        {
            Key = key;
            Value = value;
        }
    }
}