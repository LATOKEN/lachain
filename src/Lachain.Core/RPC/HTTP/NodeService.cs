using System.Diagnostics;
using AustinHarris.JsonRpc;
using Lachain.Core.Network;
using Newtonsoft.Json.Linq;
using Lachain.Utility.Utils;

namespace Lachain.Core.RPC.HTTP
{
    public class NodeService : JsonRpcService
    {
        private readonly ulong _startTs;
        private readonly IBlockSynchronizer _blockSynchronizer;
        
        public NodeService(IBlockSynchronizer blockSynchronizer)
        {
            _blockSynchronizer = blockSynchronizer;
            _startTs =  TimeUtils.CurrentTimeMillis();
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

            // TODO: remove mock
            if (peers.Length == 0)
            {
                for (var i = 0; i < 10; i++)
                {
                    var peerJObject = new JObject
                    {
                        ["publicKey"] = "1",
                        ["host"] = "1",
                        ["port"] = "1",
                        ["protocol"] = "1",
                    };
                    result.Add(peerJObject);
                }
            }

            return result;
        }
        
        [JsonRpcMethod("net_version")]
        private string GetNodeVersion()
        {
            return "0.100.0";
        }
    }
}