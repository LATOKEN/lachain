namespace Phorkus.Hestia.PersistentHashTrie
{
    public interface IHashTrieNode
    {
        NodeType Type { get; }
        ulong GetChildByHash(byte h);
    }
}