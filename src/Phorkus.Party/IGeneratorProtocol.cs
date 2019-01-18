using System.Collections.Generic;
using Phorkus.Party.Crypto.Key;
using Phorkus.Party.Generator;
using Phorkus.Party.Generator.Messages;
using Phorkus.Party.Generator.State;
using Phorkus.Proto;

namespace Phorkus.Party
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