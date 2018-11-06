﻿using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NeoSharp.Application.Attributes;
using NeoSharp.Application.Client;
using NeoSharp.BinarySerialization;
using NeoSharp.Core.Blockchain;
using NeoSharp.Core.Blockchain.Processing;
using NeoSharp.Core.Blockchain.Repositories;
using NeoSharp.Core.Extensions;
using NeoSharp.Core.Models;
using NeoSharp.Core.Models.OperationManger;
using NeoSharp.Core.Network;
using NeoSharp.Types;

namespace NeoSharp.Application.Controllers
{
    public class PromptBlockchainController : IPromptController
    {
        #region Private fields

        private readonly IServerContext _serverContext;
        private readonly IBlockPool _blockPool;
        private readonly ITransactionPool _transactionPool;
        private readonly IBlockchain _blockchain;
        private readonly IBlockRepository _blockRepository;
        private readonly IBlockPersister _blockPersister;
        private readonly ITransactionRepository _transactionModel;
        private readonly IAssetRepository _assetModel;
        private readonly IBlockchainContext _blockchainContext;
        private readonly IConsoleHandler _consoleHandler;
        private readonly ISigner<Block> _blockSigner;

        #endregion

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="serverContext">Server context</param>
        /// <param name="blockchain">Blockchain</param>
        /// <param name="blockRepository">The Block Model.</param>
        /// <param name="transactionModel"></param>
        /// <param name="assetModel"></param>
        /// <param name="blockchainContext">The block chain context class.</param>
        /// <param name="blockPool">Block pool</param>
        /// <param name="blockSigner">Block signer</param>
        /// <param name="blockPersister">Block persister</param>
        /// <param name="transactionPool">Transaction Pool</param>
        /// <param name="consoleHandler">Console handler</param>
        public PromptBlockchainController(
            IServerContext serverContext,
            IBlockchain blockchain,
            IBlockRepository blockRepository,
            ITransactionRepository transactionModel,
            IAssetRepository assetModel,
            IBlockchainContext blockchainContext,
            IBlockPool blockPool,
            IBlockPersister blockPersister,
            ISigner<Block> blockSigner,
            ITransactionPool transactionPool,
            IConsoleHandler consoleHandler)
        {
            _serverContext = serverContext;
            _blockchain = blockchain;
            _blockRepository = blockRepository;
            _blockSigner = blockSigner;
            _blockPersister = blockPersister;
            _transactionModel = transactionModel;
            _assetModel = assetModel;
            _blockchainContext = blockchainContext;
            _blockPool = blockPool;
            _transactionPool = transactionPool;
            _consoleHandler = consoleHandler;
        }

        void WriteStatePercent(string title, string msg, long? value, long? max)
        {
            if (!value.HasValue || !max.HasValue)
            {
                _consoleHandler.WriteLine(title + ": " + msg + " ");
                return;
            }

            _consoleHandler.Write(title + ": " + msg + " ");

            using (var pg = _consoleHandler.CreatePercent(max.Value))
            {
                pg.Value = value.Value;
            }
        }

        private string FormatState(long? value)
        {
            return value.HasValue ? value.Value.ToString("###,###,###,###,##0") : "?";
        }

