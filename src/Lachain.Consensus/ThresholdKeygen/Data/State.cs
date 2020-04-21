using System;
using System.Linq;
using Lachain.Crypto.MCL.BLS12_381;
using Lachain.Utility.Utils;
using NLog.Targets;

namespace Lachain.Consensus.ThresholdKeygen.Data
{
    public class State
    {
        public Commitment? Commitment { get; set; }
        public readonly Fr[] Values;
        public readonly bool[] Acks;

        public State(int n)
        {
            Values = new Fr[n];
            Acks = new bool[n];
        }

        public int ValueCount()
        {
            return Acks.Select(x => x ? 1 : 0).Sum();
        }

        public Fr InterpolateValues()
        {
            if (Commitment is null) throw new ArgumentException("Cannot interpolate without commitment");
            var xs = Acks.WithIndex()
                .Where(x => x.item)
                .Select(x => x.index + 1)
                .Select(Fr.FromInt)
                .Take(Commitment.Degree + 1)
                .ToArray();
            var ys = Acks.WithIndex()
                .Where(x => x.item)
                .Select(x => Values[x.index])
                .Take(Commitment.Degree + 1)
                .ToArray();
            if (xs.Length != Commitment.Degree + 1 || ys.Length != Commitment.Degree + 1)
                throw new Exception("Cannot interpolate values");
            return Mcl.LagrangeInterpolateFr(xs, ys);
        }
    }
}