using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
// using System.Data;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
// using System.Runtime.InteropServices;
// using System.Runtime.Serialization;
// using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Web;
// using System.Threading.Tasks;
// using Google.Protobuf;
using Lachain.Logger;
using Lachain.Core.Blockchain.Error;
using Lachain.Core.Blockchain.Interface;
using Lachain.Core.Blockchain.Pool;
using Lachain.Core.Config;
using Lachain.Core.RPC;
using Lachain.Networking;
using Lachain.Proto;
using Lachain.Storage;
using Lachain.Storage.Repositories;
// using Lachain.Storage.Repositories;
using Lachain.Storage.State;
using Lachain.Storage.Trie;
// using Lachain.Storage.Trie;
using Lachain.Utility.Utils;
using Nethereum.ABI.Model;
// using Nethereum.JsonRpc.Client;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NLog;
using NLog.Fluent;
using Secp256k1Net;
using WebAssembly.Instructions;

namespace Lachain.Core.Network
{
    public class BlockSynchronizer : IBlockSynchronizer
    {
        private static readonly ILogger<BlockSynchronizer>
            Logger = LoggerFactory.GetLoggerForClass<BlockSynchronizer>();

        private readonly ITransactionManager _transactionManager;
        private readonly IBlockManager _blockManager;
        private readonly INetworkBroadcaster _networkBroadcaster;
        private readonly INetworkManager _networkManager;
        private readonly ITransactionPool _transactionPool;
        private readonly IStateManager _stateManager;
        private IRocksDbContext _rocksDbContext;
        private IConfigManager _configManager;

        private readonly object _peerHasTransactions = new object();
        private readonly object _peerHasBlocks = new object();
        private readonly object _blocksLock = new object();
        private readonly object _txLock = new object();
        private LogLevel _logLevelForSync = LogLevel.Trace;
        private bool _running;
        private readonly Thread _blockSyncThread;
        private readonly Thread _pingThread;
        private bool _fastSyncFlag = false;
        private ulong _fastHeight = 0;

        private RpcConfig _rpcConfig = new RpcConfig();
        private List<string> _rpcPeers = new List<string>();
        private string _rpcURL;

        private Dictionary<ulong, Tuple<List<ulong>, List<byte[]>>> _repoBlocks =
            new Dictionary<ulong, Tuple<List<ulong>, List<byte[]>>>();

        private List<ulong> _repoList = new List<ulong>()
        {
            1, 4, 5, 6, 7, 9, 10
        };

        Dictionary<ulong, List<ulong>> _blockVersion = new Dictionary<ulong, List<ulong>>();


        private int _totalRetryCount = 5;

        private readonly IDictionary<ECDSAPublicKey, ulong> _peerHeights
            = new ConcurrentDictionary<ECDSAPublicKey, ulong>();

        public BlockSynchronizer(
            ITransactionManager transactionManager,
            IBlockManager blockManager,
            INetworkBroadcaster networkBroadcaster,
            INetworkManager networkManager,
            ITransactionPool transactionPool,
            IStateManager stateManager,
            IRocksDbContext rocksDbContext,
            IConfigManager configManager
        )
        {
            _transactionManager = transactionManager;
            _blockManager = blockManager;
            _networkBroadcaster = networkBroadcaster;
            _networkManager = networkManager;
            _transactionPool = transactionPool;
            _stateManager = stateManager;
            _rocksDbContext = rocksDbContext;
            _configManager = configManager;
            _blockSyncThread = new Thread(BlockSyncWorker);
            _pingThread = new Thread(PingWorker);
        }

