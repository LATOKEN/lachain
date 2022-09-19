using System;
using System.Collections.Generic;
using System.Linq;
using Lachain.Consensus;
using Lachain.Core.Blockchain.Interface;
using Lachain.Crypto.ThresholdSignature;
using Lachain.Logger;
using Lachain.Networking;
using Lachain.Proto;
using Lachain.Storage.Repositories;
using NLog.Fluent;
using PublicKey = Lachain.Crypto.TPKE.PublicKey;

namespace Lachain.Core.Blockchain.Validators
{
    public class ValidatorManager : IValidatorManager
    {
        private readonly ISnapshotIndexRepository _snapshotIndexRepository;
        private readonly INetworkManager _networkManager;
        private static readonly ILogger<ValidatorManager> Logger = LoggerFactory.GetLoggerForClass<ValidatorManager>();
        private readonly Dictionary<long, IReadOnlyCollection<ECDSAPublicKey>> _pubkeyCache = new Dictionary<long, IReadOnlyCollection<ECDSAPublicKey>>();

        public ValidatorManager(
            ISnapshotIndexRepository snapshotIndexRepository,
            IBlockManager blockManager,
            INetworkManager networkManager
        )
        {
            _snapshotIndexRepository = snapshotIndexRepository;
            _networkManager = networkManager;

            blockManager.OnBlockPersisted += OnBlockPersisted;
        }

        private void OnBlockPersisted(object? sender, Block block)
        {
            var validators = _snapshotIndexRepository.GetSnapshotForBlock(block.Header.Index).Validators
                .GetValidatorsPublicKeys().ToList();

            var myPublicKey = _networkManager.MessageFactory.GetPublicKey();
            if (validators.Contains(myPublicKey))
                _networkManager.ConnectValidatorChannel(validators);
            else _networkManager.DisconnectValidatorChannel();
        }

        public IPublicConsensusKeySet? GetValidators(long afterBlock)
        {
            Logger.LogTrace($"Getting validators after block {afterBlock}");
            try
            {
                var state = _snapshotIndexRepository.GetSnapshotForBlock((ulong) afterBlock).Validators
                    .GetConsensusState();
                var n = state.Validators.Length;
                var f = (n - 1) / 3;
                Logger.LogTrace($"Fetched {n} validators f={f}");
                return new PublicConsensusKeySet(
                    n, f,
                    PublicKey.FromBytes(state.TpkePublicKey),
                    state.TpkeVerificationKeys.Select(x => PublicKey.FromBytes(x)),
                    new PublicKeySet(
                        state.Validators.Select(v =>
                            Crypto.ThresholdSignature.PublicKey.FromBytes(v.ThresholdSignaturePublicKey)),
                        f
                    ),
                    state.Validators.Select(v => v.PublicKey)
                );
            }
            catch (Exception)
            {
                // ignored
            }

            return null;
        }

        public IReadOnlyCollection<ECDSAPublicKey> GetValidatorsPublicKeys(long afterBlock)
        {
            lock (_pubkeyCache)
            {
                if (_pubkeyCache.ContainsKey(afterBlock)) return _pubkeyCache[afterBlock];
                try
                {
                    IReadOnlyCollection<ECDSAPublicKey> res = _snapshotIndexRepository.GetSnapshotForBlock((ulong)afterBlock).Validators
                    .GetValidatorsPublicKeys()
                    .ToArray();
                    if (!res.Any())
                        return res; // do not cache empty value,  it can change in future
                    _pubkeyCache.Add(afterBlock, res);
                    return _pubkeyCache[afterBlock];
                }
                catch (Exception e)
                {
                    Logger.LogWarning($"Failed to get validators for block {afterBlock},  error {e}");
                    return new List<ECDSAPublicKey>();
                }
            }
        }

        public ECDSAPublicKey GetPublicKey(uint validatorIndex, long afterBlock)
        {
            return GetValidatorsPublicKeys(afterBlock).ElementAt((int) validatorIndex);
        }

        public int GetValidatorIndex(ECDSAPublicKey publicKey, long afterBlock)
        {
            return GetValidatorsPublicKeys(afterBlock)
                .Select((key, index) => new {key, index})
                .Where(arg => publicKey.Equals(arg.key))
                .Select(arg => arg.index)
                .First();
        }

        public bool IsValidatorForBlock(ECDSAPublicKey key, long block)
        {
            return GetValidatorsPublicKeys(block - 1).Contains(key);
        }
    }
}