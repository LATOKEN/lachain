using System;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;
using Lachain.Logger;
using Lachain.Consensus.Messages;
using Lachain.Crypto.MCL.BLS12_381;
using Lachain.Crypto.TPKE;
using Lachain.Proto;
using Lachain.Utility.Utils;


// todo investigate https://github.com/poanetwork/hbbft/blob/master/src/sync_key_gen.rs#L17 and implement robust version of key generation

namespace Lachain.Consensus.TPKE
{
    public class TPKESecureSetup : AbstractProtocol
    {
        private readonly TPKESetupId _tpkeSetupId;
        private ResultStatus _requested;
        private Keys? _result;

        private readonly Fr[] _p;
        private readonly Fr[] _received;
        private readonly G1[][] _hiddenPolyG1;
        private readonly G2[][] _hiddenPolyG2;
        private readonly byte[][][] _confirmationHash;
        private bool _allValuesReceived;
        private bool _allHiddenPolynomialsReceived;
        private bool _allConfirmationHashesReceived;
        private bool _allHashSent;
        private readonly ILogger<TPKESecureSetup> _logger = LoggerFactory.GetLoggerForClass<TPKESecureSetup>();

        public TPKESecureSetup(
            TPKESetupId tpkeSetupId, IPublicConsensusKeySet wallet, IConsensusBroadcaster broadcaster)
            : base(wallet, tpkeSetupId, broadcaster)
        {
            _tpkeSetupId = tpkeSetupId;
            _requested = ResultStatus.NotRequested;

            _p = new Fr[F];

            // todo add reception manager!!!!@
            _received = new Fr[N];
            for (var i = 0; i < N; ++i)
                _received[i] = Fr.FromInt(0);

            _hiddenPolyG1 = new G1[N][];
            _hiddenPolyG2 = new G2[N][];
            _confirmationHash = new byte[N][][];
            _result = null;
        }

        public override void ProcessMessage(MessageEnvelope envelope)
        {
            if (envelope.External)
            {
                var message = envelope.ExternalMessage;
                if (message is null) throw new ArgumentNullException();
                switch (message.PayloadCase)
                {
                    case ConsensusMessage.PayloadOneofCase.PolynomialValue:
                        HandlePolynomialValue(envelope.ValidatorIndex, message.PolynomialValue);
                        break;
                    case ConsensusMessage.PayloadOneofCase.HiddenPolynomial:
                        HandleHiddenPolynomial(envelope.ValidatorIndex, message.HiddenPolynomial);
                        break;
                    case ConsensusMessage.PayloadOneofCase.ConfirmationHash:
                        HandleConfirmationHash(envelope.ValidatorIndex, message.ConfirmationHash);
                        break;
                    default:
                        throw new ArgumentException(
                            $"consensus message of type {message.PayloadCase} routed to CommonSubset protocol"
                        );
                }
            }
            else
            {
                var message = envelope.InternalMessage;
                switch (message)
                {
                    case ProtocolRequest<TPKESetupId, object> request:
                        HandleInputMessage(request);
                        break;
                    case ProtocolResult<TPKESetupId, Keys> _:
                        Terminate();
                        break;
                    default:
                        throw new InvalidOperationException(
                            "CommonSubset protocol failed to handle internal message");
                }
            }
        }

        private void HandleInputMessage(ProtocolRequest<TPKESetupId, object> request)
        {
            _logger.LogDebug($"Player {GetMyId()} got input");
            _requested = ResultStatus.Requested;
            for (var i = 0; i < F; ++i)
            {
                _p[i] = Fr.GetRandom();
            }

            for (var j = 0; j < N; ++j)
            {
                var polyVal = new ConsensusMessage
                {
                    PolynomialValue = new TPKEPolynomialValueMessage
                    {
                        Value = ByteString.CopyFrom(Fr.ToBytes(Mcl.GetValue(_p, j + 1)))
                    }
                };
                Broadcaster.SendToValidator(polyVal, j);
            }

            var msg = new ConsensusMessage
            {
                HiddenPolynomial = new TPKEHiddenPolynomialMessage()
            };

            for (var j = 0; j < F; ++j)
            {
                msg.HiddenPolynomial.CoeffsG1.Add(ByteString.CopyFrom(G1.ToBytes(G1.Generator * _p[j])));
                msg.HiddenPolynomial.CoeffsG2.Add(ByteString.CopyFrom(G2.ToBytes(G2.Generator * _p[j])));
            }

            Broadcaster.Broadcast(msg);

            CheckResult();
        }

        private void HandlePolynomialValue(int sender, TPKEPolynomialValueMessage messagePolynomialValue)
        {
            _logger.LogDebug($"Player {GetMyId()} got value from {sender}");
            var value = Fr.FromBytes(messagePolynomialValue.Value.ToByteArray());
            // todo fix
//            if (_received[messageValidator.ValidatorIndex] == null)
//                throw new Exception("Already received!");

            _received[sender] = value;
            CheckAllValuesReceived();
        }