        public uint WaitForTransactions(IEnumerable<UInt256> transactionHashes, TimeSpan timeout)
        {
            var txHashes = transactionHashes as UInt256[] ?? transactionHashes.ToArray();
            var lostTxs = _GetMissingTransactions(txHashes);
            var endWait = DateTime.UtcNow.Add(timeout);

            while (_GetMissingTransactions(txHashes).Count != 0)
            {
                const int maxPeersToAsk = 1;
                var maxHeight = _peerHeights.Values.Count == 0 ? 0 : _peerHeights.Values.Max();
                var rnd = new Random();
                var peers = _peerHeights
                    .Where(entry => entry.Value >= maxHeight)
                    .Select(entry => entry.Key)
                    .OrderBy(_ => rnd.Next())
                    .Take(maxPeersToAsk)
                    .ToArray();
                if (lostTxs.Count == 0) return (uint) txHashes.Length;
                Logger.LogTrace($"Sending query for {lostTxs.Count} transactions to {peers.Length} peers");
                var request = _networkManager.MessageFactory.SyncPoolRequest(lostTxs);
                foreach (var peer in peers) _networkManager.SendTo(peer, request);
                lock (_peerHasTransactions)
                    Monitor.Wait(_peerHasTransactions, TimeSpan.FromMilliseconds(5_000));
                if (DateTime.UtcNow.CompareTo(endWait) > 0) break;
            }

            return (uint) (txHashes.Length - (uint) _GetMissingTransactions(txHashes).Count);
        }

        public uint HandleTransactionsFromPeer(IEnumerable<TransactionReceipt> transactions, ECDSAPublicKey publicKey)
        {
            lock (_txLock)
            {
                var txs = transactions.ToArray();
                Logger.LogTrace($"Received {txs.Length} transactions from peer {publicKey.ToHex()}");
                var persisted = 0u;
                foreach (var tx in txs)
                {
                    if (tx.Signature.IsZero())
                    {
                        Logger.LogTrace($"Received zero-signature transaction: {tx.Hash.ToHex()}");
                        if (_transactionPool.Add(tx) == OperatingError.Ok)
                            persisted++;
                        continue;
                    }

                    var error = _transactionManager.Verify(tx);
                    if (error != OperatingError.Ok)
                    {
                        Logger.LogTrace($"Unable to verify transaction: {tx.Hash.ToHex()} ({error})");
                        continue;
                    }

                    error = _transactionPool.Add(tx);
                    if (error == OperatingError.Ok)
                        persisted++;
                    else
                        Logger.LogTrace($"Transaction {tx.Hash.ToHex()} not persisted: {error}");
                }

                lock (_peerHasTransactions)
                    Monitor.PulseAll(_peerHasTransactions);
                Logger.LogTrace($"Persisted {persisted} transactions from peer {publicKey.ToHex()}");
                return persisted;
            }
        }

        public bool HandleBlockFromPeer(BlockInfo blockWithTransactions, ECDSAPublicKey publicKey)
        {
            Logger.LogTrace("HandleBlockFromPeer");
            lock (_blocksLock)
            {
                var block = blockWithTransactions.Block;
                var receipts = blockWithTransactions.Transactions;

                Logger.LogDebug(
                    $"Got block {block.Header.Index} with hash {block.Hash.ToHex()} from peer {publicKey.ToHex()}");
                var myHeight = _blockManager.GetHeight();
                if (block.Header.Index != myHeight + 1)
                {
                    Logger.LogTrace(
                        $"Skipped block {block.Header.Index} from peer {publicKey.ToHex()}: our height is {myHeight}");
                    return false;
                }

                if (!block.TransactionHashes.ToHashSet().SetEquals(receipts.Select(r => r.Hash)))
                {
                    var needHashes = string.Join(", ", block.TransactionHashes.Select(x => x.ToHex()));
                    var gotHashes = string.Join(", ", receipts.Select(x => x.Hash.ToHex()));
                    Logger.LogTrace(
                        $"Skipped block {block.Header.Index} from peer {publicKey.ToHex()}: expected hashes [{needHashes}] got hashes [{gotHashes}]");
                    return false;
                }

                var error = _stateManager.SafeContext(() =>
                {
                    if (_blockManager.GetHeight() + 1 == block.Header.Index)
                        return _blockManager.Execute(block, receipts, commit: true, checkStateHash: true);
                    Logger.LogTrace(
                        $"We have blockchain with height {_blockManager.GetHeight()} but got block {block.Header.Index}");
                    return OperatingError.BlockAlreadyExists;
                });
                if (error == OperatingError.BlockAlreadyExists)
                {
                    Logger.LogTrace(
                        $"Skipped block {block.Header.Index} from peer {publicKey.ToHex()}: block already exists");
                    return true;
                }

                if (error != OperatingError.Ok)
                {
                    Logger.LogWarning(
                        $"Unable to persist block {block.Header.Index} (current height {_blockManager.GetHeight()}), got error {error}, dropping peer");
                    return false;
                }

                lock (_peerHasBlocks)
                    Monitor.PulseAll(_peerHasBlocks);
                return true;
            }
        }

