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

        QiTestForRound ValidateProof(IDictionary<PublicKey, QiTestForRound> proofs, out BiprimalityTestResult biprimalityTestResult);

        IDictionary<PublicKey, KeysDerivationPublicParameters> GenerateDerivation(BiprimalityTestResult acceptedN);

        ThetaPoint GenerateTheta(IDictionary<PublicKey, KeysDerivationPublicParameters> derivations);
        
        VerificationKey GenerateVerification(IDictionary<PublicKey, ThetaPoint> thetas);
        
        PaillierPrivateThresholdKey Finalize(IDictionary<PublicKey, VerificationKey> verificationKeys);
    }
}