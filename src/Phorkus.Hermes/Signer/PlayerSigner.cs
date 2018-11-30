using System;
using System.Collections.Generic;
using System.Linq;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Math.EC;
using Phorkus.Hermes.Crypto;
using Phorkus.Hermes.Crypto.Key;
using Phorkus.Hermes.Signer.Messages;

namespace Phorkus.Hermes.Signer
{
    public class PlayerSigner
    {
        private Paillier paillierPublicKey;
        private PaillierThreshold pI;
        private BigInteger encryptedDSAKey;
        public CurveParams curveParams;
        private LinearRandom rnd;
        private BigInteger rhoI;
        private BigInteger randomness1;
        private BigInteger uI;
        private BigInteger vI;
        private BigInteger u;
        private BigInteger v;
        private ECPoint rI;
        private BigInteger wI;
        private BigInteger w;
        private BigInteger r;
        private PartialDecryption wShare;
        private PartialDecryption sigmaShare;
        private BigInteger kI;
        private BigInteger cI;
        private BigInteger randomness2;

        IEnumerable<BigInteger> openUiVi;
        IEnumerable<BigInteger> openRiWi;

        public PublicParameters parameters;

        public byte[] message;
        Round1Message[] round1messages;
        Round3Message[] round3messages;

        public PlayerSigner(PublicParameters parameters,
            CurveParams curveParams,
            PaillierPrivateThresholdKey paillierKeyShare,
            BigInteger encryptedDSAKey, byte[] message)
        {
            this.curveParams = curveParams;
            rnd = new LinearRandom(123456789);
            paillierPublicKey = new Paillier(paillierKeyShare.getPublicKey());
            pI = new PaillierThreshold(paillierKeyShare);
            this.message = message;
            this.parameters = parameters;
            this.encryptedDSAKey = encryptedDSAKey;
        }

        public Round1Message round1()
        {
            rhoI = Util.randomFromZn(curveParams.Q, rnd);
            randomness1 = paillierPublicKey.getPublicKey().getRandomModNStar();
            uI = paillierPublicKey.encrypt(rhoI, randomness1);
            vI = paillierPublicKey.multiply(encryptedDSAKey, rhoI);

            openUiVi = new[] {uI, vI};
            var commUiVi = Util.sha3Hash(openUiVi);

            return new Round1Message(commUiVi);
        }

        public Round2Message round2(IEnumerable<Round1Message> round1Messages)
        {
            // save round1messages which contain commitment to ui and vi so you
            // can verify them after they're opened during this round.
            this.round1messages = round1Messages.ToArray();

            Zkpi1 zkp1 = new Zkpi1(curveParams, parameters, rhoI, rnd, randomness1, vI, encryptedDSAKey, uI);

            return new Round2Message(openUiVi, zkp1);
        }

        public Round3Message round3(IEnumerable<Round2Message> round2Messages)
        {
            // check uI and vi commitment. We are assuming that the players
            // messages are
            // presented in the same order for consecutive rounds. Otherwise, the
            // verification
            // will fail.

            var messages = round2Messages.ToArray();
            for (int i = 0; i < messages.Length; i++)
            {
                var msg1 = round1messages[i];
                var msg2 = messages[i];

                if (!Util.sha3Hash(msg2.openUiVi).SequenceEqual(msg1.uIviCommitment))
                    throw new Exception("Commitment from R2 failed");
            }

            // verify Everyone else's Zkp_i1
            foreach (Round2Message msg in messages)
            {
                if (!msg.zkp1.verify(parameters, curveParams.Curve,
                    msg.openUiVi.ElementAt(1), encryptedDSAKey,
                    msg.openUiVi.ElementAt(0)))
                {
                    //throw new Exception("ZKP1 failed");
                }
            }

            u = uI;
            for (int i = 0; i < messages.Length; i++)
            {
                u = paillierPublicKey.add(u,
                    messages[i].openUiVi.ElementAt(0));
            }

            v = vI;
            for (int i = 0; i < messages.Length; i++)
            {
                v = paillierPublicKey.add(v,
                    messages[i].openUiVi.ElementAt(1));
            }

            kI = Util.randomFromZn(curveParams.Q, rnd);
            rI = curveParams.G.Multiply(kI);
            cI = Util.randomFromZn(curveParams.Q.Pow(6), rnd);
            randomness2 = paillierPublicKey.getPublicKey().getRandomModNStar();
            BigInteger mask = paillierPublicKey.encrypt(
                curveParams.Q.Multiply(cI), randomness2);
            wI = paillierPublicKey.add(paillierPublicKey.multiply(u, kI), mask);

            openRiWi = new[]
            {
                new BigInteger(rI.GetEncoded()), wI
            };

            return new Round3Message(Util.sha3Hash(openRiWi));
        }

