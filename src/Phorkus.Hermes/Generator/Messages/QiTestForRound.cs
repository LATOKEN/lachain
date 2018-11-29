using Org.BouncyCastle.Math;

namespace Phorkus.Hermes.Generator.Messages
{
    public class QiTestForRound
    {
        /** A Qi in the Biprimality test*/
        public BigInteger Qi;

        /** The current round number in the Biprimality test*/
        public int round;

        public QiTestForRound(BigInteger Qi, int round)
        {
            this.Qi = Qi;
            this.round = round;
        }
    }
}