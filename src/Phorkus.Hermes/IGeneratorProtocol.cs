using System.Collections.Generic;
using Phorkus.Hermes.Crypto.Key;
using Phorkus.Hermes.Generator;
using Phorkus.Hermes.Generator.Messages;
using Phorkus.Hermes.Generator.State;
using Phorkus.Proto;

namespace Phorkus.Hermes
{
    public interface IGeneratorProtocol
    {
        GeneratorState CurrentState { get; }

        void Initialize(byte[] seed);

        IDictionary<PublicKey, BgwPublicParams> GenerateShare();
        
        BGWNPoint GeneratePoint(IDictionary<PublicKey, BgwPublicParams> shares);

        QiTestForRound GenerateProof(IDictionary<PublicKey, BGWNPoint> points);

        BiprimalityTestResult ValidateProof(IDictionary<PublicKey, QiTestForRound> proofs);

        IReadOnlyCollection<KeysDerivationPublicParameters> GenerateDerivation(BiprimalityTestResult acceptedN);

        ThetaPoint GenerateTheta(IReadOnlyCollection<KeysDerivationPublicParameters> derivations);
        
        VerificationKey GenerateVerification(IReadOnlyCollection<ThetaPoint> thetas);
        
        PaillierPrivateThresholdKey Finalize(IReadOnlyCollection<VerificationKey> verificationKeys);
    }
}