        public Round4Message round4(IEnumerable<Round3Message> round3Messages)
        {
            // save round3messages which contain commitment to wi and ri so you
            // can verify them after they're opened during this round.
            this.round3messages = round3Messages.ToArray();

            Zkpi2 zkp2 = new Zkpi2(curveParams, parameters, kI, cI, rnd, curveParams.G, wI, u, randomness2);

            return new Round4Message(openRiWi, zkp2);
        }

        public Round5Message round5(IEnumerable<Round4Message> round4Messages)
        {
            var messages = round4Messages.ToArray();
            // check rI and wI commitment. We are assuming that the players
            // messages are presented in the same order for consecutive
            // rounds. Otherwise, the verification will fail.
            for (int i = 0; i < messages.Length; i++)
            {
                var msg3 = round3messages[i];
                var msg4 = messages[i];

                if (!Util.sha3Hash(msg4.openRiWi).SequenceEqual(msg3.riWiCommitment))
                    throw new Exception("Commitment from R4");
            }

            // verify Everyone else's Zkp_i2
            foreach (Round4Message msg in messages)
            {
                if (!msg.zkp2.verify(
                    parameters,
                    curveParams.Curve,
                    curveParams.Curve.Curve.DecodePoint(
                        msg.openRiWi.ElementAt(0).ToByteArray()), u,
                    msg.openRiWi.ElementAt(1)))
                {
                    throw new Exception("ZKP2 failed");
                }
            }

            w = wI;
            for (int i = 0; i < messages.Length; i++)
            {
                w = paillierPublicKey.add(w,
                    messages[i].openRiWi.ElementAt(1));
            }

            ECPoint R = rI;
            for (int i = 0; i < messages.Length; i++)
            {
                R = R.Add(curveParams.Curve.Curve.DecodePoint(
                    messages[i].openRiWi.ElementAt(0).ToByteArray()));
            }

            r = R.Normalize().XCoord.ToBigInteger().Mod(curveParams.Q);
            wShare = pI.decrypt(w);

            return new Round5Message(wShare);
        }

        public Round6Message round6(IEnumerable<Round5Message> round5Messages)
        {
            var messages = round5Messages.ToArray();
            PartialDecryption[] wShares = new PartialDecryption[messages.Length + 1];
            wShares[0] = wShare;
            for (int i = 0; i < messages.Length; i++)
            {
                PartialDecryption share = messages[i].wShare;

                wShares[i + 1] = share;
            }

            BigInteger mu = pI.combineShares(wShares);
            BigInteger sigma = paillierPublicKey.multiply(paillierPublicKey.add(
                paillierPublicKey.multiply(u,
                    Util.calculateMPrime(curveParams.Q, message)),
                paillierPublicKey.multiply(v, r)), mu
                .ModInverse(curveParams.Q));

            sigmaShare = pI.decrypt(sigma);

            return new Round6Message(sigmaShare);
        }

        public DSASignature outputSignature(IEnumerable<Round6Message> round6Messages)
        {
            var messages = round6Messages.ToArray();
            PartialDecryption[] sigmaShares = new PartialDecryption[messages.Length + 1];
            sigmaShares[0] = sigmaShare;
            for (int i = 0; i < messages.Length; i++)
            {
                sigmaShares[i + 1] = messages[i].sigmaShare;
            }

            BigInteger s = pI.combineShares(sigmaShares).Mod(curveParams.Q);
            if (s.CompareTo(curveParams.Q.Divide(BigInteger.ValueOf(2))) > 0)
            {
                s = curveParams.Q.Subtract(s);
            }

            return new DSASignature(r, s);
        }
    }
}