        public void HandlePeerHasBlocks(ulong blockHeight, ECDSAPublicKey publicKey)
        {
            Logger.Log(_logLevelForSync, $"Peer {publicKey.ToHex()} has height {blockHeight}");
            lock (_peerHasBlocks)
            {
                if (_peerHeights.TryGetValue(publicKey, out var peerHeight) && blockHeight <= peerHeight)
                    return;
                _peerHeights[publicKey] = blockHeight;
                Monitor.PulseAll(_peerHasBlocks);
            }
        }

        public bool IsSynchronizingWith(IEnumerable<ECDSAPublicKey> peers)
        {
            var myHeight = _blockManager.GetHeight();
            if (myHeight > _networkManager.LocalNode.BlockHeight)
                _networkManager.LocalNode.BlockHeight = myHeight;
            var setOfPeers = peers.ToHashSet();
            if (setOfPeers.Count == 0) return false;

            lock (_peerHasBlocks)
                Monitor.Wait(_peerHasBlocks, TimeSpan.FromSeconds(1));
            var validatorPeers = _peerHeights
                .Where(entry => setOfPeers.Contains(entry.Key))
                .ToArray();
            Logger.LogDebug($"Got {validatorPeers.Length} connected out of {setOfPeers.Count}");
            if (validatorPeers.Length < setOfPeers.Count * 2 / 3)
                return true;
            if (!validatorPeers.Any()) return false;
            var maxHeight = validatorPeers.Max(v => v.Value);
            Logger.LogDebug($"Max height among peers: {maxHeight}, my height: {myHeight}");
            return myHeight < maxHeight;
        }

        public void SynchronizeWith(IEnumerable<ECDSAPublicKey> peers)
        {
            var peersArray = peers.ToArray();
            _logLevelForSync = LogLevel.Debug;
            Logger.LogDebug($"Synchronizing with peers: {string.Join(", ", peersArray.Select(x => x.ToHex()))}");
            while (IsSynchronizingWith(peersArray))
            {
                Thread.Sleep(TimeSpan.FromMilliseconds(1_000));
            }
        }

        public ulong? GetHighestBlock()
        {
            if (_peerHeights.Count == 0) return null;
            return _peerHeights.Max(v => v.Value);
        }

        public IDictionary<ECDSAPublicKey, ulong> GetConnectedPeers()
        {
            return _peerHeights;
        }

        private void PingWorker()
        {
            while (_running)
            {
                try
                {
                    var reply = _networkManager.MessageFactory.PingReply(
                        TimeUtils.CurrentTimeMillis(), _stateManager.LastApprovedSnapshot.Blocks.GetTotalBlockHeight()
                    );
                    _networkBroadcaster.Broadcast(reply);
                    Logger.LogTrace($"Broadcasted our height: {reply.PingReply.BlockHeight}");
                }
                catch (Exception e)
                {
                    Logger.LogError($"Error in ping worker: {e}");
                }

                Thread.Sleep(TimeSpan.FromMilliseconds(3_000));
            }
        }

