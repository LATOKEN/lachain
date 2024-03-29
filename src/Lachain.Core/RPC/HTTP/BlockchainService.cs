﻿using System;
using System.Linq;
using AustinHarris.JsonRpc;
using Newtonsoft.Json.Linq;
using Lachain.Core.Blockchain.Interface;
using Lachain.Core.Blockchain.Pool;
using Lachain.Core.Blockchain.SystemContracts;
using Lachain.Core.Network;
using Lachain.Proto;
using Lachain.Storage.State;
using Lachain.Utility.JSON;
using Lachain.Utility.Utils;

namespace Lachain.Core.RPC.HTTP
{
    public class BlockchainService : JsonRpcService
    {
        private readonly ITransactionManager _transactionManager;
        private readonly IBlockManager _blockManager;
        private readonly IStateManager _stateManager;
        private readonly ITransactionPool _transactionPool;
        private readonly IBlockSynchronizer _blockSynchronizer;
        private readonly ISystemContractReader _systemContractReader;

        public BlockchainService(
            ITransactionManager transactionManager,
            IBlockManager blockManager,
            ITransactionPool transactionPool,
            IStateManager stateManager,
            IBlockSynchronizer blockSynchronizer,
            ISystemContractReader systemContractReader
        )
        {
            _transactionPool = transactionPool;
            _transactionManager = transactionManager;
            _blockManager = blockManager;
            _stateManager = stateManager;
            _blockSynchronizer = blockSynchronizer;
            _systemContractReader = systemContractReader;
        }

        [JsonRpcMethod("getBlockByHeight")]
        private JObject? GetBlockByHeight(uint blockHeight)
        {
            var block = _blockManager.GetByHeight(blockHeight);
            return block?.ToJson();
        }

        [JsonRpcMethod("getBlockByHash")]
        private JObject? GetBlockByHash(string blockHash)
        {
            var block = _blockManager.GetByHash(blockHash.HexToBytes().ToUInt256());
            return block?.ToJson();
        }

        [JsonRpcMethod("getTransactionsByBlockHash")]
        private JObject? GetTransactionsByBlockHash(string blockHash)
        {
            var block = _blockManager.GetByHash(blockHash.HexToBytes().ToUInt256()) ??
                        throw new Exception($"No block with hash {blockHash}");
            var txs = block.TransactionHashes
                .Select(hash => _transactionManager.GetByHash(hash)?.ToJson())
                .ToList();
            return new JObject
            {
                ["transactions"] = new JArray(txs),
            };
        }

        [JsonRpcMethod("getTransactionByHash")]
        private JObject? GetTransactionByHash(string txHash)
        {
            var tx = _transactionManager.GetByHash(txHash.HexToBytes().ToUInt256());
            return tx?.ToJson();
        }

        [JsonRpcMethod("getBlockStat")]
        private JObject GetBlockStat()
        {
            var json = new JObject
            {
                ["currentHeight"] = _blockManager.GetHeight()
            };
            return json;
        }

        [JsonRpcMethod("getEventsByTransactionHash")]
        private JArray GetEventsByTransactionHash(string txHash)
        {
            var transactionHash = txHash.HexToUInt256();
            var txEvents = _stateManager.LastApprovedSnapshot.Events.GetTotalTransactionEvents(transactionHash);
            var jArray = new JArray();
            for (var i = 0; i < txEvents; i++)
            {
                var evObj = _stateManager.LastApprovedSnapshot.Events.GetEventByTransactionHashAndIndex(transactionHash,
                    (uint) i);
                var ev = evObj.Event;
                if (ev is null)
                    continue;
                jArray.Add(evObj.ToJson());
            }

            return jArray;
        }

        [JsonRpcMethod("bcn_validators")]
        private JArray GetValidatorPublicKeys()
        {
            var publicKeys = _stateManager.LastApprovedSnapshot.Validators.GetValidatorsPublicKeys();
            var jArray = new JArray();
            var ecdsaPublicKeys = publicKeys as ECDSAPublicKey[] ?? publicKeys.ToArray();
            for (var i = 0; i < ecdsaPublicKeys.Count(); i++)
            {
                jArray.Add(ecdsaPublicKeys[i].ToHex());
            }

            return jArray;
        }

        [JsonRpcMethod("getTransactionPool")]
        private JArray GetTransactionPool()
        {
            var txHashes = _transactionPool.Transactions.Keys;
            var jArray = new JArray();
            foreach (var txHash in txHashes)
            {
                jArray.Add(txHash.ToHex());
            }

            return jArray;
        }

        [JsonRpcMethod("getTransactionPoolRepository")]
        private JArray GetTransactionPoolRepository()
        {
            var txHashes = _transactionPool.GetTransactionPoolRepository();
            var jArray = new JArray();
            foreach (var txHash in txHashes)
            {
                jArray.Add(txHash.ToHex());
            }
            return jArray;
        }

        [JsonRpcMethod("deleteTransactionPoolRepository")]
        private void DeleteTransactionPool()
        {
            _transactionPool.ClearRepository();
        }

        [JsonRpcMethod("clearInMemoryPool")]
        private void ClearInMemoryPool()
        {
            _transactionPool.Clear();
        }



        [JsonRpcMethod("getTransactionPoolByHash")]
        private JObject? GetTransactionPoolByHash(string txHash)
        {
            var transaction = _transactionPool.GetByHash(HexUtils.HexToUInt256(txHash));
            return transaction?.ToJson();
        }

        [JsonRpcMethod("bcn_syncing")]
        private JObject? GetSyncStatus()
        {
            var current = _blockManager.GetHeight();
            var max = _blockSynchronizer.GetHighestBlock();
            
            // add 1 to prevent non full sync status appearance on each new block
            var isSyncing = !max.HasValue || max > current + 1;
            var maxBlock = max.HasValue ? max.Value : 0;
            return new JObject
            {
                ["syncing"] = isSyncing,
                ["currentBlock"] = _blockManager.GetHeight().ToHex(),
                ["highestBlock"] = maxBlock.ToHex(),
                // TODO: check time correctness
                ["wrongTime"] = false,
            };
        }

        [JsonRpcMethod("eth_syncing")]
        private bool GetEthSyncStatus()
        {
            return false;
        }

        [JsonRpcMethod("bcn_cycle")]
        private JObject GetCurrentCycle()
        {
            var attendanceDetectionPhase = _systemContractReader.IsAttendanceDetectionPhase();
            var vrfSubmissionPhase = _systemContractReader.IsVrfSubmissionPhase();
            var keyGenPhase = _systemContractReader.IsKeyGenPhase();
            var phase = attendanceDetectionPhase ? "AttendanceSubmissionPhase" :
                vrfSubmissionPhase ? "VrfSubmissionPhase" :
                keyGenPhase ? "KeyGenPhase" : "None";
            return new JObject
            {
                ["currentPeriod"] = phase,
                ["cycle"] = _blockManager.GetHeight() / StakingContract.CycleDuration,
            };
        }
    }
}