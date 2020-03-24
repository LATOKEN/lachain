using System;

namespace Lachain.Consensus
{
    public class ConsensusException : Exception
    {
        public ConsensusException(string message) : base(message)
        {
            
        }
    }
}