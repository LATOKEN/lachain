using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NeoSharp.Core.Messaging.Messages;
using NeoSharp.Core.Models;
using NeoSharp.Core.Network;
using NeoSharp.Core.Storage.Blockchain;

namespace NeoSharp.Core.Messaging.Handlers
{
    public class GetBlockHeadersMessageHandler : MessageHandler<GetBlockHeadersMessage>
    {
        private const int MaxBlockHeadersCountToReturn = 2000;
        
        private readonly IBlockRepository _blockRepository;

        private Task<BlockHeader> GetBlockHeader(UInt256 hash) => Task.FromResult(_blockRepository.GetBlockHeaderByHash(hash));

        public GetBlockHeadersMessageHandler(IBlockRepository blockModel)
        {
            _blockRepository = blockModel ?? throw new ArgumentNullException(nameof(blockModel));
        }

        #region MessageHandler override methods
        /// <inheritdoc />
        public override async Task Handle(GetBlockHeadersMessage message, IPeer sender)
        {
            var hashStart = (message.Payload.HashStart ?? new UInt256[0])
                .Where(h => h != null)
                .Distinct()
                .ToArray();

            if (hashStart.Length == 0) return;

            var hashStop = message.Payload.HashStop;

            var blockHash = (await Task.WhenAll(hashStart.Select(GetBlockHeader)))
                .Where(bh => bh != null)
                .OrderBy(bh => bh.Index)
                .Select(bh => bh.Hash)
                .FirstOrDefault();

            if (blockHash == null || blockHash == hashStop)
                return;
            var blockHeaders = new List<BlockHeader>();

            do
            {
                var nextBlock = _blockRepository.GetNextBlockHeaderByHash(blockHash);
                if (nextBlock == null || nextBlock.Hash == hashStop)
                    break;
                blockHeaders.Add(_blockRepository.GetBlockHeaderByHash(blockHash));
            } while (blockHeaders.Count < MaxBlockHeadersCountToReturn);

            if (blockHeaders.Count == 0) return;

            await sender.Send(new BlockHeadersMessage(blockHeaders));
        }

        /// <inheritdoc />
        public override bool CanHandle(Message message)
        {
            return message is GetBlockHeadersMessage;
        }
        #endregion
    }
}