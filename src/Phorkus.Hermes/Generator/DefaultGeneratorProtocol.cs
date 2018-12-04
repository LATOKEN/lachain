using System;
using System.Collections.Generic;
using System.Linq;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;
using Phorkus.Hermes.Crypto.Key;
using Phorkus.Hermes.Generator.Messages;
using Phorkus.Hermes.Generator.State;
using Phorkus.Hermes.Math;
using Phorkus.Proto;

namespace Phorkus.Hermes.Generator
{
    public class DefaultGeneratorProtocol : IGeneratorProtocol
    {
        public static int N_PARTIES = 10; // Current implementation: works for 3 to 30 
        public static int T_THRESHOLD = 4; // Should be less than n/2
        public static int KEY_SIZE = 128; // Tested up to 512
        public static int NUMBER_OF_ROUNDS = 10;

        private readonly SecureRandom sr = new SecureRandom();

        private IReadOnlyDictionary<PublicKey, int> participants;
        private PublicKey publicKey;
        private ProtocolParameters protoParam;
        private BGWData bgwData;
        private BiprimalityTestData biprimalityTestData;
        private KeysDerivationData keysDerivationData;
        private CandidateN candidateN;

        public DefaultGeneratorProtocol(IReadOnlyDictionary<PublicKey, int> participants, PublicKey publicKey)
        {
            this.participants = participants;
            this.publicKey = publicKey;
        }

        public GeneratorState CurrentState { get; private set; }

        public void Initialize()
        {
            CurrentState = GeneratorState.Initialization;

            protoParam = ProtocolParameters.gen(KEY_SIZE, N_PARTIES, T_THRESHOLD, new SecureRandom());
            Console.WriteLine("Pp=" + protoParam.P);
        }

        public IReadOnlyCollection<BgwPublicParams> GenerateShare()
        {
            CurrentState = GeneratorState.GeneratingShare;

            // Generates new p, q and all necessary sharings
            var bgwPrivateParameters = BgwPrivateParams.genFor(participants[publicKey], protoParam, sr);
            var bgwSelfShare = BgwPublicParams.genFor(participants[publicKey], bgwPrivateParameters);

            bgwData = BGWData.Init()
                .WithPrivateParameters(bgwPrivateParameters)
                .WithNewShare(bgwSelfShare, participants[publicKey])
                .WithParticipants(participants);

            var shares = participants.Select(p => BgwPublicParams.genFor(p.Value, bgwData.bgwPrivateParameters))
                .ToList();
            return shares;
        }

        public void CollectShare(IReadOnlyCollection<BgwPublicParams> shares)
        {
            CurrentState = GeneratorState.CollectingShare;

            for (var i = 0; i < shares.Count; i++)
                bgwData = bgwData.WithNewShare(shares.ElementAt(i), i);
            if (!bgwData.HasShareOf(participants.Values))
                throw new Exception("We havn't collected all required shares from other participants");

            var badActors = bgwData.shares()
                .Where(e => !e.Value.isCorrect(protoParam))
                .Select(e => e.Key); // Check the shares (not implemented yet)
            if (badActors.Any())
                throw new Exception("There are bad actors that sent incorrect share");
        }

        public BGWNPoint GeneratePoint()
        {
            CurrentState = GeneratorState.GeneratingPoint;

            var sumPj = bgwData.shares().Select(e => e.Value.pij)
                .Aggregate(BigInteger.Zero, (p1, p2) => p1.Add(p2));
            var sumQj = bgwData.shares().Select(e => e.Value.qij)
                .Aggregate(BigInteger.Zero, (q1, q2) => q1.Add(q2));
            var sumHj = bgwData.shares().Select(e => e.Value.hij)
                .Aggregate(BigInteger.Zero, (h1, h2) => h1.Add(h2));
            var Ni = sumPj.Multiply(sumQj).Add(sumHj).Mod(protoParam.P);

            bgwData = bgwData.withNewNi(Ni, participants[publicKey]);
            return new BGWNPoint(bgwData.Ns[participants[publicKey]]);
        }

        public void CollectPoint(IReadOnlyCollection<BGWNPoint> points)
        {
            CurrentState = GeneratorState.CollectingPoint;

            for (var i = 0; i < points.Count; i++)
                bgwData = bgwData.withNewNi(points.ElementAt(i).point, i);
            if (!bgwData.hasNiOf(participants.Values))
                throw new Exception("We havn't collected all required points from other participants");

            var Nis = bgwData.nis().Select(e => e.Value).ToList();
            var N = IntegersUtils.GetIntercept(Nis, protoParam.P);

            candidateN = new CandidateN(N, bgwData.bgwPrivateParameters);
        }

