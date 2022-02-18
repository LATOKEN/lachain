using Lachain.Proto;
using System.Collections.Generic;


namespace Lachain.Utility
{
    public class EventObject
    {
        public Event? Event { get; set; } = null; 
        public List<UInt256>? Topics { get; set; } = null;

        public EventObject(Event? ev){
            Event = ev;
        }

        public EventObject(Event? ev, List<UInt256>? topics){
            Event = ev;
            Topics = topics;
        }

    }
}