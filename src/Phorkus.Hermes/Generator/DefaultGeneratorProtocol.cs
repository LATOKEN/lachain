using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;
using Phorkus.Hermes.Crypto.Key;
using Phorkus.Hermes.Generator.Messages;
using Phorkus.Hermes.Generator.State;
using Phorkus.Hermes.Math;
using Phorkus.Proto;
using Phorkus.Utility;

namespace Phorkus.Hermes.Generator
{
    public class DefaultGeneratorProtocol : IGeneratorProtocol
    {
        public static int KEY_SIZE = 128; // Tested up to 512
        public static int NUMBER_OF_ROUNDS = 10;

        private readonly SecureRandom rand = new SecureRandom(
            Encoding.ASCII.GetBytes("Hello World"));

        private IReadOnlyDictionary<PublicKey, int> participants;
        private PublicKey publicKey;
        private static ProtocolParameters protoParam;
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

        public void Initialize(byte[] seed)
        {
            CurrentState = GeneratorState.Initialization;
            if (protoParam is null)
                protoParam = ProtocolParameters.gen(KEY_SIZE, participants.Count, participants.Count / 3, new SecureRandom(seed));
            Console.WriteLine("Pp=" + protoParam.P);
        }

        public IDictionary<PublicKey, BgwPublicParams> GenerateShare()
        {
            CurrentState = GeneratorState.GeneratingShare;

            // Generates new p, q and all necessary sharings
            var bgwPrivateParameters = BgwPrivateParams.genFor(participants[publicKey], protoParam, rand);
            var bgwSelfShare = BgwPublicParams.genFor(participants[publicKey], bgwPrivateParameters);

            bgwData = BGWData.Init(new PublicKeyComparer())
                .WithPrivateParameters(bgwPrivateParameters)
                .WithNewShare(bgwSelfShare, publicKey)
                .WithParticipants(participants);

            var shares = new SortedDictionary<PublicKey, BgwPublicParams>(new PublicKeyComparer());
            foreach (var p in participants)
                shares[p.Key] = BgwPublicParams.genFor(p.Value, bgwData.bgwPrivateParameters);
            return shares;
        }

        public BGWNPoint GeneratePoint(IDictionary<PublicKey, BgwPublicParams> shares)
        {
            CurrentState = GeneratorState.CollectingShare;

            foreach (var s in shares)
                bgwData = bgwData.WithNewShare(s.Value, s.Key);
            
            if (!bgwData.HasShareOf(participants.Keys))
                throw new Exception("We havn't collected all required shares from other participants");

            var badActors = bgwData.shares()
                .Where(e => !e.Value.isCorrect(protoParam))
                .Select(e => e.Key); // Check the shares (not implemented yet)
            if (badActors.Any())
                throw new Exception("There are bad actors that sent incorrect share");
            
            CurrentState = GeneratorState.GeneratingPoint;
            
            var sumPj = bgwData.shares().Select(e => e.Value.pij)
                .Aggregate(BigInteger.Zero, (p1, p2) => p1.Add(p2));
            var sumQj = bgwData.shares().Select(e => e.Value.qij)
                .Aggregate(BigInteger.Zero, (q1, q2) => q1.Add(q2));
            var sumHj = bgwData.shares().Select(e => e.Value.hij)
                .Aggregate(BigInteger.Zero, (h1, h2) => h1.Add(h2));
            var Ni = sumPj.Multiply(sumQj).Add(sumHj).Mod(protoParam.P);

            bgwData = bgwData.withNewNi(Ni, publicKey);
            return new BGWNPoint(bgwData.Ns[publicKey]);
        }

        public QiTestForRound GenerateProof(IDictionary<PublicKey, BGWNPoint> points)
        {
            CurrentState = GeneratorState.CollectingPoint;

            foreach (var p in points)
                bgwData = bgwData.withNewNi(p.Value.point, p.Key);
            if (!bgwData.hasNiOf(participants.Keys))
                throw new Exception("We havn't collected all required points from other participants");

            var Nis = bgwData.nis().Select(e => e.Value).ToList();
            var N = IntegersUtils.GetIntercept(Nis, protoParam.P);

            Console.WriteLine("N= " + N);
//            Console.WriteLine("N = " + N + "\n" + string.Join(" (+) ", Nis));
//            Console.WriteLine(" - - - ");
            
            candidateN = new CandidateN(N, bgwData.bgwPrivateParameters);
            
            CurrentState = GeneratorState.GeneratingProof;

            if (biprimalityTestData is null)
                biprimalityTestData = BiprimalityTestData.init();
            else
                biprimalityTestData = biprimalityTestData.forNextRound();

            var gp = getGp(candidateN.N, 0);
            var Qi = getQi(gp, candidateN.N, candidateN.BgwPrivateParameters.pi,
                candidateN.BgwPrivateParameters.qi, participants[publicKey]);

            biprimalityTestData = biprimalityTestData
                .withNewCandidateN(candidateN.N, candidateN.BgwPrivateParameters)
                .withNewQi(Qi, publicKey, biprimalityTestData.round);
            
            return new QiTestForRound(
                biprimalityTestData.qiss(biprimalityTestData.round)[publicKey],
                biprimalityTestData.round);
        }

