using System;
using Google.Protobuf;
using Org.BouncyCastle.Math;
using Phorkus.Hermes.Config;
using Phorkus.Hermes.Math;
using Phorkus.Proto;

namespace Phorkus.Hermes.Generator
{
    public static class DerivationGenerator
    {
        public static KeysDerivationPrivateParameters GeneratePrivate(GenesisPrivateParams protocolParameters, int i,
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

        public static KeysDerivationPublicParams GeneratePublic(int j, KeysDerivationPrivateParameters keysDerivationPrivateParameters)
        {
            BigInteger Betaij = keysDerivationPrivateParameters.betaiSharing.eval(j);
            BigInteger DRij = keysDerivationPrivateParameters.DRiSharing.eval(j);
            BigInteger Phiij = keysDerivationPrivateParameters.PhiSharing.eval(j);
            BigInteger hij = keysDerivationPrivateParameters.zeroSharing.eval(j);
            return new KeysDerivationPublicParams
            {
                I = keysDerivationPrivateParameters.i,
                J = j,
                Betaij = ByteString.CopyFrom(Betaij.ToByteArray()),
                Drij = ByteString.CopyFrom(DRij.ToByteArray()),
                Phiij = ByteString.CopyFrom(Phiij.ToByteArray()),
                Hij = ByteString.CopyFrom(hij.ToByteArray())
            };
        }
    }
}