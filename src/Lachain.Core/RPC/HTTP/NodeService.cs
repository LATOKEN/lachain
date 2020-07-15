using System.Diagnostics;
using AustinHarris.JsonRpc;
using Lachain.Core.BlockchainFilter;
using Lachain.Core.Network;
using Newtonsoft.Json.Linq;
using Lachain.Utility.Utils;

namespace Lachain.Core.RPC.HTTP
{
    public class NodeService : JsonRpcService
    {
        private readonly ulong _startTs;
        private readonly IBlockSynchronizer _blockSynchronizer;
        private readonly IBlockchainEventFilter _blockchainEventFilter;

        public NodeService(IBlockSynchronizer blockSynchronizer, IBlockchainEventFilter blockchainEventFilter)
        {
            _blockSynchronizer = blockSynchronizer;
            _blockchainEventFilter = blockchainEventFilter;
            _startTs = TimeUtils.CurrentTimeMillis();
        }

        [JsonRpcMethod("getNodeStats")]
        private JObject GetNodeStats()
        {
            using var process = Process.GetCurrentProcess();
            return new JObject
            {
                ["uptime"] = TimeUtils.CurrentTimeMillis() - _startTs,
                ["threads"] = process.Threads.Count,
                ["memory"] = process.WorkingSet64,
                ["max_memory"] = process.PeakWorkingSet64,
            };
        }

        [JsonRpcMethod("net_peers")]
        private JArray GetConnectedPeers()
        {
            var peers = _blockSynchronizer.GetConnectedPeers();
            var result = new JArray();

            foreach (var peer in peers)
            {
                var peerJObject = new JObject
                {
                    ["publicKey"] = peer.PublicKey!.ToHex(),
                    ["host"] = peer.Host,
                    ["port"] = peer.Port,
                    ["protocol"] = peer.Protocol.ToString(),
                };
                result.Add(peerJObject);
            }

            return result;
        }

        [JsonRpcMethod("net_peerCount")]
        private string GetPeerCount()
        {
            var peers = _blockSynchronizer.GetConnectedPeers();
            return peers.Length.ToHex();
        }

        [JsonRpcMethod("net_listening")]
        private bool IsListening()
        {
            return true;
        }
        
        [JsonRpcMethod("net_version")]
        public string GetNetVersion()
        {
            return "0.100.0";
        }

        [JsonRpcMethod("web3_clientVersion")]
        private string GetWeb3ClientVersion()
        {
            return "Lachain/v0.0.0-test6/linux-x64/.NetSDK3.1";
        }

        [JsonRpcMethod("eth_protocolVersion")]
        private string GetProtocolVersion()
        {
            return "v0.0.0-test6";
        }

        [JsonRpcMethod("eth_newBlockFilter")]
        private string SetBlockFilter()
        {
            return _blockchainEventFilter.Create(BlockchainEvent.Block).ToHex();
        }

        [JsonRpcMethod("eth_uninstallFilter")]
        private bool UnsetBlockFilter(string filterId)
        {
            var id = filterId.HexToUlong();
            return _blockchainEventFilter.Remove(id);
        }

        [JsonRpcMethod("eth_getFilterChanges")]
        private string[] GetFilterUpdates(string filterId)
        {
            var id = filterId.HexToUlong();
            return _blockchainEventFilter.Sync(id);
        }

        [JsonRpcMethod("eth_hashrate")]
        private string GetHashrate()
        {
            return "0x38";
        }
    }
}