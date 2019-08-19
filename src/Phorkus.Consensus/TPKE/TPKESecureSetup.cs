﻿using System;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;
using Phorkus.Consensus.Messages;
using Phorkus.Crypto.MCL.BLS12_381;
using Phorkus.Proto;
using Phorkus.Utility.Utils;

namespace Phorkus.Consensus.TPKE
{
    public class TPKESecureSetup: AbstractProtocol
    {
        private readonly TPKESetupId _tpkeSetupId;
        private ResultStatus _requested;
        private readonly int _n;
        private readonly int _t;
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
        
        public override IProtocolIdentifier Id => _tpkeSetupId;

        public TPKESecureSetup(int n, int t, TPKESetupId tpkeSetupId, IConsensusBroadcaster broadcaster) : base(broadcaster)
        {
            _n = n;
            _t = t;
            _tpkeSetupId = tpkeSetupId;
            _requested = ResultStatus.NotRequested;

            _P = new Fr[_t];
            
            // todo add reception manager!!!!@
            _received = new Fr[n];
            for (var i = 0; i < n; ++i)
                _received[i] = Fr.FromInt(0);
            
            _hiddenPolyG1 = new G1[n][];
            _hiddenPolyG2 = new G2[n][];
            _confirmationHash = new byte[n][][];
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
            for (var i = 0; i < _t; ++i)
            {
                _P[i] = Fr.GetRandom();
            }

            for (var j = 0; j < _n; ++j)
            {
                var polyVal = new ConsensusMessage
                {
                    Validator = new Validator
                    {
                        Era = _tpkeSetupId.Era,
                        ValidatorIndex = (ulong) GetMyId()
                    },
                    PolynomialValue = new TPKEPolynomialValueMsg
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
                    ValidatorIndex = (ulong) GetMyId()
                },
                HiddenPolynomial = new TPKEHiddenPolynomialMsg()
            };

            for (var j = 0; j < _t; ++j)
            {
                msg.HiddenPolynomial.CoeffsG1.Add(ByteString.CopyFrom(G1.ToBytes(G1.Generator * _P[j])));
                msg.HiddenPolynomial.CoeffsG2.Add(ByteString.CopyFrom(G2.ToBytes(G2.Generator * _P[j])));
            }
            
            _broadcaster.Broadcast(msg);

            CheckResult();
        }
        
        private void HandlePolynomialValue(Validator messageValidator, TPKEPolynomialValueMsg messagePolynomialValue)
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

        private void HandleHiddenPolynomial(Validator messageValidator, TPKEHiddenPolynomialMsg msg)
        {
            var id = messageValidator.ValidatorIndex;
            if (msg.CoeffsG1.Count != _t || msg.CoeffsG2.Count != _t)
            {
                throw new Exception($"Expected length of coeffs to be {_t}, but got {msg.CoeffsG1.Count}");
            }

            var tmpG1 = new List<G1>();
            var tmpG2 = new List<G2>();
            for (var i = 0; i < _t; ++i)
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
                    ValidatorIndex = (ulong) GetMyId(),
                    Era = Id.Era
                },
                ConfirmationHash = new TPKEConfirmationHashMsg()
            };

            for (var i = 0; i < _n; ++i)
            {
                var hsh = Mcl.CalculateHash(_hiddenPolyG1[i], _hiddenPolyG2[i]);
                msg.ConfirmationHash.Hashes.Add(ByteString.CopyFrom(hsh));
            }
            
            _broadcaster.Broadcast(msg);

            allHashSent = true;
        }
        
        private void HandleConfirmationHash(Validator messageValidator, TPKEConfirmationHashMsg hsh)
        {
            var id = messageValidator.ValidatorIndex;
            var tmp = new List<byte[]>();
            for (var i = 0; i < _n; ++i)
            {
                tmp.Add(hsh.Hashes[i].ToByteArray());
            }

            _confirmationHash[id] = tmp.ToArray();
            
            CheckAllConfirmationHashesReceived();
        }

        private void CheckAllValuesReceived()
        {
            if (allValuesReceived) return;
            
            for (var i = 0; i < _n; ++i)
                if (_received[i].Equals(Fr.FromInt(0)))
                    return;

            allValuesReceived = true;
            TryFinalize();
        }

        private void CheckAllPolynomialsReceived()
        {
            if (allHiddenPolynomialsReceived) return;
            for (var i = 0; i < _n; ++i)
                if (_hiddenPolyG1[i] == null)
                    return;
            
            for (var i = 0; i < _n; ++i)
                if (_hiddenPolyG2[i] == null)
                    return;

            allHiddenPolynomialsReceived = true;
            TrySendConfirmationHash();
            TryFinalize();
        }

        private void CheckAllConfirmationHashesReceived()
        {
            if (allConfirmationHashesReceived) return;
            for (var i = 0; i < _n; ++i)
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
            for (var i = 0; i < _n; ++i)
                if (!(G1.Generator * _received[i]).Equals(Mcl.GetValue(_hiddenPolyG1[i].AsDynamic(), GetMyId() + 1, G1.Zero)))
                    throw new Exception($"Party {i} sent inconsistent hidden polynomial!");
            
            for (var i = 0; i < _n; ++i)
                if (!(G2.Generator * _received[i]).Equals(Mcl.GetValue(_hiddenPolyG2[i].AsDynamic(), GetMyId() + 1, G2.Zero)))
                    throw new Exception($"Party {i} sent inconsistent hidden polynomial!");
            
            // verify hashes
            for (var i = 0; i < _n; ++i)
            {
                for (var j = 0; j < _n; ++j)
                    if (!_confirmationHash[0][i].SequenceEqual(_confirmationHash[j][i]))
                        throw new Exception($"Hash mismatch at {i} vs {j}");
            }

            var Y = G1.Zero;
            for (var i = 0; i < _n; ++i)
                Y += _hiddenPolyG1[i][0];
            
            var pubKey = new TPKEPubKey(Y, _t);

            var value = Fr.FromInt(0);
            for (var i = 0; i < _n; ++i)
                value += _received[i];
            
            var privKey = new TPKEPrivKey(value, GetMyId());

            var tmp = new List<G2>();
            for (var i = 0; i < _n; ++i)
            {
                var cur = G2.Zero;
                for (var j = 0; j < _n; ++j)
                {
                    cur += Mcl.GetValue(_hiddenPolyG2[j].AsDynamic(), i + 1, G2.Zero);
                }
                tmp.Add(cur);
            }
            var verificationKey =  new TPKEVerificationKey(Y, _t, tmp.ToArray());
            
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