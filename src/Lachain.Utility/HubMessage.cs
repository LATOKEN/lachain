using System;
using Lachain.Proto;
using Lachain.Utility.Utils;

namespace Lachain.Utility
{
    public class HubMessage : IComparable<HubMessage>
    {
        public NetworkMessagePriority Priority { get; }
        public NetworkMessage Msg { get; }
        public ulong CreationTime { get; }
        public HubMessage(NetworkMessagePriority priority, NetworkMessage msg, ulong creationTime)
        {
            Priority = priority;
            Msg = msg;
            CreationTime = creationTime;
        }

        public HubMessage(NetworkMessagePriority priority, NetworkMessage msg)
        {
            Priority = priority;
            Msg = msg;
            CreationTime = TimeUtils.CurrentTimeMillis();
        }

        public int CompareTo(HubMessage? other)
        {
            if (other is null) return 1;
            if (Priority == other.Priority)
                return CreationTime.CompareTo(other.CreationTime);
            return Priority.CompareTo(other.Priority);
        }
    }
}