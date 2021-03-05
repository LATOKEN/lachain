using System.Diagnostics;
using System.Reflection;
using AustinHarris.JsonRpc;
using Lachain.Core.BlockchainFilter;
using Lachain.Core.Network;
using Lachain.Core.RPC.HTTP.Web3;
using Lachain.Networking;
using Lachain.Utility.Utils;
using Newtonsoft.Json.Linq;

namespace Lachain.Core.RPC.HTTP
{
    public class NodeService : JsonRpcService
    {
        private readonly ulong _startTs;
        private readonly IBlockSynchronizer _blockSynchronizer;
        private readonly IBlockchainEventFilter _blockchainEventFilter;

        public NodeService(
            IBlockSynchronizer blockSynchronizer,
            IBlockchainEventFilter blockchainEventFilter,
            INetworkManager networkManager
        )
        {
            _blockchainEventFilter = blockchainEventFilter;
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
            
            foreach (var (ecdsaPublicKey, height) in peers)
            {
                var peerJObject = new JObject
                {
                    ["publicKey"] = ecdsaPublicKey!.ToHex(),
                    ["height"] = height,
                };
                result.Add(peerJObject);
            }
            
            return result;
        }

        [JsonRpcMethod("net_peerCount")]
        private string GetPeerCount()
        {
            return 0.ToHex();
            // var peers = _networkManager.GetConnectedPeers().ToArray();
            // return peers.Length.ToHex();
        }

        [JsonRpcMethod("net_listening")]
        private bool IsListening()
        {
            return true;
        }

        private static string GetVersion()
        {
            return Assembly.GetEntryAssembly()!
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                .InformationalVersion;
        }

        [JsonRpcMethod("web3_clientVersion")]
        private string GetWeb3ClientVersion()
        {
            return $"Lachain/v{GetVersion()}/linux-x64/.NetSDK3.1";
        }

        [JsonRpcMethod("eth_protocolVersion")]
        private string GetProtocolVersion()
        {
            return GetVersion();
        }

        [JsonRpcMethod("eth_newFilter")]
        private string SetFilter(JObject opt)
        {
            return Web3DataFormatUtils.Web3Number(_blockchainEventFilter.Create(BlockchainEvent.Block));
        }

        [JsonRpcMethod("eth_newBlockFilter")]
        private string SetBlockFilter()
        {
            return Web3DataFormatUtils.Web3Number(_blockchainEventFilter.Create(BlockchainEvent.Block));
        }

        [JsonRpcMethod("eth_newPendingTransactionFilter")]
        private string SetPendingTransactionFilter()
        {
            return Web3DataFormatUtils.Web3Number(_blockchainEventFilter.Create(BlockchainEvent.Transaction));
        }

        [JsonRpcMethod("eth_uninstallFilter")]
        private bool UnsetFilter(string filterId)
        {
            var id = filterId.HexToUlong();
            return _blockchainEventFilter.Remove(id);
        }

        [JsonRpcMethod("eth_getFilterChanges")]
        private JArray GetFilterUpdates(string filterId)
        {
            var id = filterId.HexToUlong();
            var updates = _blockchainEventFilter.Sync(id);
            var result = new JArray();
            foreach (var e in updates)
                result.Add(e);
            return result;
        }

        [JsonRpcMethod("eth_getFilterLogs")]
        private JArray GetFilterLogs(string filterId)
        {
            return new JArray();
        }

        [JsonRpcMethod("eth_getLogs")]
        private JArray GetLogs(JObject opts)
        {
            return new JArray();
        }

        [JsonRpcMethod("eth_hashrate")]
        private string GetHashrate()
        {
            return "0x38";
        }
    }
}