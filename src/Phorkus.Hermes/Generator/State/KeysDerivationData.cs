using System.Collections.Generic;
using System.Linq;
using Org.BouncyCastle.Math;
using Phorkus.Proto;

namespace Phorkus.Hermes.Generator.State
{
    public class KeysDerivationData : AbstractData<KeysDerivationData>
    {
        /** The current candidate to RSA modulus*/
        public readonly BigInteger N;

        /** The share of &Delta;R help by the actor*/
        public readonly BigInteger DRpoint;

        /** The generator used for the verification keys*/
        public readonly BigInteger v;

        /** The share of the private key held by the actor*/
        public readonly BigInteger fi;

        /** The statistically hidden &Phi;(N),  &Theta;'*/
        public readonly BigInteger thetaprime;

        /** The key derivation private parameters of this actor ( &Beta;<sub>i</sub>, R<sub>i</sub>,...) */
        public readonly KeysDerivationPrivateParameters keysDerivationPrivateParameters;

        /** Collection of the shares of &Theta;' of all actors*/
        public readonly IDictionary<int, BigInteger> thetas;

        /** Collection of the verification keys of all actors*/
        public readonly IDictionary<int, BigInteger> verificationKeys;

        public readonly IDictionary<int, BgwPublicParams> publicParameters1;
        


        private KeysDerivationData(IReadOnlyDictionary<PublicKey, int> participants,
            BigInteger N,
            BigInteger Rpoint,
            BigInteger v,
            BigInteger fi,
            BigInteger thetaprime,
            KeysDerivationPrivateParameters keysDerivationPrivateParameters,
            IDictionary<int, BgwPublicParams> publicParameters,
            IDictionary<int, BigInteger> thetas,
            IDictionary<int, BigInteger> verificationKeys) : base(participants)
        {
            this.N = N;
            this.DRpoint = Rpoint;
            this.v = v;
            this.fi = fi;
            this.thetaprime = thetaprime;
            this.keysDerivationPrivateParameters = keysDerivationPrivateParameters;
            this.publicParameters1 = publicParameters;
            this.thetas = thetas;
            this.verificationKeys = verificationKeys;
        }

        public bool hasBetaiRiOf(IEnumerable<int> s)
        {
            return s.All(i => publicParameters1.ContainsKey(i));
        }

        public bool hasThetaiOf(IEnumerable<int> s)
        {
            // return s.stream().allMatch(i => this.thetas.ContainsKey(i));
            return s.All(i => thetas.ContainsKey(i));
        }

        public bool hasVerifKeyOf(IEnumerable<int> s)
        {
            // return s.stream().allMatch(i => this.verificationKeys.ContainsKey(i));
            return s.All(i => verificationKeys.ContainsKey(i));
        }
        
        // public Stream<Entry<Integer, KeysDerivationPublicParameters>> publicParameters() {
        //     return this.publicParameters.entrySet().stream();
        // }
        public IEnumerable<KeyValuePair<int, BgwPublicParams>> publicParameters()
        {
            return publicParameters1;
        }

        public override KeysDerivationData WithParticipants(IReadOnlyDictionary<PublicKey, int> participants)
        {
            return new KeysDerivationData(participants, N, DRpoint, v, fi, thetaprime, keysDerivationPrivateParameters,
                publicParameters1, thetas, verificationKeys);
        }

        public KeysDerivationData withN(BigInteger N)
        {
            return new KeysDerivationData(Participants, N, DRpoint, v, fi, thetaprime, keysDerivationPrivateParameters,
                publicParameters1, thetas, verificationKeys);
        }

        public KeysDerivationData withPrivateParameters(KeysDerivationPrivateParameters keysDerivationPrivateParameters)
        {
            return new KeysDerivationData(Participants, N, DRpoint, v, fi, thetaprime, keysDerivationPrivateParameters,
                publicParameters1, thetas, verificationKeys);
        }

        public KeysDerivationData withFi(BigInteger fi)
        {
            return new KeysDerivationData(Participants, N, DRpoint, v, fi, thetaprime, keysDerivationPrivateParameters,
                publicParameters1, thetas, verificationKeys);
        }

        public KeysDerivationData withThetaprime(BigInteger thetaprime)
        {
            return new KeysDerivationData(Participants, N, DRpoint, v, fi, thetaprime, keysDerivationPrivateParameters,
                publicParameters1, thetas, verificationKeys);
        }

        public KeysDerivationData withNewPublicParametersFor(int j,
            BgwPublicParams keysDerivationPublicParameters)
        {
            if (publicParameters1 != null && publicParameters1.ContainsKey(j))
            {
                return this;
            }

            var newBetasMap = publicParameters1;

            newBetasMap.Add(j, keysDerivationPublicParameters);
            return new KeysDerivationData(Participants, N, DRpoint, v, fi, thetaprime, keysDerivationPrivateParameters,
                newBetasMap, thetas, verificationKeys);
        }

        public KeysDerivationData withRPoint(BigInteger RPoint)
        {
            return new KeysDerivationData(Participants, N, RPoint, v, fi, thetaprime, keysDerivationPrivateParameters,
                publicParameters1, thetas, verificationKeys);
        }

        public KeysDerivationData withNewV(BigInteger v)
        {
            return new KeysDerivationData(Participants, N, DRpoint, v, fi, thetaprime, keysDerivationPrivateParameters,
                publicParameters1, thetas, verificationKeys);
        }

        public KeysDerivationData withNewThetaFor(int j, BigInteger theta)
        {
            if (thetas != null && thetas.ContainsKey(j))
            {
                return this;
            }

            IDictionary<int, BigInteger> newThetaMap = thetas != null
                ? new Dictionary<int, BigInteger>(thetas)
                : new Dictionary<int, BigInteger>();
            newThetaMap.Add(j, theta);
            return new KeysDerivationData(Participants, N, DRpoint, v, fi, thetaprime, keysDerivationPrivateParameters,
                publicParameters1, newThetaMap, verificationKeys);
        }

        public KeysDerivationData withNewVerificationKeyFor(int j, BigInteger newVerifKey)
        {
            if (verificationKeys != null && verificationKeys.ContainsKey(j))
            {
                return this;
            }

            IDictionary<int, BigInteger> newVKMap = verificationKeys != null
                ? new Dictionary<int, BigInteger>(verificationKeys)
                : new Dictionary<int, BigInteger>();
            newVKMap.Add(j, newVerifKey);
            return new KeysDerivationData(Participants, N, DRpoint, v, fi, thetaprime, keysDerivationPrivateParameters,
                publicParameters1, thetas, newVKMap);
        }
        
        public static KeysDerivationData init()
        {
            return new KeysDerivationData(null, null, null, null, null, null, null, null, null, null);
        }
    }
}