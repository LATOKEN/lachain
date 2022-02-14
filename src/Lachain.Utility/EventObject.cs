using Lachain.Proto;
using System.Collections.Generic;


namespace Lachain.Utility
{
    public class EventObject
    {
        public Event? _event = null; // = new Event();
        public List<UInt256>? _topics = null;// = new List<UInt256>();

        public EventObject(Event? ev){
            _event = ev;
        }

        public EventObject(Event? ev, List<UInt256>? topics){
            _event = ev;
            _topics = topics;
        }

    }
}