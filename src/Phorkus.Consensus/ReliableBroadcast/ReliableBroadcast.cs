using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Google.Protobuf;
using Phorkus.Consensus.Messages;
using Phorkus.Crypto;
using Phorkus.Crypto.TPKE;
using Phorkus.Proto;
using Phorkus.Utility.Utils;
using STH1123.ReedSolomon;

namespace Phorkus.Consensus.ReliableBroadcast
{
    public class ReliableBroadcast : AbstractProtocol
    {
        private readonly ReliableBroadcastId _broadcastId;

        private readonly bool[] _isBroadcast;

        //private readonly ISet<int>[] _receivedValues;
        private readonly Dictionary<long, ArrayList> _receivedValues;
        private ResultStatus _requested;
        private BoolSet? _result;

        private readonly bool[] _sentReadyMsg;
        private int _countValMsg;
        private int _countCorrectECHOMsg;
        private int _countReadyMsg;


        public ReliableBroadcast(
            ReliableBroadcastId broadcastId, IPublicConsensusKeySet wallet, IConsensusBroadcaster broadcaster) :
            base(wallet, broadcastId, broadcaster)
        {
            _broadcastId = broadcastId;
            _requested = ResultStatus.NotRequested;
            //_receivedValues = new ISet<int>[N];
            _receivedValues = new Dictionary<long, ArrayList>();
            _isBroadcast = new bool[2];
            _sentReadyMsg = new bool[N];
            _result = null;

            _countValMsg = 0;
            _countCorrectECHOMsg = 0;
            _countReadyMsg = 0;
        }


        [MethodImpl(MethodImplOptions.Synchronized)]
        public override void ProcessMessage(MessageEnvelope envelope)
        {
            //CreateValMessage(GetTestVector(N * 64));
            if (envelope.External)
            {
                var message = envelope.ExternalMessage;
                if (message is null) throw new ArgumentNullException();
                switch (message.PayloadCase)
                {
                    case ConsensusMessage.PayloadOneofCase.ValMessage:
                        HandleValMessage(envelope.ValidatorIndex, message.ValMessage);
                        break;
                    case ConsensusMessage.PayloadOneofCase.EchoMessage:
                        HandleECHOMessage(envelope.ValidatorIndex, message.EchoMessage);
                        break;
                    case ConsensusMessage.PayloadOneofCase.ReadyMessage:
                        HandleReadyMessage(message.ReadyMessage);
                        break;
                    default:
                        throw new ArgumentException(
                            $"consensus message of type {message.PayloadCase} routed to ReliableBroadcast protocol"
                        );
                }
            }
            else
            {
                var message = envelope.InternalMessage;
                switch (message)
                {
                    case ProtocolRequest<ReliableBroadcastId, EncryptedShare?> broadcastRequested:
                        var tmp = broadcastRequested.From;
                        HandleInputMessage(broadcastRequested);
                        break;
                    case ProtocolResult<ReliableBroadcastId, EncryptedShare?> _:
                        Terminate();
                        break;
                    default:
                        throw new InvalidOperationException(
                            "Binary broadcast protocol handles not any internal messages");
                }
            }
        }

        private void HandleInputMessage(ProtocolRequest<ReliableBroadcastId, EncryptedShare?> broadcastRequested)
        {
            //if (broadcastRequested.Input.Id.Equals(_broadcastId.AssociatedValidatorId))
            if (broadcastRequested.From.GetHashCode().Equals(_broadcastId.GetHashCode()))
            {
                Broadcaster.Broadcast(CreateValMessage(GetTestVector(N * 64)));
            }
        }

        private void HandleValMessage(int validatorIndex, ValMessage val)
        {
            // Save: blocks, root, branch <-> senderId

            var receivedValues = new Dictionary<int, ArrayList>
            {
                [validatorIndex] = new ArrayList {val.BlockErasureCoding, val.BranchMerkleTree, val.RootMerkleTree}
            };
            // broadcast ECHO messages
            Broadcaster.Broadcast(CreateECHOMessage(val));
        }

