using Phorkus.Core.Proto;
using Phorkus.Core.Utils;

namespace Phorkus.Core.Blockchain
{
    public class HashedBlockHeader
    {
        public BlockHeader BlockHeader { get; }

        public UInt256 Hash {
            get
            {
                if (_hash != null)
                    return _hash;
                _hash = BlockHeader?.ToHash256();
                return _hash;
            }
        }

        public HashedBlockHeader(BlockHeader blockHeader)
        {
            BlockHeader = blockHeader;
        }

        private UInt256 _hash;
    }
}