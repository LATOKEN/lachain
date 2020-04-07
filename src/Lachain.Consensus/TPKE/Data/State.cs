using System.Collections;
using System.Linq;
using Lachain.Crypto.MCL.BLS12_381;

namespace Lachain.Consensus.TPKE.Data
{
    public class State
    {
        public Commitment? Commitment { get; set; }
        public readonly Fr[] Values;
        public readonly BitArray Acks;

        public State(int n)
        {
            Values = new Fr[n];
            Acks = new BitArray(n);
        }

        public int ValueCount()
        {
            return Acks.Cast<int>().Sum();
        }
    }
}