        private void BlockSyncWorker()
        {
            var rnd = new Random();
            Logger.LogDebug("Starting block synchronization worker");
            while (_running)
            {
                try
                {
                    var myHeight = _blockManager.GetHeight();
                    if (myHeight > _networkManager.LocalNode.BlockHeight)
                        _networkManager.LocalNode.BlockHeight = myHeight;

                    if (_peerHeights.Count == 0)
                    {
                        Logger.LogWarning("Peer height map is empty, nobody responds to pings?");
                        Thread.Sleep(TimeSpan.FromMilliseconds(1_000));
                        continue;
                    }

                    var maxHeight = _peerHeights.Values.Max();
                    if (myHeight >= maxHeight)
                    {
                        Logger.LogTrace($"Nothing to do: my height is {myHeight} and peers are at {maxHeight}");
                        Thread.Sleep(TimeSpan.FromMilliseconds(1_000));
                        continue;
                    }

                    const int maxPeersToAsk = 1;
                    const int maxBlocksToRequest = 10;

                    var peers = _peerHeights
                        .Where(entry => entry.Value >= maxHeight)
                        .Select(entry => entry.Key)
                        .OrderBy(_ => rnd.Next())
                        .Take(maxPeersToAsk)
                        .ToArray();

                    var leftBound = myHeight + 1;
                    var rightBound = Math.Min(maxHeight, myHeight + maxBlocksToRequest);
                    Logger.LogTrace($"Sending query for blocks [{leftBound}; {rightBound}] to {peers.Length} peers");
                    foreach (var peer in peers)
                    {
                        _networkManager.SendTo(
                            peer, _networkManager.MessageFactory.SyncBlocksRequest(leftBound, rightBound)
                        );
                    }

                    Logger.LogDebug($"Sync End Time {DateTime.Now.ToShortTimeString()}");
                    var waitStart = TimeUtils.CurrentTimeMillis();
                    while (true)
                    {
                        lock (_peerHasBlocks)
                        {
                            Monitor.Wait(_peerHasBlocks, TimeSpan.FromMilliseconds(1_000));
                        }

                        if (TimeUtils.CurrentTimeMillis() - waitStart > 5_000) break;
                    }
                }
                catch (Exception e)
                {
                    Logger.LogError($"Error in block synchronizer: {e}");
                    Thread.Sleep(1_000);
                }
            }
        }

