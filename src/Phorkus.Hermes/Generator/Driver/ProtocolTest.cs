using System.Collections.Generic;
using Phorkus.Hermes.Generator.State;
using Phorkus.Proto;

namespace Phorkus.Hermes.Generator.Driver
{
    public class ProtocolTest
    {
        public static int N_PARTIES = 10; // Current implementation: works for 3 to 30 
        public static int T_THRESHOLD = 4; // Should be less than n/2
        public static int KEY_SIZE = 128; // Tested up to 512

        ProtocolTest()
        {
//            var participants = new ProtocolData();
//            var defaultGeneratorProtocol = new DefaultGeneratorProtocol(participants);
        }
    }
}