        public BiprimalityTestResult ValidateProof(IDictionary<PublicKey, QiTestForRound> proofs)
        {
            CurrentState = GeneratorState.CollectingProof;
            
            foreach (var p in proofs)
                biprimalityTestData = biprimalityTestData.withNewQi(p.Value.Qi, p.Key, p.Value.round);
            if (!biprimalityTestData.hasQiOf(participants.Keys, biprimalityTestData.round))
                throw new Exception("We havn't collected all proofs from other participants");
            
            CurrentState = GeneratorState.ValidatingProof;

            var check = biprimalityTestData.qis(biprimalityTestData.round)
                .Select(qi =>
                {
                    if (participants[qi.Key] == 1)
                        return qi.Value;
                    return qi.Value.ModInverse(biprimalityTestData.N);
                })
                .Aggregate(BigInteger.One, (qi, qj) => qi.Multiply(qj))
                .Mod(biprimalityTestData.N);

            var minusOne = BigInteger.One.Negate().Mod(biprimalityTestData.N);

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
                var nextData = biprimalityTestData.forNextRound();

                var nextgp = getGp(nextData.N, nextData.round);
                var nextQi = getQi(nextgp, nextData.N, nextData.bgwPrivateParameters.pi,
                    nextData.bgwPrivateParameters.qi, participants[publicKey]);

                biprimalityTestData = nextData.withNewQi(nextQi, publicKey, nextData.round);
                return null;
            }
            
            biprimalityTestData = biprimalityTestData.forNextCandidate();
            return new BiprimalityTestResult(biprimalityTestData.N, biprimalityTestData.bgwPrivateParameters, false);
        }

        public IReadOnlyCollection<KeysDerivationPublicParameters> GenerateDerivation(BiprimalityTestResult acceptedN)
        {
            CurrentState = GeneratorState.GeneratingDerivation;
            
            keysDerivationData = KeysDerivationData.init();

            var self = participants[publicKey];

            BigInteger N = acceptedN.N;
            BigInteger pi = acceptedN.BgwPrivateParameters.pi;
            BigInteger qi = acceptedN.BgwPrivateParameters.qi;

            var Phii = self == 1
                ? N.Subtract(pi).Subtract(qi).Add(BigInteger.One)
                : pi.Negate().Subtract(qi);

            var keysDerivationPrivateParameters = KeysDerivationPrivateParameters.gen(protoParam, self, N, Phii, rand);
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

        public ThetaPoint GenerateTheta(IReadOnlyCollection<KeysDerivationPublicParameters> derivations)
        {
            CurrentState = GeneratorState.CollectingDerivation;
            
            for (var i = 0; i < derivations.Count; i++)
                keysDerivationData = keysDerivationData.withNewPublicParametersFor(i + 1, derivations.ElementAt(i));
            if (!keysDerivationData.hasBetaiRiOf(participants.Values))
                throw new Exception("We havn't collected all required derivations from other participants");
            
            CurrentState = GeneratorState.GeneratingTheta;
            
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

        public VerificationKey GenerateVerification(IReadOnlyCollection<ThetaPoint> thetas)
        {
            CurrentState = GeneratorState.CollectingTheta;
            
            for (var i = 0; i < thetas.Count; i++)
                keysDerivationData = keysDerivationData.withNewThetaFor(i + 1, thetas.ElementAt(i).thetai);
            if (!keysDerivationData.hasThetaiOf(participants.Values))
                throw new Exception("We havn't collected all required thetas from other participants");
            
            CurrentState = GeneratorState.GeneratingVerification;
            
            List<BigInteger> ts = keysDerivationData.thetas.Values.ToList();
            BigInteger thetap = IntegersUtils.GetIntercept(ts, protoParam.P);
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

        public PaillierPrivateThresholdKey Finalize(IReadOnlyCollection<VerificationKey> verificationKeys)
        {
            CurrentState = GeneratorState.CollectingVerification;
            
            for (var i = 0; i < verificationKeys.Count; i++)
                keysDerivationData =
                    keysDerivationData.withNewVerificationKeyFor(i + 1, verificationKeys.ElementAt(i).verificationKey);
            if (!keysDerivationData.hasVerifKeyOf(participants.Values))
                throw new Exception("We havn't collected all required verification keys from other participants");
            
            CurrentState = GeneratorState.Finalization;
            
            var keys = new BigInteger[protoParam.n];
            foreach (var entry in keysDerivationData.verificationKeys)
                keys[entry.Key - 1] = entry.Value;
            var privateKey = new PaillierPrivateThresholdKey(keysDerivationData.N,
                keysDerivationData.thetaprime,
                protoParam.n,
                protoParam.t + 1,
                keysDerivationData.v,
                keys,
                keysDerivationData.fi,
                participants[publicKey],
                rand.NextInt());
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