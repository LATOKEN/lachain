using System.Collections.Generic;
using Lachain.Proto;

namespace Lachain.Core.Blockchain.Checkpoints
{
    public class CheckpointComparer : IComparer<Checkpoint>
    {
        public int Compare(Checkpoint? x, Checkpoint? y)
        {
            if (x is null)
                return y is null ? 0 : -1;
            if (y is null)
                return 1;
            return x.BlockHeight.CompareTo(y.BlockHeight);
        }
    }
}