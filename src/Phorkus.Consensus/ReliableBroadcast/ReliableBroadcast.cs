using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Google.Protobuf;
using Phorkus.Consensus.BinaryAgreement;
using Phorkus.Consensus.Messages;
using Phorkus.Consensus.TPKE;
using Phorkus.Crypto;
using Phorkus.Proto;
using Phorkus.Utility.Utils;
using STH1123.ReedSolomon;

namespace Phorkus.Consensus.ReliableBroadcast
{
    public class ReliableBroadcast : AbstractProtocol
    {
        private readonly ReliableBroadcastId _broadcastId;
        private readonly bool[] _isBroadcast;
        private readonly ISet<int>[] _receivedValues;
        private ResultStatus _requested;
        private BoolSet? _result;
        
        public override IProtocolIdentifier Id => _broadcastId;
        
        public ReliableBroadcast(ReliableBroadcastId broadcastId, IWallet wallet, IConsensusBroadcaster broadcaster) : 
            base(wallet, broadcaster)
        {
            _broadcastId = broadcastId;
            _requested = ResultStatus.NotRequested;
            _receivedValues = new ISet<int>[N];
            for (var i = 0; i < N; ++i)
                _receivedValues[i] = new HashSet<int>();
            _isBroadcast = new bool[2];
            _result = null;
        }

        private byte[] GetTestVector(int size=1000)
        {
            var vector = new byte[size];
            var rnd = new Random();
            rnd.NextBytes(vector);
            return vector;
        }
        
        [MethodImpl(MethodImplOptions.Synchronized)]
        public override void ProcessMessage(MessageEnvelope envelope)
        {
            
            CreateValMessage(GetTestVector(N * 64));
            if (envelope.External)
            {
                var message = envelope.ExternalMessage;
                switch (message.PayloadCase)
                {
                    case ConsensusMessage.PayloadOneofCase.ValMessage:
                        HandleValMessage(message.Validator, message.ValMessage);
                        break;
                    case ConsensusMessage.PayloadOneofCase.EchoMessage:
                        HandleECHOMessage(message.Validator, message.EchoMessage);
                        break;
                    default:
                        throw new ArgumentException(
                            $"consensus message of type {message.PayloadCase} routed to BinaryBroadcast protocol"
                        );
                }
            }
            else
            {
                var message = envelope.InternalMessage;
                switch (message)
                {    
                    case ProtocolRequest<ReliableBroadcastId, EncryptedShare> broadcastRequested:
                        HandleInputMessage(broadcastRequested);
                        break;
                    case ProtocolResult<ReliableBroadcastId, EncryptedShare> _:
                        Terminated = true;
                        break;
                    default:
                        throw new InvalidOperationException(
                            "Binary broadcast protocol handles not any internal messages");
                }
            }
        }

        private void HandleECHOMessage(Validator messageValidator, object echo)
        {
            throw new NotImplementedException();
        }

        private void HandleValMessage(Validator messageValidator, object val)
        {
            throw new NotImplementedException();
        }

        private void HandleInputMessage(ProtocolRequest<ReliableBroadcastId, EncryptedShare> broadcastRequested)
        {
            
            throw new NotImplementedException();
        }


        private int[] ByteToIntDefineSize(byte[] bytes, int sizeMax, int zeroCount)
        {
            var result = new int[bytes.Length + zeroCount];
            for (var i = 0; i < bytes.Length; i++)
            {
                if(bytes[i] < sizeMax)
                    result[i] = bytes[i];
            }
            return result;
        }
        
        private ConsensusMessage CreateValMessage(byte[] input)
        {
            // if call from sender context
            var blocks = CreateBlocks(input);
            var root = CalculateRoot(blocks);
            var branch = CreateBranch(blocks, 0);
            
            var message = new ConsensusMessage
            {
                Validator = new Validator
                {
                    ValidatorIndex = GetMyId(),
                    Era = _broadcastId.Era
                },
                Init = new InitRBCProtocolMessage
                {
                    Val = new ValMessage
                    {
                        RootMerkleTree = root,
                        BranchMerkleTree = ByteString.CopyFrom(branch.ToByteArray()),
                        BlockErasureCoding = ByteString.CopyFrom(blocks.ToByteArray())                        
                    }
                }
            };
            return message;
        }

        // blocks - list of block on which create MT
        // playerIndex - it is the player index for which create branch (i.e. path) 
        private static List<UInt256> CreateBranch(List<UInt256> blocks, int playerIndex)
        {
            var mt = MerkleTree.ComputeTree(blocks.ToArray());
            var branch = new List<UInt256>();
            var currnode = mt.Search(blocks[playerIndex]);
            
            while (!currnode.IsRoot){
                branch.Add(currnode.Hash);
                currnode = currnode.Parent;
            }
            return branch;
        }


        private List<UInt256> CreateBlocks(byte[] input)
        {
            var additionalBits = 16;
            var initDataBuffer = ByteToIntDefineSize(input, 256, additionalBits);
            var field = new GenericGF(285, 256, 0);
            var rse = new ReedSolomonEncoder(field);

            var encodeArray = new List<int[]>();
            if (initDataBuffer.Length > 256)
            {
                generalDivide(initDataBuffer, encodeArray, N);
            }

            for (var i = 0; i < encodeArray.Count; i++)
            {
                rse.Encode(encodeArray[i], additionalBits);
            }
            
//            ReedSolomonDecoder rsd = new ReedSolomonDecoder(field);
//            
//            var tip = new []{0,1,2,3,4};
//            
//            if (rsd.Decode(initdata, additionalBits, null))
//            {
//                Console.WriteLine("Data corrected.");
//                //Console.WriteLine(String.Join(", ", afterRecieve.ToArray()));
//            }
//            else
//            {
//                Console.WriteLine("Too many errors-erasures to correct.");
//            }
            
            return BuildShares(initDataBuffer, N);
        }
        
        private static UInt256 CalculateRoot(List<UInt256> blocks)
        {
            return MerkleTree.ComputeRoot(blocks.ToArray());
        }        
        
        private List<UInt256> BuildShares(Int32[] v, int playersCount)
        {
            if (!v.Any())
            {
                return new List<UInt256>();
            }
            var shareSize = v.Length / playersCount + 1;
            
            var a = new List<UInt256>();
            
            for (var i = 0; i < playersCount; i++)
            {
                var buffer = new byte[shareSize];
                for (var j = 0; j < shareSize; j++)
                {
                    buffer[j] = BitConverter.GetBytes(v[i * playersCount + j])[0];
                }
                a.Add(buffer.Keccak256().ToUInt256());
            }
            return a;
        }
        
        private void generalDivide(int[] source, List<int[]> result, int parts, int fillSybol=0)
        {
            var bufferLength = source.Length;
            var partLength = bufferLength / parts;
            var partsCount = bufferLength / partLength;
            var tmp = new int[partLength];
            if (bufferLength % partLength != 0)
            {
                throw new ArgumentException("The length of source parameter should be divided by the length of part ", nameof(source));
            }
            for (int i = 0; i < partsCount; i++)
            {
                tmp = source.Skip(partLength * i).Take(partLength).ToArray();
                result.Add(tmp);
            }
        }
    }
    
}