        /**
         * StartFastSync
         * Method to initiate the FastSync for any newly joined peer in the network
         */
        public void PerformFastSync()
        {
            try
            {
                RocksDbAtomicWrite rocksDbAtomicWrite = new RocksDbAtomicWrite(_rocksDbContext);
                var nodeRepository = new NodeRepository(_rocksDbContext);
                var versionRepository = new VersionRepository(_rocksDbContext);

                SetRpcUrl();
                var peerResult = _CallJsonRpcAPI("getRPCList", new JArray());
                var peers = JArray.Parse(JsonConvert.SerializeObject(peerResult!["peers"]));
                Logger.LogDebug($"tmp = {string.Join(", ", peers)}");

                SetRpcPeers(peers.ToObject<List<string>>()!);
                
                Logger.LogDebug($"RPC URL = {_rpcURL}");

                var handshakeSuccess = _HandShake();
                if (!handshakeSuccess)
                {
                    Logger.LogError($"No peer is available for FastSync");
                    return;
                }

                var fastSyncStart = DateTime.Now.ToString("HH:mm:ss.ffff");
                Logger.LogDebug($"Start: FastSync Start {fastSyncStart} ");

                _GetNodesForFastSync();
                PersistNodesForFastSync(nodeRepository, rocksDbAtomicWrite);
                var meta = GetMetaVersion();
                SetMetaVersion(versionRepository, rocksDbAtomicWrite, meta);
                
                _fastSyncFlag = true;
                _fastHeight = _blockManager.GetHeight();
                
                Logger.LogDebug($"Block Height: {_fastHeight}");

                _SetBlockVersions(_fastHeight);

                var p = JArray.Parse(@$"[{_fastHeight}]");
                var res = _CallJsonRpcAPI("getSnapShot", p);
                Logger.LogDebug($"7071: {res}");


                StorageManager storageManager = new StorageManager(_rocksDbContext);
                SnapshotIndexRepository snapshotIndexRepository =
                    new SnapshotIndexRepository(_rocksDbContext, storageManager);

                IBlockchainSnapshot bs = snapshotIndexRepository.GetSnapshotForBlock(_fastHeight);

                Logger.LogDebug($"7072");
                Logger.LogDebug($"stateHash: {bs.StateHash.ToString()}");
                Logger.LogDebug($"snapshot: {Convert.ToString(bs)}");
                Logger.LogDebug($"Balance_Version: {bs.Balances.Version.ToString()}");
                Logger.LogDebug($"Contract_Version: {bs.Contracts.Version.ToString()}");
                Logger.LogDebug($"Storage_Version: {bs.Storage.Version.ToString()}");
                Logger.LogDebug($"Transaction_Version: {bs.Transactions.Version.ToString()}");
                Logger.LogDebug($"Block_Version: {bs.Blocks.Version.ToString()}");
                Logger.LogDebug($"Event_Version: {bs.Events.Version.ToString()}");
                Logger.LogDebug($"Validator_Version: {bs.Validators.Version.ToString()}");
                Logger.LogDebug($"LastApprovedSS: {_stateManager.LastApprovedSnapshot.StateHash.ToString()}");

                Logger.LogDebug($"Block Height: {_fastHeight}");

                _CallJsonRpcAPI("handShake", new JArray());

                Logger.LogDebug(
                    $"End: FastSync Start {fastSyncStart} End {DateTime.Now:HH:mm:ss.ffff}" +
                    $"With BlockHeight {_fastHeight} ");
            }
            catch (Exception e)
            {
                Logger.LogError($"Error in FastSync {e.Message}");
                throw;
            }
        }

        public (bool, ulong) GetFastSyncDetail()
        {
            return (_fastSyncFlag, _fastHeight);
        }

        public List<string> GetRpcPeers()
        {
            return _rpcPeers;
        }

        public void SetRpcPeers(List<string> peers)
        {
            _rpcPeers.AddRange(peers);
            _rpcPeers = _rpcPeers.ConvertAll(element =>
                element = (element.Contains("http://") ? element : "http://" + element));

            Logger.LogDebug($"List of peers {string.Join(",", _rpcPeers)}");
        }

        private void SetRpcUrl()
        {
            List<string> rpcList;
            if (GetRpcPeers().Count == 0)
            {
                var rpc = _configManager.GetConfig<RpcConfig>("rpc")!.Peers;
                rpcList = rpc!.ToList();
                SetRpcPeers(rpcList);
            }
            else
            {
                rpcList = GetRpcPeers();
            }

            var random = new Random();

            _rpcURL = rpcList[random.Next(rpcList.Count)];
            if (!_rpcURL.Contains("http"))
            {
                _rpcURL = string.Concat("http://", _rpcURL);
            }
        }

