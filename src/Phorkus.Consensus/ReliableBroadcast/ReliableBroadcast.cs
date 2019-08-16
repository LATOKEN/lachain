using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Google.Protobuf;
using Phorkus.Consensus.BinaryAgreement;
using Phorkus.Consensus.Messages;
using Phorkus.Crypto;
using Phorkus.Proto;
using Phorkus.Utility.Utils;
//using STH1123.ReedSolomon;

namespace Phorkus.Consensus.ReliableBroadcast
{
    public class ReliableBroadcast : AbstractProtocol
    {
        public ReliableBroadcast(ReliableBroadcastId reliableBroadcastId, IConsensusBroadcaster broadcaster) : base(
            broadcaster)
        {
            throw new NotImplementedException();
        }
        private readonly ReliableBroadcastId _broadcastId;

        [MethodImpl(MethodImplOptions.Synchronized)]
        public override void ProcessMessage(MessageEnvelope envelope)
        {
            throw new NotImplementedException();
        }

        public override IProtocolIdentifier Id => _broadcastId;
        /*
        private readonly IConsensusBroadcaster _broadcaster;
        private readonly IConsensusBroadcaster _consensusBroadcaster;

        private readonly bool[] _isBroadcast;
        
        
        private readonly int _faulty, _players;
        private readonly ISet<int>[] _receivedValues;
        private ResultStatus _requested;
        private BoolSet? _result;
        
        
        public override IProtocolIdentifier Id { get; }
        
        public ReliableBroadcast(int n, int f, ReliableBroadcastId broadcastId, IConsensusBroadcaster broadcaster)
        {
            _broadcastId = broadcastId;
            _broadcaster = broadcaster;
            _players = n;
            _faulty = f;
            _requested = ResultStatus.NotRequested;
            _receivedValues = new ISet<int>[n];
            for (var i = 0; i < n; ++i)
                _receivedValues[i] = new HashSet<int>();
            _isBroadcast = new bool[2];
            _result = null;
        }
        
        [MethodImpl(MethodImplOptions.Synchronized)]
        public override void ProcessMessage(MessageEnvelope envelope)
        {
            if (envelope.External)
            {
//                var message = envelope.ExternalMessage;
//                switch (message.PayloadCase)
//                {
//                    case ConsensusMessage.PayloadOneofCase.Val:
//                        HandleValMessage(message.Validator, message.Val);
//                        return;
//                    case ConsensusMessage.PayloadOneofCase.ECHO:
//                        HandleECHOMessage(message.Validator, message.ECHO);
//                        return;
//                    default:
//                        throw new ArgumentException(
//                            $"consensus message of type {message.PayloadCase} routed to BinaryBroadcast protocol"
//                        );
//                }
            }
            else
            {
                var message = envelope.InternalMessage;
                switch (message)
                {
//                    case ProtocolRequest<BinaryBroadcastId, bool> broadcastRequested:
//                        _requested = true;
//                        
//                        var b = broadcastRequested.Input ? 1 : 0;
//                        _isBroadcast[b] = true;
//                        _consensusBroadcaster.Broadcast(CreateValMessage(b));
//                        break;
//                    case ProtocolResult<BinaryBroadcastId, BoolSet> broadcastCompleted:
//                        break;
//                    default:
//                        throw new InvalidOperationException(
//                            "Binary broadcast protocol handles not any internal messages");
                }
            }
        }

        private void HandleValMessage(Validator validator, ValMessage valMessage)
        {
            throw new NotImplementedException();
        }
        private void HandleECHOMessage(Validator validator, ECHOMessage echoMessage)
        {
            throw new NotImplementedException();
        }
        
        private ConsensusMessage CreateValMessage(UInt64 input)
        {
            var root = CalculateRoot(new List<UInt256>());
            var branch = CreateBranch();
            var block = CreateBlocks(BitConverter.GetBytes(input));
            
            var message = new ConsensusMessage
            {
                Validator = new Validator
                {
                    // TODO: somehow fill validator field
                    ValidatorIndex = _broadcaster.GetMyId(),
                    Era = _broadcastId.Era
                },
                Init = new InitRBCProtocolMessage
                {
                    Val = new ValMessage
                    {
                        RootMerkleTree = root,
                        BranchMerkleTree = ByteString.CopyFrom(),
                        BlockErasureCoding = ByteString.CopyFrom()                        
                    }
                }
            };
            return message;
        }



        private class Branch
        {
            
        }

        private class Block
        {
            
        }

        private class Root : List<byte>
        {
            
        }
        private static Branch CreateBranch()
        {
            return new Branch();
        }

        private List<Block> CreateBlocks(byte[] v)
        {
            List<UInt256> blocks = SeparateVector(v, _players);
            CalculateRoot(blocks);
            
            GenericGF field = new GenericGF(285, 256, 0);
            ReedSolomonEncoder rse = new ReedSolomonEncoder(field);
            int[] data = new int[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F, 0x20, 0x57, 0x6F, 0x72, 0x6C, 0x64, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
            rse.Encode(data, 9);
            Console.WriteLine(data.ToString());
            
            return new List<Block>();
        }
        
        
        private static UInt256 CalculateRoot(List<UInt256> blocks)
        {
            return MerkleTree.ComputeRoot(blocks.ToArray());
        }        

        private List<UInt256> SeparateVector(byte[] v, int numberParts)
        {
            byte[] aa = v.Keccak256().ToUInt256().ToBigInteger().ToByteArray();
            
            
            
            v.Keccak256().ToUInt256();
            v.ToUInt256();

            int lenPart = v.Length / numberParts;
            List<byte[]> a;
            byte[] buffer;
            for (var i = 0; i < lenPart; i++)
            {
                buffer = v + i;
                a.Add(buffer);
            }
            
            
            if (!v.Any())
                return new List<int>();
            // the logic for separate the input of the vector of bytes
            var blocksTestInt = new List<int>();
            var blocksTest = new List<UInt256>();

            var rnd = new Random();
            
            for (var i = 0; i < _players; i++)
            {
                blocksTestInt.Add(rnd.Next());
                blocksTest.Add(blocksTestInt[i].ToByteArray().ToUInt256());
            }
            return blocksTest;
        }
        */
    }
}
