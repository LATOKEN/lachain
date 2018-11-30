using System;
using System.Collections.Generic;
using System.Linq;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Math.EC;
using Org.BouncyCastle.Security;
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
        private JavaRandom rnd;
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

        PublicParameters parameters;

        private byte[] message;
        Round1Message[] round1messages;
        Round3Message[] round3messages;

        public PlayerSigner(PublicParameters parameters,
            PaillierPrivateThresholdKey paillierKeyShare,
            BigInteger encryptedDSAKey, byte[] message)
        {
            rnd = new JavaRandom(123456789);
            paillierPublicKey = new Paillier(paillierKeyShare.getPublicKey());
            pI = new PaillierThreshold(paillierKeyShare);
            this.message = message;
            this.parameters = parameters;
            this.encryptedDSAKey = encryptedDSAKey;
        }

        public Round1Message round1()
        {
            rhoI = Util.randomFromZn(BitcoinParams.Instance.q, rnd);
            randomness1 = paillierPublicKey.getPublicKey().getRandomModNStar();
            uI = paillierPublicKey.encrypt(rhoI, randomness1);
            vI = paillierPublicKey.multiply(encryptedDSAKey, rhoI);

            openUiVi = new[] {uI, vI};
            var commUiVi = Util.sha3Hash(openUiVi);

//            Console.WriteLine("uI: " + uI);
//            Console.WriteLine("vI: " + uI);
//            Console.WriteLine(" ~ HASH: " + HexUtil.bytesToHex(commUiVi));

            return new Round1Message(commUiVi);
        }

        public Round2Message round2(params Round1Message[] round1Messages)
        {
            // save round1messages which contain commitment to ui and vi so you
            // can verify them after they're opened during this round.
            this.round1messages = round1Messages;

            Zkpi1 zkp1 = new Zkpi1(parameters, rhoI, rnd, randomness1, vI, encryptedDSAKey, uI);

            return new Round2Message(openUiVi, zkp1);
        }

        public Round3Message round3(params Round2Message[] round2Messages)
        {
            // check uI and vi commitment. We are assuming that the players
            // messages are
            // presented in the same order for consecutive rounds. Otherwise, the
            // verification
            // will fail.

            for (int i = 0; i < round2Messages.Length; i++)
            {
                var msg1 = round1messages[i];
                var msg2 = round2Messages[i];

//                Console.WriteLine("uI: " + msg2.openUiVi.ElementAt(0));
//                Console.WriteLine("vI: " + msg2.openUiVi.ElementAt(1));
//                Console.WriteLine(" ~ HASH: " + HexUtil.bytesToHex(msg1.uIviCommitment));
//                Console.WriteLine(" ~ HASH: " + HexUtil.bytesToHex(Util.sha3Hash(msg2.openUiVi)));

                if (!Util.sha3Hash(msg2.openUiVi).SequenceEqual(msg1.uIviCommitment))
                    throw new Exception("Commitment from R2 failed");
            }

            // verify Everyone else's Zkp_i1
            foreach (Round2Message message in round2Messages)
            {
                if (!message.zkp1.verify(parameters, BitcoinParams.Instance.CURVE,
                    message.openUiVi.ElementAt(1), encryptedDSAKey,
                    message.openUiVi.ElementAt(0)))
                {
                    //throw new Exception("ZKP1 failed");
                }
            }

            u = uI;
            for (int i = 0; i < round2Messages.Length; i++)
            {
                u = paillierPublicKey.add(u,
                    round2Messages[i].openUiVi.ElementAt(0));
            }

            v = vI;
            for (int i = 0; i < round2Messages.Length; i++)
            {
                v = paillierPublicKey.add(v,
                    round2Messages[i].openUiVi.ElementAt(1));
            }

            kI = Util.randomFromZn(BitcoinParams.Instance.q, rnd);
            rI = BitcoinParams.Instance.G.Multiply(kI);
            cI = Util.randomFromZn(BitcoinParams.Instance.q.Pow(6), rnd);
            randomness2 = paillierPublicKey.getPublicKey().getRandomModNStar();
            BigInteger mask = paillierPublicKey.encrypt(
                BitcoinParams.Instance.q.Multiply(cI), randomness2);
            wI = paillierPublicKey.add(paillierPublicKey.multiply(u, kI), mask);

            openRiWi = new[]
            {
                new BigInteger(rI.GetEncoded()), wI
            };

            return new Round3Message(Util.sha3Hash(openRiWi));
        }

        public Round4Message round4(params Round3Message[] round3Messages)
        {
            // save round3messages which contain commitment to wi and ri so you
            // can verify them after they're opened during this round.
            this.round3messages = round3Messages;

            Zkpi2 zkp2 = new Zkpi2(parameters, kI, cI, rnd, BitcoinParams.Instance.G, wI, u, randomness2);

            return new Round4Message(openRiWi, zkp2);
        }

        public Round5Message round5(params Round4Message[] round4Messages)
        {
            // check rI and wI commitment. We are assuming that the players
            // messages are presented in the same order for consecutive
            // rounds. Otherwise, the verification will fail.
            for (int i = 0; i < round4Messages.Length; i++)
            {
                var msg3 = round3messages[i];
                var msg4 = round4Messages[i];

                if (!Util.sha3Hash(msg4.openRiWi).SequenceEqual(msg3.riWiCommitment))
                    throw new Exception("Commitment from R4");
            }

            // verify Everyone else's Zkp_i2
            foreach (Round4Message message in round4Messages)
            {
                if (!message.zkp2.verify(
                    parameters,
                    BitcoinParams.Instance.CURVE,
                    BitcoinParams.Instance.CURVE.Curve.DecodePoint(
                        message.openRiWi.ElementAt(0).ToByteArray()), u,
                    message.openRiWi.ElementAt(1)))
                {
                    throw new Exception("ZKP2 failed");
                }
            }

            w = wI;
            for (int i = 0; i < round4Messages.Length; i++)
            {
                w = paillierPublicKey.add(w,
                    round4Messages[i].openRiWi.ElementAt(1));
            }

            ECPoint R = rI;
            for (int i = 0; i < round4Messages.Length; i++)
            {
                R = R.Add(BitcoinParams.Instance.CURVE.Curve.DecodePoint(
                    round4Messages[i].openRiWi.ElementAt(0).ToByteArray()));
            }
            
            r = R.Normalize().XCoord.ToBigInteger().Mod(BitcoinParams.Instance.q);
            wShare = pI.decrypt(w);

            return new Round5Message(wShare);
        }

        public Round6Message round6(params Round5Message[] round5Messages)
        {
            PartialDecryption[] wShares = new PartialDecryption[round5Messages.Length + 1];
            wShares[0] = wShare;
            for (int i = 0; i < round5Messages.Length; i++)
            {
                PartialDecryption share = round5Messages[i].wShare;

                wShares[i + 1] = share;
            }

            BigInteger mu = pI.combineShares(wShares);
            BigInteger sigma = paillierPublicKey.multiply(paillierPublicKey.add(
                paillierPublicKey.multiply(u,
                    Util.calculateMPrime(BitcoinParams.Instance.q, message)),
                paillierPublicKey.multiply(v, r)), mu
                .ModInverse(BitcoinParams.Instance.q));

            sigmaShare = pI.decrypt(sigma);

            return new Round6Message(sigmaShare);
        }

        public DSASignature outputSignature(params Round6Message[] round6Messages)
        {
            PartialDecryption[] sigmaShares = new PartialDecryption[round6Messages.Length + 1];
            sigmaShares[0] = sigmaShare;
            for (int i = 0; i < round6Messages.Length; i++)
            {
                sigmaShares[i + 1] = round6Messages[i].sigmaShare;
            }

            BigInteger s = pI.combineShares(sigmaShares).Mod(BitcoinParams.Instance.q);
            if (s.CompareTo(BitcoinParams.Instance.q.Divide(BigInteger.ValueOf(2))) > 0)
            {
                s = BitcoinParams.Instance.q.Subtract(s);
            }

            return new DSASignature(r, s);
        }
    }
}