using System.Collections.Generic;
using Phorkus.Proto;

namespace Phorkus.Utility
{
    public class PublicKeyComparer : IComparer<PublicKey>
    {
        public int Compare(PublicKey x, PublicKey y)
        {
            if (x == null) return y == null ? 0 : -1;
            if (y == null) return 1;
            for (var i = 0; i < x.Buffer.Length; ++i)
            {
                if (x.Buffer[i] != y.Buffer[i]) return x.Buffer[i] - y.Buffer[i];
            }
            return 0;
        }
    }
}