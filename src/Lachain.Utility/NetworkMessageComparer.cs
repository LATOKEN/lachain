using System.Collections.Generic;
using Lachain.Proto;

namespace Lachain.Utility
{
    public class NetworkMessageComparer : IComparer<(NetworkMessagePriority, NetworkMessage)>
    {
        public int Compare((NetworkMessagePriority, NetworkMessage) x, (NetworkMessagePriority, NetworkMessage) y)
        {
            return ((byte) x.Item1).CompareTo((byte) y.Item1);
        }
    }
}