using System.Diagnostics;
using AustinHarris.JsonRpc;
using Newtonsoft.Json.Linq;
using Lachain.Utility.Utils;

namespace Lachain.Core.RPC.HTTP
{
    public class NodeService : JsonRpcService
    {
        private readonly ulong _startTs;
        
        public NodeService()
        {
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
    }
}