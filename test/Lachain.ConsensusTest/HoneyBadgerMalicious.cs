using System.Linq;
using Google.Protobuf;
using Lachain.Consensus;
using Lachain.Consensus.HoneyBadger;
using Lachain.Crypto.TPKE;
using Lachain.Proto;

namespace Lachain.ConsensusTest
{
    public class HoneyBadgerMalicious : HoneyBadger
    {
        public HoneyBadgerMalicious(HoneyBadgerId honeyBadgerId, IPublicConsensusKeySet wallet, PrivateKey privateKey, IConsensusBroadcaster broadcaster) : 
            base(honeyBadgerId, wallet, privateKey, broadcaster)
        {
        }
        
        protected override ConsensusMessage CreateDecryptedMessage(PartiallyDecryptedShare share)
        {
            var message = new ConsensusMessage
            {
                Decrypted = Wallet.TpkePublicKey.Encode(share)
            };
            message.Decrypted.Share = ByteString.CopyFrom((message.Decrypted.Share.ToByteArray().Reverse().ToArray()));
            return message;
        }
    }
}