using Phorkus.Core.Utils;
using Phorkus.Proto;
using Phorkus.Storage.Treap;

namespace Phorkus.Storage.Mappings
{
    public class BlockMapManager : PersistentTreeMapManager<UInt256, Block, UInt256Comparer>
    {
        public BlockMapManager(IPersistentMapStorageContext<UInt256, Block> context, UInt256Comparer comparator)
            : base(context, comparator)
        {
        }
    }
}