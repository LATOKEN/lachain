using Phorkus.Storage.Treap;

namespace Phorkus.Storage.Mappings
{
    public class BlockMap : IPersistentTreeMap
    {
        public BlockMap(ulong id)
        {
            Id = id;
        }

        public ulong Id { get; }

        public bool Equals(BlockMap other)
        {
            return Id == other.Id;
        }

        public bool Equals(IPersistentTreeMap other)
        {
            if (!(other is BlockMap)) return false;
            return Equals((BlockMap) other);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((BlockMap) obj);
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }
    }
}