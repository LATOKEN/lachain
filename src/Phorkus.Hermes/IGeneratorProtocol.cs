using System.Collections.Generic;
using Phorkus.Hermes.Crypto.Key;
using Phorkus.Hermes.Generator;
using Phorkus.Hermes.Generator.Messages;
using Phorkus.Hermes.Generator.State;

namespace Phorkus.Hermes
{
    public interface IGeneratorProtocol
    {
        GeneratorState CurrentState { get; }

        void Initialize();

        IReadOnlyCollection<BgwPublicParams> GenerateShare();
        
        BGWNPoint GeneratePoint(IReadOnlyCollection<BgwPublicParams> shares);

        QiTestForRound GenerateProof(IReadOnlyCollection<BGWNPoint> points);

        BiprimalityTestResult ValidateProof(IReadOnlyCollection<QiTestForRound> proofs);

        IReadOnlyCollection<KeysDerivationPublicParameters> GenerateDerivation(BiprimalityTestResult acceptedN);

        ThetaPoint GenerateTheta(IReadOnlyCollection<KeysDerivationPublicParameters> derivations);
        
        VerificationKey GenerateVerification(IReadOnlyCollection<ThetaPoint> thetas);
        
        PaillierPrivateThresholdKey Finalize(IReadOnlyCollection<VerificationKey> verificationKeys);
    }
}