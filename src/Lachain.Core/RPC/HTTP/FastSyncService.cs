using System;
using System.Linq;
using System.Numerics;
using AustinHarris.JsonRpc;
using Lachain.Core.Blockchain.Interface;
using System.Collections.Generic;
using Lachain.Core.Config;
using Lachain.Core.Network;
using Lachain.Logger;
using Lachain.Proto;
using Lachain.Storage;
using Lachain.Storage.Repositories;
using Lachain.Storage.State;
using Lachain.Storage.Trie;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Lachain.Core.RPC.HTTP
{
    public class FastSyncService : JsonRpcService
    {
        private static readonly ILogger<FastSyncService> Logger = LoggerFactory.GetLoggerForClass<FastSyncService>();
        private readonly IStateManager _stateManager;
        private readonly IBlockManager _blockManager;
        private readonly IBlockSynchronizer _blockSynchronizer;
        private readonly IRocksDbContext _rocksDbContext;
        private readonly NodeRepository _nodeRepository;
        private readonly VersionRepository _versionRepository;
        private readonly IConfigManager _configManager;

        private Dictionary<ulong, Tuple<List<ulong>, List<byte[]>>> _repoBlocks =
            new Dictionary<ulong, Tuple<List<ulong>, List<byte[]>>>();

        private bool _fastSyncStatus = false;

        public FastSyncService(
            IStateManager stateManager,
            IBlockManager blockManager,
            IRocksDbContext rocksDbContext,
            IBlockSynchronizer blockSynchronizer,
            IConfigManager configManager
        )
        {
            _stateManager = stateManager;
            _blockManager = blockManager;
            _rocksDbContext = rocksDbContext;
            _blockSynchronizer = blockSynchronizer;

            _nodeRepository = new NodeRepository(_rocksDbContext);
            _versionRepository = new VersionRepository(_rocksDbContext);

            _configManager = configManager;
        }

        private void GetSerializedNodesAndIds(ulong repoType)
        {
            Logger.LogTrace($"Start: getSerializedNodesAndIds");

            ulong _version = 0;

            switch (repoType)
            {
                case 1:
                    _version = _stateManager.CurrentSnapshot.Balances.Version;
                    break;
                case 4:
                    _version = _stateManager.CurrentSnapshot.Contracts.Version;
                    break;
                case 5:
                    _version = _stateManager.CurrentSnapshot.Storage.Version;
                    break;
                case 6:
                    _version = _stateManager.CurrentSnapshot.Transactions.Version;
                    break;
                case 7:
                    _version = _stateManager.CurrentSnapshot.Blocks.Version;
                    break;
                case 9:
                    _version = _stateManager.CurrentSnapshot.Events.Version;
                    break;
                case 10:
                    _version = _stateManager.CurrentSnapshot.Validators.Version;
                    break;
                default:
                    break;
            }

            var versionFactory = new VersionFactory(_versionRepository.GetVersion(Convert.ToUInt32(repoType)));
            var trieHashMap = new TrieHashMap(_nodeRepository, versionFactory);

            if (trieHashMap.GetNodeIds(_version).ToList().Count > 0)
            {
                var t = Tuple.Create(trieHashMap.GetNodeIds(_version).ToList(),
                    trieHashMap.GetSerializedNodes(_version).ToList());

                _repoBlocks.Add(repoType, t);    
            }

            Logger.LogTrace($"End: getSerializedNodesAndIds");
        }

        [JsonRpcMethod("getBlocks")]
        private JObject? GetBlocks(ulong repoType, int offset)
        {
            Logger.LogTrace($"Start: RetrieveValues");

            try
            {
                if (!_repoBlocks.ContainsKey(repoType))
                    GetSerializedNodesAndIds(Convert.ToUInt32(repoType));

                if (_repoBlocks.ContainsKey(repoType))
                {
                    if (_repoBlocks[repoType].Item1.Count == 0)
                    {
                        return new JObject
                        {
                            ["success"] = "true",
                            ["message"] = "no blocks to fetch",
                            ["repoType"] = repoType,
                            ["total_blocks"] = _repoBlocks[repoType].Item1.Count,
                            ["old_offset"] = offset,
                            ["new_offset"] = offset,
                            ["ids"] = null,
                            ["values"] = null
                        };
                    }

                    if (offset > _repoBlocks[repoType].Item1.Count)
                    {
                        return new JObject
                        {
                            ["success"] = "true",
                            ["message"] = "offset is higher than the total values",
                            ["repoType"] = repoType,
                            ["total_blocks"] = _repoBlocks[repoType].Item1.Count,
                            ["old_offset"] = offset,
                            ["new_offset"] = offset,
                            ["ids"] = null,
                            ["values"] = null
                        };
                    }

                    List<ulong> resId;
                    List<byte[]> resVal;

                    if ((_repoBlocks[repoType].Item1.Count - offset) > 500)
                    {
                        resId = _repoBlocks[repoType].Item1.GetRange(offset, 500);
                        resVal = _repoBlocks[repoType].Item2.GetRange(offset, 500);
                    }
                    else
                    {
                        resId = _repoBlocks[repoType].Item1
                            .GetRange(offset, _repoBlocks[repoType].Item1.Count - offset);
                        resVal = _repoBlocks[repoType].Item2
                            .GetRange(offset, _repoBlocks[repoType].Item2.Count - offset);
                    }

                    var retObj = new JObject
                    {
                        ["success"] = "true",
                        ["message"] = "OK",
                        ["repoType"] = repoType,
                        ["total_blocks"] = _repoBlocks[repoType].Item1.Count,
                        ["old_offset"] = offset,
                        ["new_offset"] = offset + 500,
                        ["ids"] = JArray.Parse(JsonConvert.SerializeObject(resId)),
                        ["values"] = JArray.Parse(JsonConvert.SerializeObject(resVal))
                    };

                    Logger.LogTrace($"End: RetrieveValues");

                    return retObj;
                }
                else
                {
                    Logger.LogTrace($"End: RetrieveValues - No Blocks for Repo");
                    return new JObject
                    {
                        ["success"] = "true",
                        ["message"] = "no blocks for repo",
                        ["repoType"] = repoType,
                        ["total_blocks"] = 0,
                        ["old_offset"] = offset,
                        ["new_offset"] = offset,
                        ["ids"] = null,
                        ["values"] = null
                    };
                }
            }
            catch (Exception exp)
            {
                Logger.LogTrace($"End: RetrieveValues - Error {exp.Message}");
                return new JObject
                {
                    ["success"] = "true",
                    ["message"] = exp.Message,
                    ["repoType"] = repoType,
                    ["total_blocks"] = 0,
                    ["old_offset"] = offset,
                    ["new_offset"] = offset,
                    ["ids"] = null,
                    ["values"] = null
                };
            }
        }

        [JsonRpcMethod("getMetaVersion")]
        private JObject? GetMetaVersion()
        {
            Logger.LogTrace($"Start: GetMetaVersion");
            try
            {
                var metaVersionFactory = new VersionFactory(_versionRepository.GetVersion(0));

                Logger.LogTrace($"End: GetMetaVersion");
                return new JObject
                {
                    ["success"] = "true",
                    ["message"] = "OK",
                    ["Meta"] = metaVersionFactory.CurrentVersion,
                };
            }
            catch (System.Exception exp)
            {
                Logger.LogTrace($"End: GetMetaVersion - Error {exp.Message}");
                return new JObject
                {
                    ["success"] = "false",
                    ["message"] = exp.Message
                };
            }
        }

        [JsonRpcMethod("getRPCList")]
        private JObject? GetRPCList()
        {
            try
            {
                Logger.LogTrace($"Start: GetRPCList");

                List<string> tmp;
                if (_blockSynchronizer.GetRpcPeers().Count == 0)
                {
                    tmp = _configManager.GetConfig<RpcConfig>("rpc")!.Peers!.ToList();
                    _blockSynchronizer.SetRpcPeers(tmp);
                }
                else
                {
                    Logger.LogTrace($"inside else");
                    tmp = _blockSynchronizer.GetRpcPeers();
                    Logger.LogTrace($"tmp: {string.Join(",", tmp)}");
                }

                Logger.LogTrace($"End: GetRPCList");

                return new JObject
                {
                    ["success"] = "true",
                    ["message"] = "OK",
                    ["peers"] = JArray.Parse(JsonConvert.SerializeObject(tmp))
                };
            }
            catch (System.Exception exp)
            {
                Logger.LogTrace($"End: GetRPCList - Error {exp.Message}");
                return new JObject
                {
                    ["success"] = "false",
                    ["message"] = exp.Message
                };
            }
        }

        [JsonRpcMethod("updateRPCList")]
        private JObject? UpdateRPCList(string rpcUrl)
        {
            try
            {
                Logger.LogTrace($"Start: UpdateRPCList");

                if (!_blockSynchronizer.GetRpcPeers().Contains(rpcUrl))
                {
                    _blockSynchronizer.SetRpcPeers(new List<string>() {rpcUrl});
                }

                Logger.LogTrace($"End: UpdateRPCList");

                return new JObject
                {
                    ["success"] = "true",
                    ["message"] = "OK"
                };
            }
            catch (Exception exp)
            {
                return new JObject
                {
                    ["success"] = "false",
                    ["message"] = exp.Message
                };
            }
        }

        [JsonRpcMethod("getLatestStatus")]
        private JObject? GetLatestStatus()
        {
            try
            {
                Logger.LogTrace($"Start: GetLatestStatus");

                Block? latestBlock = _blockManager.LatestBlock();
                var res = _blockManager.Verify(latestBlock);

                Logger.LogTrace($"End: GetLatestStatus");

                return new JObject
                {
                    ["success"] = "true",
                    ["message"] = "OK",
                    ["Height"] = _blockManager.GetHeight(),
                    ["Hash"] = latestBlock.Hash.ToString(),
                    ["Balance_Version"] = _stateManager.CurrentSnapshot.Balances.Version,
                    ["Contract_Version"] = _stateManager.CurrentSnapshot.Contracts.Version,
                    ["Storage_Version"] = _stateManager.CurrentSnapshot.Storage.Version,
                    ["Transaction_Version"] = _stateManager.CurrentSnapshot.Transactions.Version,
                    ["Block_Version"] = _stateManager.CurrentSnapshot.Blocks.Version,
                    ["Event_Version"] = _stateManager.CurrentSnapshot.Events.Version,
                    ["Validator_Version"] = _stateManager.CurrentSnapshot.Validators.Version,
                    ["LastApprovedSS"] = _stateManager.LastApprovedSnapshot.StateHash.ToString()
                };
            }
            catch (System.Exception exp)
            {
                Logger.LogTrace($"End: GetLatestStatus - Error {exp.Message}");
                return new JObject
                {
                    ["success"] = "false",
                    ["message"] = exp.Message
                };
            }
        }
        
        [JsonRpcMethod("handShake")]
        private JObject? HandShake()
        {
            try
            {
                Logger.LogDebug($"Start: HandShake");
                JObject? returnObj;

                if (_fastSyncStatus)
                {
                    Logger.LogDebug($"End: HandShake - 'false'");
                    returnObj = new JObject
                    {
                        ["success"] = "true",
                        ["ready"] = "false"
                    };    
                }
                else
                {
                    _fastSyncStatus = true;
                    Logger.LogDebug($"End: HandShake - 'true'");
                    
                    returnObj = new JObject
                    {
                        ["success"] = "true",
                        ["ready"] = "true"
                    }; 
                }

                return returnObj;
            }
            catch (Exception exp)
            {
                return new JObject
                {
                    ["success"] = "false",
                    ["message"] = exp.Message
                };
            }
        }


        [JsonRpcMethod("getSnapShot")]
        private JObject? GetSnapShot(ulong blockNumber)
        {
            StorageManager storageManager = new StorageManager(_rocksDbContext);
            SnapshotIndexRepository snapshotIndexRepository =
                    new SnapshotIndexRepository(_rocksDbContext, storageManager);
                
            IBlockchainSnapshot bs = snapshotIndexRepository.GetSnapshotForBlock(blockNumber);
            var res = bs.StateHash.ToString();

            Block b = _blockManager.GetByHeight(blockNumber+1);
            var b_hash = b.Header.StateHash.ToString();

            

            return new JObject
            {
                ["success"] = "true",
                ["stateHash"] = res,
                ["block"] = blockNumber,
                ["b_hash"] = b_hash,
                ["next_block"] = blockNumber + 1
            };
        }
    }
}