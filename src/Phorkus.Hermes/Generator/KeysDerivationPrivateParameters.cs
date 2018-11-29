using Phorkus.Hermes.Math;

namespace Phorkus.Hermes.Generator
{
    public class KeysDerivationPrivateParameters
    {
        public int i;
        public Polynomial DRiSharing;
        public PolynomialMod betaiSharing;
        public PolynomialMod PhiSharing;
        public PolynomialMod zeroSharing;
        
        internal KeysDerivationPrivateParameters(int i, PolynomialMod betaiSharing, PolynomialMod PhiSharing, PolynomialMod zeroSharing, Polynomial DRiSharing)
        {
            this.i = i;
            this.betaiSharing = betaiSharing;
            this.DRiSharing = DRiSharing;
            this.PhiSharing = PhiSharing;
            this.zeroSharing = zeroSharing;
        }
    }
}