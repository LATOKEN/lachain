using System;

namespace Phorkus.Storage.Treap
{
    public interface IPersistentTreeMap : IEquatable<IPersistentTreeMap>
    {
        ulong Id { get; }
    }
}