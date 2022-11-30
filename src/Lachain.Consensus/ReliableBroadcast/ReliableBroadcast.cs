using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using Google.Protobuf;
using Lachain.Consensus.Messages;
using Lachain.Crypto;
using Lachain.Crypto.TPKE;
using Lachain.Logger;
using Lachain.Proto;
using Lachain.Utility.Serialization;
using Lachain.Utility.Utils;

namespace Lachain.Consensus.ReliableBroadcast
{
    public class ReliableBroadcast : AbstractProtocol
    {
        private static readonly ILogger<ReliableBroadcast>
            Logger = LoggerFactory.GetLoggerForClass<ReliableBroadcast>();

        private readonly ReliableBroadcastId _broadcastId;

        private ResultStatus _requested;

        private readonly ECHOMessage?[] _echoMessages;
        private readonly ReadyMessage?[] _readyMessages;
        private readonly bool[] _sentValMessage;
        private readonly int _merkleTreeSize;
        private bool _readySent;

        public ReliableBroadcast(
            ReliableBroadcastId broadcastId, IPublicConsensusKeySet wallet, IConsensusBroadcaster broadcaster) :
            base(wallet, broadcastId, broadcaster)
        {
            _broadcastId = broadcastId;
            _echoMessages = new ECHOMessage?[N];
            _readyMessages = new ReadyMessage?[N];
            _sentValMessage = new bool[N];
            _requested = ResultStatus.NotRequested;
            _merkleTreeSize = N;
            while ((_merkleTreeSize & (_merkleTreeSize - 1)) != 0)
                _merkleTreeSize++; // increment while not power of two
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public override void ProcessMessage(MessageEnvelope envelope)
        {
            if (envelope.External)
            {
                var message = envelope.ExternalMessage;
                if (message is null)
                {
                    _lastMessage = "Failed to decode external message";
                    throw new InvalidOperationException();
                }
                switch (message.PayloadCase)
                {
                    case ConsensusMessage.PayloadOneofCase.ValMessage:
                        _lastMessage = "ValMessage";
                        HandleValMessage(message.ValMessage, envelope.ValidatorIndex);
                        break;
                    case ConsensusMessage.PayloadOneofCase.EchoMessage:
                        _lastMessage = "EchoMessage";
                        HandleEchoMessage(message.EchoMessage, envelope.ValidatorIndex);
                        break;
                    case ConsensusMessage.PayloadOneofCase.ReadyMessage:
                        _lastMessage = "ReadyMessage";
                        HandleReadyMessage(message.ReadyMessage, envelope.ValidatorIndex);
                        break;
                    default:
                        _lastMessage = $"consensus message of type {message.PayloadCase} routed to ReliableBroadcast protocol";
                        throw new ArgumentException(
                            $"consensus message of type {message.PayloadCase} routed to ReliableBroadcast protocol"
                        );
                }
            }
            else
            {
                var message = envelope.InternalMessage;
                if (message is null)
                {
                    _lastMessage = "Failed to decode internal message";
                    throw new InvalidOperationException();
                }
                switch (message)
                {
                    case ProtocolRequest<ReliableBroadcastId, EncryptedShare> broadcastRequested:
                        _lastMessage = "broadcastRequested";
                        HandleInputMessage(broadcastRequested);
                        break;
                    case ProtocolResult<ReliableBroadcastId, EncryptedShare> _:
                        _lastMessage = "ProtocolResult";
                        Terminate();
                        break;
                    default:
                        _lastMessage = $"RBC protocol does not handle message of type {message.GetType()}";
                        throw new InvalidOperationException(
                            $"RBC protocol does not handle message of type {message.GetType()}");
                }
            }
        }

        private void HandleInputMessage(ProtocolRequest<ReliableBroadcastId, EncryptedShare> broadcastRequested)
        {
            _requested = ResultStatus.Requested;
            if (broadcastRequested.Input == null)
            {
                CheckResult();
                return;
            }

            Logger.LogTrace($"Protocol {Id} got input");
            var input = broadcastRequested.Input.ToBytes().ToList();
            AugmentInput(input);
            foreach (var (valMessage, i) in ConstructValMessages(input).WithIndex())
            {
                Broadcaster.SendToValidator(new ConsensusMessage {ValMessage = valMessage}, i);
                Logger.LogTrace($"Protocol {Id} sent VAL to validator {i} ({Wallet.EcdsaPublicKeySet[i].ToHex()})");
            }

            CheckResult();
        }

        private void HandleValMessage(ValMessage val, int validator)
        {
            var validatorPubKey = Wallet.EcdsaPublicKeySet[validator].ToHex();
            if (_sentValMessage[validator])
            {
                Logger.LogWarning($"{Id}: validator {validator} ({validatorPubKey}) tried to send VAL message twice");
                return;
            }

            Logger.LogTrace(
                $"Protocol {Id} got VAL message from {validator} ({validatorPubKey}), sending ECHO"
            );

            _sentValMessage[validator] = true;
            // Before sending echo, we can check if validator == val.SenderId, means if the val message is from the correct validator
            // Because one validator cannot produce val message of another validator, it can only send echo.
            // If we don't check this condition, there could be potential issue, for example, a malicious validator (id = x) sends a
            // val message that has random shards but correct MerkleProof and uses val.SenderId = y (another validator), and sends to
            // validator with id = z. It will be constructed as echo message and sent to everyone by validator, id = z, this echo will
            // pass the CheckEchoMessage(). Now every honest validator will think that val message of validator of id = y is confirmed
            // by echo message from validator of id = z. When the correct val message of id = y will come to id = z, he will send echo
            // again but others will not accept it, because id = z already sent echo for id = y, (but it was from malicious id = x),
            // because the correct echo for each pair is received only once.
            if (validator == val.SenderId)
            {
                InvokeReceivedExternalMessage(validator, new ConsensusMessage { ValMessage = val });
                Broadcaster.Broadcast(CreateEchoMessage(val));
            }
            else
            {
                var pubKey = Broadcaster.GetPublicKeyById(validator)!.ToHex();
                Logger.LogWarning(
                    $"Faulty behaviour: val message with sender id: {val.SenderId} came from validator: " +
                    $"{validator} ({pubKey}), which should not happen. Val message for {val.SenderId} should come " +
                    $"from {val.SenderId}. Not sending echo message for this val message");
            }
        }

        private void HandleEchoMessage(ECHOMessage echo, int validator)
        {
            // Every validator can send echo to any instance of ReliableBroadcast of any validator. So if not handled
            // properly, wrong message can stop all instances of ReliableBroadcast which will stop consensus
            var validatorPubKey = Wallet.EcdsaPublicKeySet[validator].ToHex();
            if (_echoMessages[validator] != null)
            {
                Logger.LogWarning($"{Id} already received correct echo from {validator} ({validatorPubKey})");
                return;
            }

            if (!CheckEchoMessage(echo, validator))
            {
                Logger.LogWarning($"{Id}: validator {validator} ({validatorPubKey}) sent incorrect ECHO");
                return;
            }

            Logger.LogTrace($"Protocol {Id} got ECHO message from {validator} ({validatorPubKey})");
            _echoMessages[validator] = echo;
            InvokeReceivedExternalMessage(validator, new ConsensusMessage { EchoMessage = echo });
            TrySendReadyMessageFromEchos();
            CheckResult();
        }

        private void HandleReadyMessage(ReadyMessage readyMessage, int validator)
        {
            var validatorPubKey = Wallet.EcdsaPublicKeySet[validator].ToHex();
            if (_readyMessages[validator] != null)
            {
                Logger.LogWarning($"{Id} received duplicate ready from validator {validator} ({validatorPubKey})");
                return;
            }

            Logger.LogTrace($"Protocol {Id} got READY message from {validator} ({validatorPubKey})");
            _readyMessages[validator] = readyMessage;
            InvokeReceivedExternalMessage(validator, new ConsensusMessage { ReadyMessage = readyMessage });
            TrySendReadyMessageFromReady();
            CheckResult();
            // Logger.LogDebug($"{Id}: got ready message from {validator}");
        }

        private void TrySendReadyMessageFromEchos()
        {
            if (_readySent) return;
            var (bestRootCnt, bestRoot) = _echoMessages
                .Where(x => x != null)
                .GroupBy(x => x!.MerkleTreeRoot)
                .Select(m => (cnt: m.Count(), key: m.Key))
                .OrderByDescending(x => x.cnt)
                .First();
            if (bestRootCnt != N - F) return;
            var interpolationData = _echoMessages
                .WithIndex()
                .Where(x => bestRoot.Equals(x.item?.MerkleTreeRoot))
                .Select(t => (echo: t.item!, t.index))
                .Take(N - 2 * F)
                .ToArray();
            var restoredData = DecodeFromEchos(interpolationData);
            var restoredMerkleTree = ConstructMerkleTree(
                restoredData
                    .Batch(interpolationData.First().echo.Data.Length)
                    .Select(x => x.ToArray())
                    .ToArray()
            );
            if (!restoredMerkleTree[1].Equals(bestRoot))
            {
                Logger.LogError($"{Id}: Interpolated result merkle root does not match!");
                Abort();
                return;
            }

            Broadcaster.Broadcast(CreateReadyMessage(bestRoot));
            _readySent = true;
            Logger.LogTrace($"Protocol {Id} got enough ECHOs and broadcasted READY message");
        }

        private void TrySendReadyMessageFromReady()
        {
            var (bestRootCnt, bestRoot) = _readyMessages
                .Where(x => x != null)
                .GroupBy(x => x!.MerkleTreeRoot)
                .Select(m => (cnt: m.Count(), key: m.Key))
                .OrderByDescending(x => x.cnt)
                .First();
            if (bestRootCnt != F + 1) return;
            if (_readySent) return;
            Broadcaster.Broadcast(CreateReadyMessage(bestRoot));
            _readySent = true;
            Logger.LogTrace($"Protocol {Id} got enough READYs and broadcasted READY message");
        }

        private void CheckResult()
        {
            if (_requested != ResultStatus.Requested) return;
            var (bestRootCnt, bestRoot) = _readyMessages
                .Where(x => x != null)
                .GroupBy(x => x!.MerkleTreeRoot)
                .Select(m => (cnt: m.Count(), key: m.Key))
                .OrderByDescending(x => x.cnt)
                .FirstOrDefault();
            if (bestRootCnt < 2 * F + 1) return;
            var matchingEchos = _echoMessages
                .WithIndex()
                .Where(t => bestRoot.Equals(t.item?.MerkleTreeRoot))
                .Select(t => (echo: t.item!, t.index))
                .Take(N - 2 * F)
                .ToArray();
            if (matchingEchos.Length < N - 2 * F) return;

            var restored = DecodeFromEchos(matchingEchos);
            var len = restored.AsReadOnlySpan().Slice(0, 4).ToInt32();
            var result = EncryptedShare.FromBytes(restored.AsMemory().Slice(4, len));

            // Share id of the encrypted share represents the validator id, each share has the same id as the from which
            // validator it came. This instance of ReliableBroadcast is handling shares of validator _broadcastId.SenderId
            // So the share we get here should have the same id, if not then there was some problem with decoding
            // it could be due to the validator for this ReliableBroadcast was malicious.
            // However if you don't stop this then in ACS, the encrypted share can replace other valid encrypted share
            // because of same share_id, so we stop it and ACS will take its result as null
            if (result.Id != _broadcastId.SenderId)
            {
                var pubKey = Broadcaster.GetPublicKeyById(_broadcastId.SenderId)!.ToHex();
                Logger.LogInformation($"Got encrypted share with share id {result.Id} in ReliableBroadcast {Id}");
                Logger.LogInformation($"Validator {pubKey} ({_broadcastId.SenderId}) might be malicious");
                throw new Exception($"Invalid encrypted share for {Id}");
            }
            _requested = ResultStatus.Sent;
            Broadcaster.InternalResponse(new ProtocolResult<ReliableBroadcastId, EncryptedShare>(_broadcastId, result));
        }

        private void Abort()
        {
            Logger.LogError($"{Id} was aborted!");
            Terminate();
        }

        private bool CheckEchoMessage(ECHOMessage msg, int from)
        {
            var value = msg.Data.Keccak();
            // We can get exception here if the size of msg.MerkleProof is less then the depth of MerkleTree
            int i, j;
            for (i = from + _merkleTreeSize, j = 0; i > 1 && j < msg.MerkleProof.Count; i /= 2, ++j)
            {
                value = (i & 1) == 0
                    ? value.ToBytes().Concat(msg.MerkleProof[j].ToBytes()).Keccak() // we are left sibling
                    : msg.MerkleProof[j].ToBytes().Concat(value.ToBytes()).Keccak(); // we are right sibling
            }

            return msg.MerkleTreeRoot.Equals(value) && i == 1; // it reached the root and matches the root
        }

        private void AugmentInput(List<byte> input)
        {
            var sz = input.Count;
            input.InsertRange(0, sz.ToBytes());
            var dataShards = N - 2 * F;
            var shardSize = (input.Count + dataShards - 1) / dataShards;
            input.AddRange(Enumerable.Repeat<byte>(0, dataShards * shardSize - input.Count));
            Debug.Assert(input.Count == dataShards * shardSize);
        }

        private ValMessage[] ConstructValMessages(IReadOnlyList<byte> input)
        {
            var shards = ErasureCodingShards(input, N, 2 * F);
            var merkleTree = ConstructMerkleTree(shards);
            var result = new ValMessage[N];
            for (var i = 0; i < N; ++i)
            {
                result[i] = new ValMessage
                {
                    SenderId = _broadcastId.SenderId,
                    MerkleTreeRoot = merkleTree[1],
                    MerkleProof = {MerkleTreeBranch(merkleTree, i)},
                    Data = ByteString.CopyFrom(shards[i]),
                };
            }

            return result;
        }

        private ConsensusMessage CreateEchoMessage(ValMessage valMessage)
        {
            return new ConsensusMessage
            {
                EchoMessage = new ECHOMessage
                {
                    SenderId = _broadcastId.SenderId,
                    MerkleTreeRoot = valMessage.MerkleTreeRoot,
                    Data = valMessage.Data,
                    MerkleProof = {valMessage.MerkleProof}
                }
            };
        }

        private ConsensusMessage CreateReadyMessage(UInt256 merkleRoot)
        {
            return new ConsensusMessage
            {
                ReadyMessage = new ReadyMessage
                {
                    SenderId = _broadcastId.SenderId,
                    MerkleTreeRoot = merkleRoot,
                }
            };
        }

        private static List<UInt256> MerkleTreeBranch(IReadOnlyList<UInt256> tree, int i)
        {
            var n = tree.Count / 2;
            var result = new List<UInt256>();
            for (i += n; i > 1; i /= 2) // go up in Merkle tree, div 2 means go to parent
                result.Add(tree[i ^ 1]); // xor 1 means take sibling
            return result;
        }

        private UInt256[] ConstructMerkleTree(IReadOnlyCollection<IReadOnlyCollection<byte>> shards)
        {
            Debug.Assert(shards.Count == N);
            var result = new UInt256[_merkleTreeSize * 2];
            foreach (var (shard, i) in shards.WithIndex())
                result[_merkleTreeSize + i] = shard.Keccak();
            for (var i = shards.Count; i < _merkleTreeSize; ++i)
                result[_merkleTreeSize + i] = UInt256Utils.Zero;
            for (var i = _merkleTreeSize - 1; i >= 1; --i)
                result[i] = result[2 * i].ToBytes().Concat(result[2 * i + 1].ToBytes()).Keccak();
            return result;
        }

        /**
         * Split arbitrary array into specified number of shards adding some parity shards
         * After this operation whole array can be recovered if specified number of shards is lost
         * Array length is assumed to be divisible by (shards - erasures)
         */
        public static byte[][] ErasureCodingShards(IReadOnlyList<byte> input, int shards, int erasures)
        {
            var dataShards = shards - erasures;
            if (input.Count % dataShards != 0) throw new InvalidOperationException();
            var shardSize = input.Count / dataShards;
            if (erasures == 0) return input.Batch(shardSize).Select(x => x.ToArray()).ToArray();
            var result = new byte[shards][];
            var erasureCoding = new ErasureCoding();
            foreach (var (shard, i) in input
                .Batch(shardSize)
                .WithIndex()
            ) result[i] = shard.ToArray();

            for (var i = dataShards; i < shards; ++i) result[i] = new byte[shardSize];

            for (var i = 0; i < shardSize; ++i)
            {
                var codeword = new int[shards];
                for (var j = 0; j < dataShards; j++)
                    codeword[j] = input[i + j * shardSize];
                erasureCoding.Encode(codeword, erasures);
                for (var j = dataShards; j < shards; ++j)
                    result[j][i] = checked((byte) codeword[j]);
            }

            return result;
        }

        public byte[] DecodeFromEchos(IReadOnlyCollection<(ECHOMessage echo, int from)> echos)
        {
            Debug.Assert(echos.Count == N - 2 * F);
            Debug.Assert(echos.Select(t => t.echo.Data.Length).Distinct().Count() == 1);
            var shardLength = echos.First().echo.Data.Length;
            var result = new byte[shardLength * N];
            foreach (var (echo, i) in echos)
                Buffer.BlockCopy(echo.Data.ToArray(), 0, result, i * shardLength, shardLength);
            if (F == 0) return result;
            var erasureCoding = new ErasureCoding();
            for (var i = 0; i < shardLength; ++i)
            {
                var codeword = new int[N];
                for (var j = 0; j < N; ++j) codeword[j] = result[i + j * shardLength];
                var erasurePlaces = Enumerable.Range(0, N)
                    .Where(z => !echos.Select(t => t.from).Contains(z))
                    .ToArray();
                Debug.Assert(erasurePlaces.Length == 2 * F);
                erasureCoding.Decode(codeword, 2 * F, erasurePlaces);
                for (var j = 0; j < N; ++j) result[i + j * shardLength] = checked((byte) codeword[j]);
            }

            return result;
        }
    }
}