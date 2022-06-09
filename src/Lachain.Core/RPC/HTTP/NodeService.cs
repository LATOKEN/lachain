using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using AustinHarris.JsonRpc;
using Lachain.Core.BlockchainFilter;
using Lachain.Core.Network;
using Lachain.Networking;
using Newtonsoft.Json.Linq;
using Lachain.Utility.Utils;
using Lachain.Storage.State;
using Lachain.Core.Blockchain.Interface;
using Lachain.Logger;
using Lachain.Proto;


namespace Lachain.Core.RPC.HTTP
{
    public class NodeService : JsonRpcService
    {
        private static readonly ILogger<NodeService> Logger =
            LoggerFactory.GetLoggerForClass<NodeService>();
        private readonly ulong _startTs;
        private readonly IBlockSynchronizer _blockSynchronizer;
        private readonly IBlockchainEventFilter _blockchainEventFilter;
        private readonly IBlockManager _blockManager;

        public NodeService(
            IBlockSynchronizer blockSynchronizer,
            IBlockchainEventFilter blockchainEventFilter,
            INetworkManager networkManager,
            IBlockManager blockManager
        )
        {
            _blockchainEventFilter = blockchainEventFilter;
            _blockSynchronizer = blockSynchronizer;
            _blockManager = blockManager;
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
            var fromBlock = opt["fromBlock"];
            var toBlock = opt["toBlock"];
            var address = opt["address"];
            var topicsJson = opt["topics"];

            ulong? start = _blockManager.GetHeight();
            ulong? finish = _blockManager.GetHeight();

            if(!(fromBlock is null)) start = GetBlockNumberByTag((string)fromBlock!);
            if(!(toBlock is null)) finish = GetBlockNumberByTag((string)toBlock!);

            var addresses = new List<UInt160>();
            if(!(address is null)) addresses = BlockchainFilterUtils.GetAddresses((JArray)address);

            var allTopics = new List<List<UInt256>>();
            if (!(topicsJson is null))
            {
                allTopics = BlockchainFilterUtils.GetTopics(topicsJson);
            }
            while(allTopics.Count < 4) allTopics.Add(new List<UInt256>());

            return Web3.Web3DataFormatUtils.Web3Number(
                _blockchainEventFilter.Create(BlockchainEvent.Logs, start, finish, addresses , allTopics)
            );
        }

        [JsonRpcMethod("eth_newBlockFilter")]
        private string SetBlockFilter()
        {
            return Web3.Web3DataFormatUtils.Web3Number(_blockchainEventFilter.Create(BlockchainEvent.Block));
        }

        [JsonRpcMethod("eth_newPendingTransactionFilter")]
        private string SetPendingTransactionFilter()
        {
            return Web3.Web3DataFormatUtils.Web3Number(_blockchainEventFilter.Create(BlockchainEvent.Transaction));
        }

        [JsonRpcMethod("eth_uninstallFilter")]
        private bool UnsetFilter(string filterId)
        {
            _blockchainEventFilter.RemoveUnusedFilters();
            var id = filterId.HexToUlong();
            return _blockchainEventFilter.Remove(id);
        }

        [JsonRpcMethod("eth_getFilterChanges")]
        private JArray GetFilterUpdates(string filterId)
        {
            var id = filterId.HexToUlong();
            return _blockchainEventFilter.Sync(id,true);
        }

        [JsonRpcMethod("eth_getFilterLogs")]
        private JArray GetFilterLogs(string filterId)
        {
            var id = filterId.HexToUlong();
            return _blockchainEventFilter.Sync(id,false);
        }
        [JsonRpcMethod("eth_hashrate")]
        private string GetHashrate()
        {
            return "0x38";
        }

        private ulong? GetBlockNumberByTag(string blockTag)
        {
            return blockTag switch
            {
                "latest" => _blockManager.GetHeight(),
                "earliest" => 0,
                "pending" => null,
                _ => blockTag.HexToUlong()
            };
        }
    }
}