        public QiTestForRound GenerateProof()
        {
            CurrentState = GeneratorState.GeneratingProof;

            if (biprimalityTestData is null)
                biprimalityTestData = BiprimalityTestData.init();
            else
                biprimalityTestData = biprimalityTestData.forNextRound();

            var gp = getGp(candidateN.N, 0);
            var Qi = getQi(gp, candidateN.N, candidateN.BgwPrivateParameters.pi,
                candidateN.BgwPrivateParameters.qi, participants[publicKey]);

            return new QiTestForRound(
                biprimalityTestData.qiss(biprimalityTestData.round)[biprimalityTestData.GetParticipants()[publicKey]],
                biprimalityTestData.round);
        }

        public void CollectProof(IReadOnlyCollection<QiTestForRound> proofs)
        {
            CurrentState = GeneratorState.CollectingProof;

            for (var i = 0; i < proofs.Count; i++)
                biprimalityTestData =
                    biprimalityTestData.withNewQi(proofs.ElementAt(i).Qi, i, proofs.ElementAt(i).round);
            if (!biprimalityTestData.hasQiOf(participants.Values, biprimalityTestData.round))
                throw new Exception("We havn't collected all proofs from other participants");
        }

        public BiprimalityTestResult ValidateProof()
        {
            CurrentState = GeneratorState.ValidatingProof;

            var check = biprimalityTestData.qis(biprimalityTestData.round).Select(
                    qi => qi.Key == 1 ? qi.Value : qi.Value.ModInverse(biprimalityTestData.N))
                .Aggregate(BigInteger.One, (qi, qj) => qi.Multiply(qj)).Mod(biprimalityTestData.N);

            BigInteger minusOne = BigInteger.One.Negate().Mod(biprimalityTestData.N);

            if (check.Equals(minusOne) || check.Equals(BigInteger.One))
            {
                if (participants[publicKey] == 1)
                    Console.WriteLine("PASSED TEST " + biprimalityTestData.round);

                if (biprimalityTestData.round == NUMBER_OF_ROUNDS)
                {
                    return new BiprimalityTestResult(biprimalityTestData.N, biprimalityTestData.bgwPrivateParameters,
                        true);
                }

                // Resets N, BgwPrivateParameters, gprime, Qi and increments round counter for next round
                BiprimalityTestData nextData = biprimalityTestData.forNextRound();

                BigInteger nextgp = getGp(nextData.N, nextData.round);
                BigInteger nextQi = getQi(nextgp, nextData.N, nextData.bgwPrivateParameters.pi,
                    nextData.bgwPrivateParameters.qi, participants[publicKey]);

                biprimalityTestData = nextData.withNewQi(nextQi, participants[publicKey], nextData.round);
            }

            return new BiprimalityTestResult(biprimalityTestData.N, biprimalityTestData.bgwPrivateParameters, false);
        }

        public IReadOnlyCollection<KeysDerivationPublicParameters> GenerateDerivation(BiprimalityTestResult acceptedN)
        {
            keysDerivationData = KeysDerivationData.init();

            var self = participants[publicKey];

            BigInteger N = acceptedN.N;
            BigInteger pi = acceptedN.BgwPrivateParameters.pi;
            BigInteger qi = acceptedN.BgwPrivateParameters.qi;

            var Phii = self == 1
                ? N.Subtract(pi).Subtract(qi).Add(BigInteger.One)
                : pi.Negate().Subtract(qi);

            var keysDerivationPrivateParameters = KeysDerivationPrivateParameters.gen(protoParam, self, N, Phii, sr);
            var keysDerivationPublicParameters =
                KeysDerivationPublicParameters.genFor(self, keysDerivationPrivateParameters);

            keysDerivationData = keysDerivationData.withN(N)
                .withPrivateParameters(keysDerivationPrivateParameters)
                .withNewPublicParametersFor(self, keysDerivationPublicParameters);

            var privateParameters =
                keysDerivationData.keysDerivationPrivateParameters;

            return participants.Values.Select(p => KeysDerivationPublicParameters.genFor(p, privateParameters))
                .ToList();
        }

        public void CollectDerivation(IReadOnlyCollection<KeysDerivationPublicParameters> derivations)
        {
            for (var i = 0; i < derivations.Count; i++)
                keysDerivationData = keysDerivationData.withNewPublicParametersFor(i, derivations.ElementAt(i));
            if (!keysDerivationData.hasBetaiRiOf(participants.Values))
                throw new Exception("We havn't collected all required derivations from other participants");
        }