        private void _GetBlockVersions(ulong blockHeight)
        {
            var startTime = DateTime.Now.ToString("HH:mm:ss.ffff");
            Logger.LogDebug($"Start Receiving Versions For The Blocks - StartTime: {startTime}");

            foreach (var repo in _repoList)
            {
                ulong offset = 0;

                do
                {
                    Logger.LogDebug($"Start: Repo - {repo}");
                    JArray param = JArray.Parse(@$"[{blockHeight}, {repo}, {offset}]");
                    var result = _CallJsonRpcAPI("getBlockVersion", param);

                    if (offset == 0)
                    {
                        var valuesArr = JArray.Parse(JsonConvert.SerializeObject(result!["values"]!));
                        List<ulong>? values = valuesArr.ToObject<List<ulong>>();

                        _blockVersion.Add(repo, values!);
                    }
                    else
                    {
                        List<ulong> currentValues = _blockVersion[repo];

                        var valuesArr = JArray.Parse(JsonConvert.SerializeObject(result!["values"]!));
                        List<ulong>? values = valuesArr.ToObject<List<ulong>>();

                        currentValues.AddRange(values!);

                        _blockVersion[repo] = currentValues;
                    }

                    offset = ulong.Parse(result!["new_offset"]!.ToString());
                } while (blockHeight > offset);

                Logger.LogDebug($"End: Repo - {repo}");
            }

            Logger.LogDebug(
                $"End Receiving Versions For The Blocks - StartTime: {startTime} - EndTime: {DateTime.Now:HH:mm:ss.ffff}");
        }

        private void _SetBlockVersions(ulong blockHeight)
        {
            _GetBlockVersions(blockHeight);

            StorageManager storageManager = new StorageManager(_rocksDbContext);
            SnapshotIndexRepository snapshotIndexRepository =
                new SnapshotIndexRepository(_rocksDbContext, storageManager);

            var startTime = DateTime.Now.ToString("HH:mm:ss.ffff");
            Logger.LogDebug($"Start Setting Versions For The Blocks - StartTime: {startTime}");

            for (ulong i = 0; i <= blockHeight; i++)
            {
                foreach (var repo in _repoList)
                {
                    List<ulong> currentValues = _blockVersion[repo];
                    Logger.LogDebug($"Repo: {repo} Value: {currentValues[(int) i]}");
                    snapshotIndexRepository.SetVersion((uint) repo, i, currentValues[(int) i]);

                    IStorageState state = storageManager.GetLastState((uint) repo);
                    state.Commit();
                }
            }

            // foreach (var repo in _repoList)
            // {
            //     //if (_repoBlocks.ContainsKey(repo))
            //     //{
            //     //    List<ulong> blockIds = _repoBlocks[repo].Item1;
            //     //    snapshotIndexRepository.SetVersion((uint) repo, (ulong) blockHeight, blockIds[0]);
            //     //}
            //     //else
            //     //{
            //     //    snapshotIndexRepository.SetVersion((uint) repo, (ulong) blockHeight, 0);
            //     //}
            //     //
            //     //
            //     List<ulong> currentValues = _blockVersion[repo];
            //
            //     foreach (var (value, index) in currentValues.WithIndex())
            //     {
            //         Logger.LogDebug($"Repo: {repo} Value: {value}  Index: {index}");
            //         snapshotIndexRepository.SetVersion((uint) repo, (ulong) index, value);
            //     }
            // }

            Logger.LogDebug(
                $"End Setting Versions For The Blocks - StartTime: {startTime} - EndTime: {DateTime.Now:HH:mm:ss.ffff}");
        }

        private bool _HandShake()
        {
            var handShakeResult = _CallJsonRpcAPI("handShake", new JArray());
            var ready = bool.Parse(handShakeResult!["ready"]!.ToString());

            for (var i = 0; i < _totalRetryCount; i++)
            {
                if (!ready)
                {
                    Thread.Sleep(10000);
                    SetRpcUrl();
                    handShakeResult = _CallJsonRpcAPI("handShake", new JArray());
                    ready = bool.Parse(handShakeResult!["ready"]!.ToString());
                }
                else
                {
                    break;
                }
            }

            return ready;
        }

