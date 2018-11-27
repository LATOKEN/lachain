using Phorkus.Storage.Treap;

namespace Phorkus.Storage.Mappings
{
    public class BlockMapFactory : IPersistentTreeMapFactory
    {
        private ulong _curId;

        public BlockMapFactory(ulong start)
        {
            _curId = start;
        }
        
        public IPersistentTreeMap NewVersionId()
        {
            return new BlockMap(++_curId);
        }

        public IPersistentTreeMap NullIdentifier { get; } = new BlockMap(0);
    }
}