        /// <summary>
        /// Show state
        /// </summary>
        [PromptCommand("state", Category = "Blockchain", Help = "Show current state")]
        public void StateCommand()
        {
            var memStr = FormatState(_transactionPool.Size);
            var blockStr = FormatState(_blockPool.Size);
            var headStr = FormatState(_blockchainContext.LastBlockHeader?.Index);
            var blStr = FormatState(_blockchainContext.CurrentBlock?.Index);
            var blNodes = FormatState(_serverContext.ConnectedPeers.Count);
            var blIndex = FormatState(0); // TODO #398: Change me

            var numSpaces = new int[] { memStr.Length, blockStr.Length, blIndex.Length, headStr.Length, blStr.Length, blNodes.Length }.Max() + 1;

            _consoleHandler.WriteLine("Pools", ConsoleOutputStyle.Information);
            _consoleHandler.WriteLine("");

            WriteStatePercent(" Memory", memStr.PadLeft(numSpaces, ' '), _transactionPool.Size, _transactionPool.Capacity);
            WriteStatePercent(" Blocks", blockStr.PadLeft(numSpaces, ' '), _blockPool.Size, _blockPool.Capacity);

            _consoleHandler.WriteLine("");
            _consoleHandler.WriteLine("Heights", ConsoleOutputStyle.Information);
            _consoleHandler.WriteLine("");

            _consoleHandler.WriteLine("Headers: " + headStr.PadLeft(numSpaces, ' ') + " ");

            WriteStatePercent(" Blocks", blStr.PadLeft(numSpaces, ' '), _blockchainContext.CurrentBlock?.Index, _blockchainContext.LastBlockHeader?.Index);
            WriteStatePercent("  Index", blIndex.PadLeft(numSpaces, ' '), 0, _blockchainContext.CurrentBlock?.Index);

            _consoleHandler.WriteLine("");
            _consoleHandler.WriteLine("Nodes", ConsoleOutputStyle.Information);
            _consoleHandler.WriteLine("");

            WriteStatePercent("  Count", blNodes.PadLeft(numSpaces, ' '), _serverContext.ConnectedPeers.Count, _serverContext.MaxConnectedPeers);
        }

