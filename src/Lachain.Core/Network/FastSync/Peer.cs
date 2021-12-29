using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lachain.Core.Network.FastSync
{
    class Peer
    {
        public string _url; 
        public Peer(string url)
        {
            _url = url;
        }
    }
}
