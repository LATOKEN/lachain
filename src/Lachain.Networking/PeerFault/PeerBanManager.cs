using System;
using System.Runtime.CompilerServices;
using Lachain.Logger;
using Lachain.Networking.Hub;
using Lachain.Storage.Repositories;
using Lachain.Utility.Utils;

namespace Lachain.Networking.PeerFault
{
    public class PeerBanManager : IPeerBanManager
    {
        private static readonly ILogger<PeerBanManager> Logger = LoggerFactory.GetLoggerForClass<PeerBanManager>();
        private ulong _era = 0;
        private readonly IPeerBanRepository _repository;
        public PeerBanManager(IPeerBanRepository repository)
        {
            _repository = repository;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void AdvanceEra(ulong era)
        {
            if (era < _era)
            {
                throw new Exception($"We are on era {_era} but advancing to era {era}");
            }
            if (era > _era)
            {
                Logger.LogTrace($"Removing banned peer list of era {_era}");
                _repository.RemoveAllBannedPeer(_era);
            }
            _era = era;
        }

        public void RegisterPeer(ClientWorker peer)
        {
            peer._penaltyHandler.OnTooManyPenalty += BanPeerForPenalty;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void BanPeer(byte[] publicKey)
        {
            // ban this peer for this _era, _era + 1 and _era + 2
            // this way even if he can become validator, will not get any attendance
            // from me for 2 cycles, thus increasing chances of getting penalty
            _repository.AddBannedPeer(_era, publicKey);
            _repository.AddBannedPeer(_era + 1, publicKey);
            _repository.AddBannedPeer(_era + 2, publicKey);
        }

        private void BanPeerForPenalty(object? sender, (byte[] publicKey, ulong penalties) @event)
        {
            var (publicKey, penalties) = @event;
            BanPeer(publicKey);
            Logger.LogTrace($"Banned peer {publicKey.ToHex()} for {penalties} penalties during era {_era}");
        }
    }
}