        private void HandleHiddenPolynomial(int sender, TPKEHiddenPolynomialMessage msg)
        {
            if (msg.CoeffsG1.Count != F || msg.CoeffsG2.Count != F)
            {
                throw new Exception($"Expected length of coeffs to be {F}, but got {msg.CoeffsG1.Count}");
            }

            var tmpG1 = new List<G1>();
            var tmpG2 = new List<G2>();
            for (var i = 0; i < F; ++i)
            {
                tmpG1.Add(G1.FromBytes(msg.CoeffsG1[i].ToByteArray()));
                tmpG2.Add(G2.FromBytes(msg.CoeffsG2[i].ToByteArray()));
            }

            _hiddenPolyG1[sender] = tmpG1.ToArray();
            _hiddenPolyG2[sender] = tmpG2.ToArray();


            CheckAllPolynomialsReceived();
        }

        private void TrySendConfirmationHash()
        {
            if (_allHashSent) return;
            if (!_allHiddenPolynomialsReceived) return;

            var msg = new ConsensusMessage
            {
                ConfirmationHash = new TPKEConfirmationHashMessage()
            };

            for (var i = 0; i < N; ++i)
            {
                var hsh = Mcl.CalculateHash(_hiddenPolyG1[i], _hiddenPolyG2[i]);
                msg.ConfirmationHash.Hashes.Add(ByteString.CopyFrom(hsh));
            }

            Broadcaster.Broadcast(msg);

            _allHashSent = true;
        }

        private void HandleConfirmationHash(int sender, TPKEConfirmationHashMessage hsh)
        {
            var tmp = new List<byte[]>();
            for (var i = 0; i < N; ++i)
            {
                tmp.Add(hsh.Hashes[i].ToByteArray());
            }

            _confirmationHash[sender] = tmp.ToArray();

            CheckAllConfirmationHashesReceived();
        }

        private void CheckAllValuesReceived()
        {
            if (_allValuesReceived) return;

            for (var i = 0; i < N; ++i)
                if (_received[i].Equals(Fr.FromInt(0)))
                    return;

            _allValuesReceived = true;
            TryFinalize();
        }

        private void CheckAllPolynomialsReceived()
        {
            if (_allHiddenPolynomialsReceived) return;
            for (var i = 0; i < N; ++i)
                if (_hiddenPolyG1[i] == null)
                    return;

            for (var i = 0; i < N; ++i)
                if (_hiddenPolyG2[i] == null)
                    return;

            _allHiddenPolynomialsReceived = true;
            TrySendConfirmationHash();
            TryFinalize();
        }

        private void CheckAllConfirmationHashesReceived()
        {
            if (_allConfirmationHashesReceived) return;
            for (var i = 0; i < N; ++i)
                if (_confirmationHash[i] == null)
                    return;

            _allConfirmationHashesReceived = true;
            TryFinalize();
        }

        private void TryFinalize()
        {
            if (!_allValuesReceived) return;
            if (!_allHiddenPolynomialsReceived) return;
            if (!_allConfirmationHashesReceived) return;

            // verify values
            for (var i = 0; i < N; ++i)
                if (!(G1.Generator * _received[i]).Equals(Mcl.GetValue(_hiddenPolyG1[i], GetMyId() + 1)))
                    throw new Exception($"Party {i} sent inconsistent hidden polynomial!");

            for (var i = 0; i < N; ++i)
                if (!(G2.Generator * _received[i]).Equals(Mcl.GetValue(_hiddenPolyG2[i], GetMyId() + 1)))
                    throw new Exception($"Party {i} sent inconsistent hidden polynomial!");

            // verify hashes
            for (var i = 0; i < N; ++i)
            {
                for (var j = 0; j < N; ++j)
                    if (!_confirmationHash[0][i].SequenceEqual(_confirmationHash[j][i]))
                        throw new Exception($"Hash mismatch at {i} vs {j}");
            }

            var Y = G1.Zero;
            for (var i = 0; i < N; ++i)
                Y += _hiddenPolyG1[i][0];

            var pubKey = new PublicKey(Y, F);

            var value = Fr.FromInt(0);
            for (var i = 0; i < N; ++i)
                value += _received[i];

            var privKey = new PrivateKey(value, GetMyId());

            var tmp = new List<G2>();
            for (var i = 0; i < N; ++i)
            {
                var cur = G2.Zero;
                for (var j = 0; j < N; ++j)
                {
                    cur += Mcl.GetValue(_hiddenPolyG2[j], i + 1);
                }

                tmp.Add(cur);
            }

            var verificationKey = new VerificationKey(Y, F, tmp.ToArray());

            _result = new Keys(pubKey, privKey, verificationKey);
            CheckResult();
        }

        private void CheckResult()
        {
            if (_result == null) return;
            if (_requested != ResultStatus.Requested) return;
            _requested = ResultStatus.Sent;
            Broadcaster.InternalResponse(
                new ProtocolResult<TPKESetupId, Keys>(_tpkeSetupId, _result));
        }
    }
}