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

        void CollectShare(IReadOnlyCollection<BgwPublicParams> shares);

        BGWNPoint GeneratePoint();

        void CollectPoint(IReadOnlyCollection<BGWNPoint> points);

        QiTestForRound GenerateProof();

        void CollectProof(IReadOnlyCollection<QiTestForRound> proofs);

        BiprimalityTestResult ValidateProof();

        IReadOnlyCollection<KeysDerivationPublicParameters> GenerateDerivation(BiprimalityTestResult acceptedN);

        void CollectDerivation(IReadOnlyCollection<KeysDerivationPublicParameters> derivations);

        ThetaPoint GenerateTheta();

        void CollectTheta(IReadOnlyCollection<ThetaPoint> thetas);

        VerificationKey GenerateVerification();

        void CollectVerification(IReadOnlyCollection<VerificationKey> verificationKeys);
        
        PaillierPrivateThresholdKey Finalize();
    }
}