        private void _GetNodesForFastSync()
        {
            foreach (var repoType in _repoList)
            {
                ulong offset = 0;
                ulong totalNodes = 0;

                var startTime = DateTime.Now.ToString("HH:mm:ss.ffff");
                Logger.LogDebug($"Start Receiving Blocks for Repo {repoType} time {startTime}");
                do
                {
                    JArray param = JArray.Parse(@$"[{repoType}, {offset}]");
                    var result = _CallJsonRpcAPI("getBlocks", param);

                    if (ulong.Parse(result!["total_blocks"]!.ToString()) > 0)
                    {
                        if (offset == 0)
                        {
                            var ids = JArray.Parse(JsonConvert.SerializeObject(result!["ids"]!));
                            var values = JArray.Parse(JsonConvert.SerializeObject(result!["values"]!));

                            List<ulong>? lstIds = ids.ToObject<List<ulong>>();
                            List<byte[]>? lstValues = values.ToObject<List<byte[]>>();

                            var t = Tuple.Create(lstIds, lstValues);
                            _repoBlocks.Add(repoType, t!);
                        }
                        else
                        {
                            List<ulong> oldIds = _repoBlocks[repoType].Item1;
                            List<byte[]> oldValues = _repoBlocks[repoType].Item2;

                            var ids = JArray.Parse(JsonConvert.SerializeObject(result!["ids"]!));
                            var values = JArray.Parse(JsonConvert.SerializeObject(result!["values"]!));

                            List<ulong>? lstIds = ids.ToObject<List<ulong>>();
                            List<byte[]>? lstValues = values.ToObject<List<byte[]>>();

                            oldIds.AddRange(lstIds!);
                            oldValues.AddRange(lstValues!);

                            var t = Tuple.Create(oldIds, oldValues);
                            _repoBlocks[repoType] = t;
                        }
                    }

                    totalNodes = ulong.Parse(result!["total_blocks"]!.ToString());
                    offset = ulong.Parse(result!["new_offset"]!.ToString());
                } while (totalNodes > offset);
                
                Logger.LogDebug($"End Receiving Blocks for Repo {repoType} time {DateTime.Now.ToString("HH:mm:ss.ffff")}");
            }
        }

        public void PersistNodesForFastSync(NodeRepository nodeRepository, RocksDbAtomicWrite rocksDbAtomicWrite)
        {
            foreach (ulong repoType in _repoList)
            {
                Logger.LogDebug($"Start Persisting Blocks for Repo {repoType} time {DateTime.Now.ToString("HH:mm:ss.ffff")}");
                if (_repoBlocks.ContainsKey(repoType))
                {
                    List<ulong> blockIds = _repoBlocks[repoType].Item1;
                    List<byte[]> blockValues = _repoBlocks[repoType].Item2;

                    using (Stream stream = new MemoryStream())
                    {
                        IFormatter formatter = new BinaryFormatter();
                        formatter.Serialize(stream, blockValues);
                    }

                    for (var i = 0; i < blockIds.Count; i++)
                    {
                        nodeRepository.WriteNodeToBatch(blockIds[i], NodeSerializer.FromBytes(blockValues[i]),
                            rocksDbAtomicWrite);
                    }

                    var writeBatch2 = rocksDbAtomicWrite.GetWriteBatch();
                    nodeRepository.SaveBatch(writeBatch2);

                    switch (repoType)
                    {
                        case 1:
                            _stateManager.CurrentSnapshot.Balances.Version = blockIds[0];
                            _stateManager.CurrentSnapshot.Balances.Commit();
                            break;
                        case 4:
                            _stateManager.CurrentSnapshot.Contracts.Version = blockIds[0];
                            _stateManager.CurrentSnapshot.Contracts.Commit();
                            break;
                        case 5:
                            _stateManager.CurrentSnapshot.Storage.Version = blockIds[0];
                            _stateManager.CurrentSnapshot.Storage.Commit();
                            break;
                        case 6:
                            _stateManager.CurrentSnapshot.Transactions.Version = blockIds[0];
                            _stateManager.CurrentSnapshot.Transactions.Commit();
                            break;
                        case 7:
                            _stateManager.CurrentSnapshot.Blocks.Version = blockIds[0];
                            _stateManager.CurrentSnapshot.Blocks.Commit();
                            break;
                        case 9:
                            _stateManager.CurrentSnapshot.Events.Version = blockIds[0];
                            _stateManager.CurrentSnapshot.Events.Commit();
                            break;
                        case 10:
                            _stateManager.CurrentSnapshot.Validators.Version = blockIds[0];
                            _stateManager.CurrentSnapshot.Validators.Commit();
                            break;
                        default:
                            break;
                    }

                    _stateManager.Commit();
                }
                
                Logger.LogDebug($"End Persisting Blocks for Repo {repoType} time {DateTime.Now.ToString("HH:mm:ss.ffff")}");
            }
        }

