using System;
using Lachain.Logger;
using Lachain.Utility.Utils;

namespace Lachain.Networking.PeerFault
{
    public class PeerPenalty
    {
        private static readonly ILogger<PeerPenalty> Logger = LoggerFactory.GetLoggerForClass<PeerPenalty>();
        public ulong PenaltyCount { get; private set; }
        public byte[] PeerPublicKey { get; private set; }
        public event EventHandler<(byte[] peerPublicKey, ulong penaltyCount)>? OnTooManyPenalty;
        public PeerPenalty(byte[] publicKey)
        {
            PeerPublicKey = publicKey;
            PenaltyCount = 0;
        }

        public void IncPenalty()
        {
            PenaltyCount++;
            // TODO: determine penalty tolerance threshold for an era
            // can be cycle duration dependent
            if (PenaltyCount >= 10000)
            {
                OnTooManyPenalty?.Invoke(this, (PeerPublicKey, PenaltyCount));
                Logger.LogWarning($"Peer {PeerPublicKey.ToHex()} did {PenaltyCount} penalties");
            } 
        }

        public ulong AdvanceEra(ulong era)
        {
            var donePenalty = PenaltyCount;
            PenaltyCount = 0;
            Logger.LogTrace($"Done {donePenalty} penalties during era {era - 1} by peer {PeerPublicKey.ToHex()}");
            return donePenalty;
        }
    }
}