        public ThetaPoint GenerateTheta()
        {
            var publicParameters = keysDerivationData.publicParameters();

            var betaPointi = publicParameters.Select(e => e.Value.betaij)
                .Aggregate(BigInteger.Zero, (b1, b2) => b1.Add(b2));
            publicParameters = keysDerivationData.publicParameters();
            var DRPointi = publicParameters.Select(e => e.Value.DRij)
                .Aggregate(BigInteger.Zero, (b1, b2) => b1.Add(b2));
            publicParameters = keysDerivationData.publicParameters();
            var PhiPointi = publicParameters.Select(e => e.Value.Phiij)
                .Aggregate(BigInteger.Zero, (b1, b2) => b1.Add(b2));
            publicParameters = keysDerivationData.publicParameters();
            var hij = publicParameters.Select(e => e.Value.hij).Aggregate(BigInteger.Zero, (b1, b2) => b1.Add(b2));
            var delta = IntegersUtils.Factorial(BigInteger.ValueOf(protoParam.n));
            var thetai = delta.Multiply(PhiPointi).Multiply(betaPointi).Mod(protoParam.P)
                .Add(keysDerivationData.N.Multiply(DRPointi).Mod(protoParam.P)).Add(hij).Mod(protoParam.P);

            keysDerivationData = keysDerivationData.withNewThetaFor(participants[publicKey], thetai)
                .withRPoint(DRPointi);

            return new ThetaPoint(thetai);
        }

        public void CollectTheta(IReadOnlyCollection<ThetaPoint> thetas)
        {
            for (var i = 0; i < thetas.Count; i++)
                keysDerivationData = keysDerivationData.withNewThetaFor(i, thetas.ElementAt(i).thetai);
            if (!keysDerivationData.hasThetaiOf(participants.Values))
                throw new Exception("We havn't collected all required thetas from other participants");

            throw new NotImplementedException();
        }

        public VerificationKey GenerateVerification()
        {
            List<BigInteger> thetas = keysDerivationData.thetas.Values.ToList();
            BigInteger thetap = IntegersUtils.GetIntercept(thetas, protoParam.P);
            BigInteger theta = thetap.Mod(keysDerivationData.N);

            // Parties should have the same v, using theta to seed the random generator
            BigInteger v = IntegersUtils.PickProbableGeneratorOfZnSquare(keysDerivationData.N, 2 * protoParam.k,
                new SecureRandom(theta.ToByteArray())); /* TODO: "is it ok?" */

            BigInteger secreti = thetap.Subtract(keysDerivationData.N.Multiply(keysDerivationData.DRpoint));
            BigInteger delta = IntegersUtils.Factorial(BigInteger.ValueOf(protoParam.n));

            BigInteger verificationKeyi =
                v.ModPow(delta.Multiply(secreti), keysDerivationData.N.Multiply(keysDerivationData.N));

            keysDerivationData = keysDerivationData
                .withNewVerificationKeyFor(participants[publicKey], verificationKeyi)
                .withNewV(v)
                .withFi(secreti)
                .withThetaprime(thetap);

            return new VerificationKey(keysDerivationData.verificationKeys[participants[publicKey]]);
        }

        public void CollectVerification(IReadOnlyCollection<VerificationKey> verificationKeys)
        {
            for (var i = 0; i < verificationKeys.Count; i++)
                keysDerivationData =
                    keysDerivationData.withNewVerificationKeyFor(i, verificationKeys.ElementAt(i).verificationKey);
            if (!keysDerivationData.hasVerifKeyOf(participants.Values))
                throw new Exception("We havn't collected all required verification keys from other participants");
        }

        public PaillierPrivateThresholdKey Finalize()
        {
            var verificationKeys = new BigInteger[protoParam.n];
            foreach (var entry in keysDerivationData.verificationKeys)
                verificationKeys[entry.Key - 1] = entry.Value;
            var privateKey = new PaillierPrivateThresholdKey(keysDerivationData.N,
                keysDerivationData.thetaprime,
                protoParam.n,
                protoParam.t + 1,
                keysDerivationData.v,
                verificationKeys,
                keysDerivationData.fi,
                participants[publicKey],
                sr.NextInt());
            return privateKey;
        }

        private BigInteger getGp(BigInteger N, int round)
        {
            int hash = N.GetHashCode() * (round + 1);

            BigInteger candidateGp = BigInteger.ValueOf(hash).Abs();

            while (IntegersUtils.Jacobi(candidateGp, N) != 1)
                candidateGp = candidateGp.Add(BigInteger.One);

            return candidateGp;
        }

        private BigInteger getQi(BigInteger gp, BigInteger N, BigInteger pi, BigInteger qi, int i)
        {
            BigInteger four = BigInteger.ValueOf(4);
            BigInteger exp;
            if (i == 1)
            {
                exp = N.Add(BigInteger.One).Subtract(pi).Subtract(qi).Divide(four);
            }
            else
            {
                exp = pi.Add(qi).Divide(four);
            }

            return gp.ModPow(exp, N);
        }
    }
}