        /// <summary>
        /// Get blocks from stream
        /// </summary>
        /// <param name="stream">Stream</param>
        /// <param name="read_start">Read start index</param>
        /// <returns>Get block</returns>
        private IEnumerable<Block> GetBlocks(Stream stream, bool read_start = false)
        {
            using (var reader = new BinaryReader(stream))
            {
                var start = read_start ? reader.ReadUInt32() : 0;
                var count = reader.ReadUInt32();
                var end = start + count - 1;

                if (end <= _blockchainContext.CurrentBlock.Index) yield break;

                using (var progress = _consoleHandler.CreatePercent(count))
                {
                    for (var height = start; height <= end; height++)
                    {
                        var array = reader.ReadBytes(reader.ReadInt32());

                        progress.Value++;

                        if (height > _blockchainContext.CurrentBlock.Index)
                        {
                            var block = BinarySerializer.Default.Deserialize<Block>(array);
                            _blockSigner.Sign(block);
                            yield return block;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Import blocks
        /// </summary>
        /// <param name="file">File</param>
        [PromptCommand("import blocks", Category = "Blockchain", Help = "Import blocks from zip file")]
        public async Task ImportBlocks(FileInfo file)
        {
            if (!file.Exists)
            {
                throw new ArgumentException($"file '{file.FullName}' must exist");
            }

            using (var fs = file.OpenRead())
            {
                if (file.FullName.EndsWith(".zip", StringComparison.InvariantCultureIgnoreCase))
                {
                    using (var zip = new ZipArchive(fs, ZipArchiveMode.Read))
                    {
                        foreach (var entry in zip.Entries.Where(u => u.Length > 0).OrderBy(u => u.Name))
                        {
                            using (var zs = zip.GetEntry(entry.Name).Open())
                            {
                                foreach (var block in GetBlocks(zs, true))
                                {
                                    await _blockPersister.Persist(block);
                                }
                            }
                        }
                    }
                }
                else
                {
                    foreach (var block in GetBlocks(fs, true))
                    {
                        await _blockPersister.Persist(block);
                    }
                }
            }
        }

        /// <summary>
        /// Get block by index
        /// </summary>
        /// <param name="blockIndex">Index</param>
        /// <param name="output">Output</param>
        [PromptCommand("header", Category = "Blockchain", Help = "Get header by index or by hash")]
        public async Task HeaderCommand(uint blockIndex, PromptOutputStyle output = PromptOutputStyle.json)
        {
            _consoleHandler.WriteObject(await _blockRepository.GetBlockHeader(blockIndex), output);
        }

        /// <summary>
        /// Get block by hash
        /// </summary>
        /// <param name="blockHash">Hash</param>
        /// <param name="output">Output</param>
        [PromptCommand("header", Category = "Blockchain", Help = "Get header by index or by hash")]
        public async Task HeaderCommand(UInt256 blockHash, PromptOutputStyle output = PromptOutputStyle.json)
        {
            _consoleHandler.WriteObject(await _blockRepository.GetBlockHeader(blockHash), output);
        }

        /// <summary>
        /// Get block by index
        /// </summary>
        /// <param name="blockIndex">Index</param>
        /// <param name="output">Output</param>
        [PromptCommand("block", Category = "Blockchain", Help = "Get block by index or by hash")]
        public async Task BlockCommand(uint blockIndex, PromptOutputStyle output = PromptOutputStyle.json)
        {
            _consoleHandler.WriteObject(await _blockRepository.GetBlock(blockIndex), output);
        }

        /// <summary>
        /// Get block by hash
        /// </summary>
        /// <param name="blockHash">Hash</param>
        /// <param name="output">Output</param>
        [PromptCommand("block", Category = "Blockchain", Help = "Get block by index or by hash")]
        public async Task BlockCommand(UInt256 blockHash, PromptOutputStyle output = PromptOutputStyle.json)
        {
            _consoleHandler.WriteObject(await _blockRepository.GetBlock(blockHash), output);
        }

        /// <summary>
        /// Get tx by hash
        /// </summary>
        /// <param name="hash">Hash</param>
        /// <param name="output">Output</param>
        [PromptCommand("tx", Category = "Blockchain", Help = "Get tx")]
        public async Task TxCommand(UInt256 hash, PromptOutputStyle output = PromptOutputStyle.json)
        {
            _consoleHandler.WriteObject(await _transactionModel.GetTransaction(hash), output);
        }

        /// <summary>
        /// Get tx by block hash/ TxId
        /// </summary>
        /// <param name="blockIndex">Block Index</param>
        /// <param name="txNumber">TxNumber</param>
        /// <param name="output">Output</param>
        [PromptCommand("tx", Category = "Blockchain", Help = "Get tx by block num/tx number")]
        public async Task TxCommand(uint blockIndex, ushort txNumber, PromptOutputStyle output = PromptOutputStyle.json)
        {
            var block = await _blockRepository.GetBlock(blockIndex);
            _consoleHandler.WriteObject(block.Transactions?[txNumber], output);
        }

        /// <summary>
        /// Get asset by hash
        /// </summary>
        /// <param name="hash">Hash</param>
        /// <param name="output">Output</param>
        [PromptCommand("asset", Category = "Blockchain", Help = "Get asset", Order = 0)]
        public async Task AssetCommand(UInt256 hash, PromptOutputStyle output = PromptOutputStyle.json)
        {
            _consoleHandler.WriteObject(await _assetModel.GetAsset(hash), output);
        }

        /// <summary>
        /// Get asset by query
        /// </summary>
        /// <param name="query">Query</param>
        /// <param name="mode">Regex/Contains</param>
        /// <param name="output">Output</param>
        [PromptCommand("asset", Category = "Blockchain", Help = "Get asset", Order = 1)]
        public async Task AssetCommand(string query, EnumerableExtensions.QueryMode mode = EnumerableExtensions.QueryMode.Contains, PromptOutputStyle output = PromptOutputStyle.json)
        {
            var assets = await _assetModel.GetAssets();
            var result = assets.QueryResult(query, mode).ToArray();

            _consoleHandler.WriteObject(result, output);
        }

        /// <summary>
        /// Get contract by hash
        /// </summary>
        /// <param name="hash">Hash</param>
        /// <param name="output">Output</param>
        [PromptCommand("contract", Category = "Blockchain", Help = "Get asset", Order = 0)]
        public Task ContractCommand(UInt160 hash, PromptOutputStyle output = PromptOutputStyle.json)
        {
            throw new NotImplementedException();
            //_consoleHandler.WriteObject(await _blockchain?.GetContract(hash), output);
        }

        /// <summary>
        /// Get contract by query
        /// </summary>
        /// <param name="query">Query</param>
        /// <param name="mode">Regex/Contains</param>
        /// <param name="output">Output</param>
        [PromptCommand("contract", Category = "Blockchain", Help = "Get asset", Order = 1)]
        public Task ContractCommand(string query, EnumerableExtensions.QueryMode mode = EnumerableExtensions.QueryMode.Contains, PromptOutputStyle output = PromptOutputStyle.json)
        {
            throw new NotImplementedException();
            //var contracts = await _blockchain?.GetContracts();
            //var result = contracts.QueryResult(query, mode).ToArray();

            //_consoleHandler.WriteObject(result, output);
        }
    }
}