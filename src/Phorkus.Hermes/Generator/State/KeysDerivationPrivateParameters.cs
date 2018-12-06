using System;
using Org.BouncyCastle.Math;
using Phorkus.Hermes.Math;

namespace Phorkus.Hermes.Generator.State
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
        
        public static KeysDerivationPrivateParameters gen(ProtocolParameters protocolParameters, int i,
            BigInteger N, BigInteger Phii, Random rand)
        {
            if (i < 1 || i > protocolParameters.n)
                throw new ArgumentException("i must be between 1 and the number of parties");

            var KN = N.Multiply(BigInteger.ValueOf(protocolParameters.K));
            var K2N = KN.Multiply(BigInteger.ValueOf(protocolParameters.K));

            var betai = IntegersUtils.PickInRange(BigInteger.Zero, KN, rand);
            var delta = IntegersUtils.Factorial(BigInteger.ValueOf(protocolParameters.n));
            var Ri = IntegersUtils.PickInRange(BigInteger.Zero, K2N, rand);

            var betaiSharing = new PolynomialMod(protocolParameters.t, protocolParameters.P, betai,
                protocolParameters.k, rand);
            var PhiiSharing = new PolynomialMod(protocolParameters.t, protocolParameters.P, Phii, protocolParameters.k,
                rand);
            var zeroSharing = new PolynomialMod(protocolParameters.t, protocolParameters.P, BigInteger.Zero,
                protocolParameters.k, rand);
            var DRiSharing = new Polynomial(protocolParameters.t, delta.Multiply(Ri), protocolParameters.k, rand);

            return new KeysDerivationPrivateParameters(i, betaiSharing, PhiiSharing, zeroSharing, DRiSharing);
        }
    }
}