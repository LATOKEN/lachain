using System;
using Google.Protobuf;
using Org.BouncyCastle.Math;
using Phorkus.Hermes.Generator.State;
using Phorkus.Hermes.Math;
using Phorkus.Proto;

namespace Phorkus.Hermes.Generator.Builders
{
    public static class DerivationBuilder
    {
        public static KeysDerivationPublicParameters GeneratePublic(int j, KeysDerivationPrivateParameters keysDerivationPrivateParameters)
        {
            BigInteger Betaij = keysDerivationPrivateParameters.betaiSharing.eval(j);
            BigInteger DRij = keysDerivationPrivateParameters.DRiSharing.eval(j);
            BigInteger Phiij = keysDerivationPrivateParameters.PhiSharing.eval(j);
            BigInteger hij = keysDerivationPrivateParameters.zeroSharing.eval(j);
            return new KeysDerivationPublicParameters( keysDerivationPrivateParameters.i,  j,  Betaij,  DRij,  Phiij,  hij);
        }
        
    }
}