        public void SetNodeForPersist(Dictionary<ulong, Tuple<List<ulong>, List<byte[]>>> repoBlocks)
        {
            _repoBlocks = repoBlocks;
        }
        
        public ulong GetMetaVersion()
        {
            var res = _CallJsonRpcAPI("getMetaVersion", new JArray());
            return (ulong) res!["Meta"]!;
        }

        public void SetMetaVersion(VersionRepository versionRepository, RocksDbAtomicWrite rocksDbAtomicWrite, ulong meta)
        {
            versionRepository.SetVersion((uint) RepositoryType.MetaRepository, meta, rocksDbAtomicWrite);
            rocksDbAtomicWrite.Commit();
        }

        private JToken? _CallJsonRpcAPI(string method, JArray param)
        {
            try
            {
                Logger.LogTrace(
                    $"Calling Method: {method} with Address: {_rpcURL} and Params: {string.Join(", ", param)}");
                JObject options;
                if (param.Count == 0)
                {
                    options = new JObject
                    {
                        ["method"] = method,
                        ["jsonrpc"] = "2.0",
                        ["id"] = "1"
                    };
                }
                else
                {
                    options = new JObject
                    {
                        ["method"] = method,
                        ["params"] = param,
                        ["jsonrpc"] = "2.0",
                        ["id"] = "1"
                    };
                }

                var webRequest = (HttpWebRequest) WebRequest.Create(_rpcURL);
                webRequest.ContentType = "application/json";
                webRequest.Method = "POST";
                using (Stream dataStream = webRequest.GetRequestStream())
                {
                    string payloadString = JsonConvert.SerializeObject(options);
                    byte[] byteArray = Encoding.UTF8.GetBytes(payloadString);
                    dataStream.Write(byteArray, 0, byteArray.Length);
                }

                WebResponse webResponse;
                JObject response;
                using (webResponse = webRequest.GetResponse())
                {
                    using (Stream str = webResponse.GetResponseStream()!)
                    {
                        using (StreamReader sr = new StreamReader(str))
                        {
                            response = JsonConvert.DeserializeObject<JObject>(sr.ReadToEnd());
                        }
                    }
                }

                var result = response["result"];
                var success = bool.Parse(result!["success"]!.ToString());

                if (!success)
                {
                    throw new Exception($"Error In JSON RPC API Call - Method: {method} Endpoint: {_rpcURL}");
                }
                else
                {
                    return result;
                }
            }
            catch (System.Exception exp)
            {
                Logger.LogTrace($"Error {exp.Message}");
                throw;
            }
        }

        public void Start()
        {
            _running = true;
            _blockSyncThread.Start();
            _pingThread.Start();
        }

        private List<UInt256> _GetMissingTransactions(IEnumerable<UInt256> txHashes)
        {
            return txHashes
                .Where(hash => (_transactionManager.GetByHash(hash) ?? _transactionPool.GetByHash(hash)) is null)
                .ToList();
        }

        public void Dispose()
        {
            _running = false;
            if (_blockSyncThread.ThreadState == ThreadState.Running)
                _blockSyncThread.Join();
            if (_pingThread.ThreadState == ThreadState.Running)
                _pingThread.Join();
        }
    }
}