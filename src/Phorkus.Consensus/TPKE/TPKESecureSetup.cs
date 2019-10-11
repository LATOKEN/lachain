using System;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;
using Phorkus.Consensus.Messages;
using Phorkus.Crypto.MCL.BLS12_381;
using Phorkus.Proto;
using Phorkus.Utility.Utils;


// todo investigate https://github.com/poanetwork/hbbft/blob/master/src/sync_key_gen.rs#L17 and implement robust version of key generation

namespace Phorkus.Consensus.TPKE
{
    public class TPKESecureSetup: AbstractProtocol
    {
        private readonly TPKESetupId _tpkeSetupId;
        private ResultStatus _requested;
        private TPKEKeys _result;

        private Fr[] _P;
        private Fr[] _received;
        private G1[][] _hiddenPolyG1;
        private G2[][] _hiddenPolyG2;
        private byte[][][] _confirmationHash;
        private bool allValuesReceived = false;
        private bool allHiddenPolynomialsReceived = false;
        private bool allConfirmationHashesReceived = false;
        private bool allHashSent = false;
        

        public TPKESecureSetup(TPKESetupId tpkeSetupId, IWallet wallet, IConsensusBroadcaster broadcaster) : base(wallet, tpkeSetupId, broadcaster)
        {
            _tpkeSetupId = tpkeSetupId;
            _requested = ResultStatus.NotRequested;

            _P = new Fr[F];
            
            // todo add reception manager!!!!@
            _received = new Fr[N];
            for (var i = 0; i < N; ++i)
                _received[i] = Fr.FromInt(0);
            
            _hiddenPolyG1 = new G1[N][];
            _hiddenPolyG2 = new G2[N][];
            _confirmationHash = new byte[N][][];
        }

        public override void ProcessMessage(MessageEnvelope envelope)
        {
            if (envelope.External)
            {
                var message = envelope.ExternalMessage;
                switch (message.PayloadCase)
                {
                    case ConsensusMessage.PayloadOneofCase.PolynomialValue:
                        HandlePolynomialValue(message.Validator, message.PolynomialValue);
                        break;
                    case ConsensusMessage.PayloadOneofCase.HiddenPolynomial:
                        HandleHiddenPolynomial(message.Validator, message.HiddenPolynomial);
                        break;
                    case ConsensusMessage.PayloadOneofCase.ConfirmationHash:
                        HandleConfirmationHash(message.Validator, message.ConfirmationHash);
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
                    case ProtocolResult<TPKESetupId, TPKEKeys> _:
                        Terminated = true;
                        break;
                    default:
                        throw new InvalidOperationException(
                            "CommonSubset protocol failed to handle internal message");
                }
            }
        }

        private void HandleInputMessage(ProtocolRequest<TPKESetupId, Object> request)
        {
            Console.Error.WriteLine($"Player {GetMyId()} got input");
            _requested = ResultStatus.Requested;
            for (var i = 0; i < F; ++i)
            {
                _P[i] = Fr.GetRandom();
            }

            for (var j = 0; j < N; ++j)
            {
                var polyVal = new ConsensusMessage
                {
                    Validator = new Validator
                    {
                        Era = _tpkeSetupId.Era,
                        ValidatorIndex = GetMyId()
                    },
                    PolynomialValue = new TPKEPolynomialValueMessage
                    {
                        Value = ByteString.CopyFrom(Fr.ToBytes(Mcl.GetValue(_P.AsDynamic(), j + 1, Fr.Zero)))
                    }
                };
                _broadcaster.SendToValidator(polyVal, j);
            }
            
            var msg = new ConsensusMessage
            {
                Validator = new Validator
                {
                    Era = _tpkeSetupId.Era,
                    ValidatorIndex = GetMyId()
                },
                HiddenPolynomial = new TPKEHiddenPolynomialMessage()
            };

            for (var j = 0; j < F; ++j)
            {
                msg.HiddenPolynomial.CoeffsG1.Add(ByteString.CopyFrom(G1.ToBytes(G1.Generator * _P[j])));
                msg.HiddenPolynomial.CoeffsG2.Add(ByteString.CopyFrom(G2.ToBytes(G2.Generator * _P[j])));
            }
            
            _broadcaster.Broadcast(msg);

            CheckResult();
        }
        
        private void HandlePolynomialValue(Validator messageValidator, TPKEPolynomialValueMessage messagePolynomialValue)
        {
            var id = messageValidator.ValidatorIndex;
            Console.Error.WriteLine($"Player {GetMyId()} got value from {id}");
            var value = Fr.FromBytes(messagePolynomialValue.Value.ToByteArray());
            // todo fix
//            if (_received[messageValidator.ValidatorIndex] == null)
//                throw new Exception("Already received!");

            _received[id] = value;
            CheckAllValuesReceived();
        }

