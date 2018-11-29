using Org.BouncyCastle.Math;

namespace Phorkus.Hermes.Generator.Messages
{
    public class ThetaPoint
    {
        /**a share of Theta*/
        public BigInteger thetai;

        public ThetaPoint(BigInteger thetai)
        {
            this.thetai = thetai;
        }
    }
}