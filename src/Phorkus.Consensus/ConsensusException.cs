using System;

namespace Phorkus.Consensus
{
    public class ConsensusException : Exception
    {
        public ConsensusException(string message) : base(message)
        {
            
        }
    }
}