        private void HandleECHOMessage(int validatorIndex, ECHOMessage echo)
        {
            var era = Id.Era;
            var tmp = new ArrayList
            {
                echo.BlockErasureCoding.ToByteArray(),
                echo.BranchMerkleTree.ToByteArray(),
                echo.RootMerkleTree.ToByteArray()
            };
            if (checkECHOMsg(tmp))
            {
                _receivedValues.Add(validatorIndex, tmp);
                _countCorrectECHOMsg++;
            }

            var rootMerkleTree = echo.RootMerkleTree;
            if (_countCorrectECHOMsg > N - F)
            {
                if (Interpolate() && RecomputeMerkleTree())
                {
                    if (!_sentReadyMsg[validatorIndex])
                    {
                        // here I sent ready msg for  players of ValidatorIndex
                        Broadcaster.Broadcast(CreateReadyMessage(rootMerkleTree));
                        _sentReadyMsg[validatorIndex] = true;
                    }
                }
                else
                {
                    discard();
                }
            }
            else
            {
                discard();
            }
        }

        private void HandleReadyMessage(ReadyMessage readyMessage)
        {
            var rootMerkleTree = readyMessage.RootMerkleTree;
            if (checkREADYMsg(readyMessage.RootMerkleTree.ToByteArray()))
            {
                _countReadyMsg++;
                if (_countReadyMsg == F + 1)
                {
                    Broadcaster.Broadcast(CreateReadyMessage(rootMerkleTree));
                }
                else if (_countReadyMsg == 2 * F + 1)
                {
                    if (_countCorrectECHOMsg == N - 2 * F)
                    {
                        Decode();
                    }
                }
            }
        }

        private static void Decode()
        {
        }

        private static bool Interpolate()
        {
            return true;
        }

        private static bool RecomputeMerkleTree()
        {
            return true;
        }

        void discard()
        {
        }

        bool checkECHOMsg(ArrayList toCheck)
        {
            return true;
        }

        bool checkREADYMsg(byte[] toCheck)
        {
            return true;
        }

        private int[] ByteToIntDefineSize(byte[] bytes, int sizeMax, int zeroCount)
        {
            var result = new int[bytes.Length + zeroCount];
            for (var i = 0; i < bytes.Length; i++)
            {
                if (bytes[i] < sizeMax)
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
                ValMessage = new ValMessage
                {
                    RootMerkleTree = root,
                    BranchMerkleTree = ByteString.CopyFrom(branch.ToByteArray()),
                    BlockErasureCoding = ByteString.CopyFrom(blocks.ToByteArray())
                }
            };
            return message;
        }

        private ConsensusMessage CreateReadyMessage(UInt256 root)
        {
            var message = new ConsensusMessage
            {
                ReadyMessage = new ReadyMessage
                {
                    RootMerkleTree = root
                }
            };
            return message;
        }

        private ConsensusMessage CreateECHOMessage(ValMessage valMessage)
        {
            var message = new ConsensusMessage
            {
                EchoMessage = new ECHOMessage
                {
                    RootMerkleTree = valMessage.RootMerkleTree,
                    BranchMerkleTree = ByteString.CopyFrom(valMessage.BranchMerkleTree.ToByteArray()),
                    BlockErasureCoding = ByteString.CopyFrom(valMessage.BlockErasureCoding.ToByteArray())
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

            while (!(currnode?.Hash is null) && !currnode.IsRoot)
            {
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

            foreach (var t in encodeArray)
            {
                rse.Encode(t, additionalBits);
            }


            return BuildShares(initDataBuffer, N);
        }

        private static UInt256? CalculateRoot(List<UInt256> blocks)
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

        private void generalDivide(int[] source, List<int[]> result, int parts, int fillSybol = 0)
        {
            var bufferLength = source.Length;
            var partLength = bufferLength / parts;
            var partsCount = bufferLength / partLength;
            var tmp = new int[partLength];
            if (bufferLength % partLength != 0)
            {
                throw new ArgumentException("The length of source parameter should be divided by the length of part ",
                    nameof(source));
            }

            for (int i = 0; i < partsCount; i++)
            {
                tmp = source.Skip(partLength * i).Take(partLength).ToArray();
                result.Add(tmp);
            }
        }

        private byte[] GetTestVector(int size = 1000)
        {
            var vector = new byte[size];
            var rnd = new Random();
            rnd.NextBytes(vector);
            return vector;
        }
    }
}