        private void HandleHiddenPolynomial(Validator messageValidator, TPKEHiddenPolynomialMessage msg)
        {
            var id = messageValidator.ValidatorIndex;
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
            
            _hiddenPolyG1[id] = tmpG1.ToArray();
            _hiddenPolyG2[id] = tmpG2.ToArray();
            
            
            CheckAllPolynomialsReceived();
        }

        private void TrySendConfirmationHash()
        {
            if (allHashSent) return;
            if (!allHiddenPolynomialsReceived) return;
            
            var msg = new ConsensusMessage
            {
                Validator = new Validator
                {
                    ValidatorIndex = GetMyId(),
                    Era = Id.Era
                },
                ConfirmationHash = new TPKEConfirmationHashMessage()
            };

            for (var i = 0; i < N; ++i)
            {
                var hsh = Mcl.CalculateHash(_hiddenPolyG1[i], _hiddenPolyG2[i]);
                msg.ConfirmationHash.Hashes.Add(ByteString.CopyFrom(hsh));
            }
            
            _broadcaster.Broadcast(msg);

            allHashSent = true;
        }
        
        private void HandleConfirmationHash(Validator messageValidator, TPKEConfirmationHashMessage hsh)
        {
            var id = messageValidator.ValidatorIndex;
            var tmp = new List<byte[]>();
            for (var i = 0; i < N; ++i)
            {
                tmp.Add(hsh.Hashes[i].ToByteArray());
            }

            _confirmationHash[id] = tmp.ToArray();
            
            CheckAllConfirmationHashesReceived();
        }

        private void CheckAllValuesReceived()
        {
            if (allValuesReceived) return;
            
            for (var i = 0; i < N; ++i)
                if (_received[i].Equals(Fr.FromInt(0)))
                    return;

            allValuesReceived = true;
            TryFinalize();
        }

        private void CheckAllPolynomialsReceived()
        {
            if (allHiddenPolynomialsReceived) return;
            for (var i = 0; i < N; ++i)
                if (_hiddenPolyG1[i] == null)
                    return;
            
            for (var i = 0; i < N; ++i)
                if (_hiddenPolyG2[i] == null)
                    return;

            allHiddenPolynomialsReceived = true;
            TrySendConfirmationHash();
            TryFinalize();
        }

        private void CheckAllConfirmationHashesReceived()
        {
            if (allConfirmationHashesReceived) return;
            for (var i = 0; i < N; ++i)
                if (_confirmationHash[i] == null)
                    return;

            allConfirmationHashesReceived = true;
            TryFinalize();
        }

        private void TryFinalize()
        {
            if (!allValuesReceived) return;
            if (!allHiddenPolynomialsReceived) return;
            if (!allConfirmationHashesReceived) return;
            
            // verify values
            for (var i = 0; i < N; ++i)
                if (!(G1.Generator * _received[i]).Equals(Mcl.GetValue(_hiddenPolyG1[i].AsDynamic(), GetMyId() + 1, G1.Zero)))
                    throw new Exception($"Party {i} sent inconsistent hidden polynomial!");
            
            for (var i = 0; i < N; ++i)
                if (!(G2.Generator * _received[i]).Equals(Mcl.GetValue(_hiddenPolyG2[i].AsDynamic(), GetMyId() + 1, G2.Zero)))
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
            
            var pubKey = new TPKEPubKey(Y, F);

            var value = Fr.FromInt(0);
            for (var i = 0; i < N; ++i)
                value += _received[i];
            
            var privKey = new TPKEPrivKey(value, GetMyId());

            var tmp = new List<G2>();
            for (var i = 0; i < N; ++i)
            {
                var cur = G2.Zero;
                for (var j = 0; j < N; ++j)
                {
                    cur += Mcl.GetValue(_hiddenPolyG2[j].AsDynamic(), i + 1, G2.Zero);
                }
                tmp.Add(cur);
            }
            var verificationKey =  new TPKEVerificationKey(Y, F, tmp.ToArray());
            
            _result = new TPKEKeys(pubKey, privKey, verificationKey);
            CheckResult();
        }

        private void CheckResult()
        {
            if (_result == null) return;
            if (_requested != ResultStatus.Requested) return;
            _requested = ResultStatus.Sent;
            _broadcaster.InternalResponse(
                new ProtocolResult<TPKESetupId, TPKEKeys>(_tpkeSetupId, _result));
        }
    }
}