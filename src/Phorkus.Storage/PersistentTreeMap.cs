using System;

namespace Phorkus.Storage
{
    [Serializable]
    public class PersistentTreeMap : IEquatable<PersistentTreeMap>
    {
        public PersistentTreeMap(ulong id)
        {
            Id = id;
        }

        public ulong Id { get; }

        public bool Equals(PersistentTreeMap other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Id == other.Id;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((PersistentTreeMap) obj);
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }
    }
}