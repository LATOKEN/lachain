using System;
using System.Collections.Generic;
using System.Linq;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Math.EC;
using Phorkus.Hermes.Crypto.Key;
using Phorkus.Hermes.Signer.Messages;

namespace Phorkus.Hermes.Signer
{
    public class DefaultSignerProtocol : ISignerProtocol
    {
        private PlayerSigner _playerSigner;
        private byte[] _share;
        private IEnumerable<byte> _privateKey;
        private byte[] _publicKey;
        private string _curveType;
        
        public SignerState CurrentState { get; private set; }
        
        public DefaultSignerProtocol(byte[] share, IEnumerable<byte> privateKey, byte[] publicKey, string curveType)
        {
            _share = share;
            _privateKey = privateKey;
            _publicKey = publicKey;
            _curveType = curveType;
        }

        public void Initialize(byte[] message)
        {
            var paillierKey = new PaillierPrivateThresholdKey(_share, 4289);
            var curveParams = new CurveParams(_curveType);
            
            /* TODO: "this seed should be replaced with something more random, like transaction hash" */
            var rnd = new Random(98428965);

            var l1 = rnd.Next(31);
            var l2 = rnd.Next(31);
            var l3 = rnd.Next(31);
            
            /* convert private key to big integer (don't forget about first sign byte) */
            var bigPrivateKey = new BigInteger(new byte[1].Concat(_privateKey).ToArray());
            
            /* generate public params */
            var publicParameters = Util.generateParamsforBitcoin(curveParams, 60, 256, rnd, paillierKey);
            
            Console.WriteLine("G: " + publicParameters.getG());
            Console.WriteLine("h1: " + publicParameters.h1);
            Console.WriteLine("h2: " + publicParameters.h2);
            Console.WriteLine("Tilde: " + publicParameters.nTilde);
            Console.WriteLine("PP: " + publicParameters.paillierPubKey);
            
            _playerSigner = new PlayerSigner(publicParameters, curveParams, paillierKey, bigPrivateKey, message);
            CurrentState = SignerState.Initialization;
        }

        public Round1Message Round1()
        {
            CurrentState = SignerState.Round1;
            return _playerSigner.round1();
        }

        public Round2Message Round2(IEnumerable<Round1Message> round1Messages)
        {
            CurrentState = SignerState.Round2;
            return _playerSigner.round2(round1Messages);
        }

        public Round3Message Round3(IEnumerable<Round2Message> round2Messages)
        {
            CurrentState = SignerState.Round3;
            return _playerSigner.round3(round2Messages);
        }

        public Round4Message Round4(IEnumerable<Round3Message> round3Messages)
        {
            CurrentState = SignerState.Round4;
            return _playerSigner.round4(round3Messages);
        }

        public Round5Message Round5(IEnumerable<Round4Message> round4Messages)
        {
            CurrentState = SignerState.Round5;
            return _playerSigner.round5(round4Messages);
        }

        public Round6Message Round6(IEnumerable<Round5Message> round5Messages)
        {
            CurrentState = SignerState.Round6;
            return _playerSigner.round6(round5Messages);
        }

        public DSASignature Finalize(IEnumerable<Round6Message> round6Messages)
        {
            CurrentState = SignerState.Finalization;
            var publicKey = _playerSigner.curveParams.Curve.Curve.DecodePoint(_publicKey);
            Console.WriteLine("Public Key: " + publicKey);
            var signature = _playerSigner.outputSignature(round6Messages);
            BigInteger mprime = new BigInteger(1, _playerSigner.message);
            BigInteger invS = signature.s.ModInverse(_playerSigner.curveParams.Q);
            ECPoint validation = publicKey.Multiply(signature.r.Multiply(invS).Mod(_playerSigner.curveParams.Q))
                .Add(_playerSigner.curveParams.G.Multiply(mprime.Multiply(invS).Mod(_playerSigner.curveParams.Q)));
            BigInteger vX = validation.Normalize().XCoord.ToBigInteger().Mod(_playerSigner.curveParams.Q);
            Console.WriteLine("R: " + signature.r + ", S: " + signature.s);
            if (!vX.Equals(signature.r))
                throw new InvalidSignatureException("Signature validation failed");
            return signature;
        }
    }
}