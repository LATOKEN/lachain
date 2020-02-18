using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Phorkus.Consensus;
using Phorkus.Consensus.BinaryAgreement;
using Phorkus.Consensus.CommonCoin;
using Phorkus.Consensus.CommonSubset;
using Phorkus.Consensus.HoneyBadger;
using Phorkus.Consensus.Messages;
using Phorkus.Consensus.ReliableBroadcast;
using Phorkus.Consensus.TPKE;
using Phorkus.Core.Blockchain;
using Phorkus.Crypto;
using Phorkus.Logger;
using Phorkus.Networking;
using Phorkus.Proto;
using Phorkus.Utility.Utils;
using MessageEnvelope = Phorkus.Consensus.Messages.MessageEnvelope;

namespace Phorkus.Core.Consensus
{
    public class RootProtocol : AbstractProtocol
    {
        private readonly ILogger<RootProtocol> _logger = LoggerFactory.GetLoggerForClass<RootProtocol>();
        private readonly IBlockProducer _blockProducer;
        private readonly ECDSAPublicKey _publicKey;
        private readonly RootProtocolId _rootId;

        public override void ProcessMessage(MessageEnvelope envelope)
        {
            if (envelope.External)
                throw new InvalidOperationException("Root protocol does not accept external messages");
            var message = envelope.InternalMessage;
            switch (message)
            {
                case ProtocolRequest<RootProtocolId, object> _:

                    using (var stream = new MemoryStream())
                    {
                        foreach (var hash in _blockProducer.GetTransactionsToPropose().Select(tx => tx.Hash))
                        {
                            Debug.Assert(hash.Buffer.ToByteArray().Length == 32);
                            stream.Write(hash.Buffer.ToByteArray(), 0, 32);
                        }

                        var data = stream.GetBuffer();
                        Broadcaster.InternalRequest(new ProtocolRequest<HoneyBadgerId, IRawShare>(
                            Id, new HoneyBadgerId(Id.Era), new RawShare(data, GetMyId()))
                        );
                    }

                    break;
                case ProtocolResult<RootProtocolId, object?> _:
                    Terminate();
                    break;
                case ProtocolResult<HoneyBadgerId, ISet<IRawShare>> result:
                    _logger.LogDebug($"Received shares {result.Result.Count} from HoneyBadger");
                    var txHashes = result.Result.ToArray()
                        .SelectMany(share => SplitShare(share.ToBytes()))
                        .Select(hash => hash.ToUInt256())
                        .Distinct()
                        .ToArray();
                    _logger.LogDebug($"Collected {txHashes.Length} transactions in total");
                    _blockProducer.ProduceBlock(txHashes, _publicKey, 0); // TODO: nonce
                    Broadcaster.InternalResponse(new ProtocolResult<RootProtocolId, object?>(_rootId, null));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(message));
            }
        }

        private IEnumerable<byte[]> SplitShare(byte[] share)
        {
            for (var i = 0; i < share.Length; i += 32)
            {
                yield return share.Skip(i).Take(32).ToArray();
            }
        }

        public RootProtocol(
            IWallet wallet, RootProtocolId id, IConsensusBroadcaster broadcaster,
            IBlockProducer blockProducer, ECDSAPublicKey publicKey
        ) : base(wallet, id, broadcaster)
        {
            _rootId = id;
            _blockProducer = blockProducer;
            _publicKey = publicKey;
        }
    }
}