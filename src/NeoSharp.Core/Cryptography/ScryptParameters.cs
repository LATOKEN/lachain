namespace NeoSharp.Core.Cryptography
{
    public class ScryptParameters
    {
        public static ScryptParameters Default { get; } = new ScryptParameters(16384, 8, 8);

        /// <summary>
        /// n is a parameter that defines the CPU/memory cost.
        /// Must be a value 2^N.
        /// </summary>
        public int N { get; }

        /// <summary>
        /// r is a tuning parameter.
        /// </summary>
        public int R { get; }

        /// <summary>
        /// p is a tuning parameter (parallelization parameter). A large value
        /// of p can increase computational cost of SCrypt without increasing
        /// the memory usage.
        /// </summary>
        public int P { get; }

        public ScryptParameters(int n, int r, int p)
        {
            N = n;
            R = r;
            P = p;
        }
    }
}
