namespace Phorkus.Storage.Treap
{
    public interface IPersistentTreeMapFactory
    {
        IPersistentTreeMap NewVersionId();
        IPersistentTreeMap NullIdentifier { get; }
    }
}