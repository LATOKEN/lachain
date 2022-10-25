using System.Linq;
using Google.Protobuf;
using Lachain.Consensus;
using Lachain.Consensus.HoneyBadger;
using Lachain.Crypto.ThresholdEncryption;
using Lachain.Crypto.ThresholdSignature;
using Lachain.Proto;

namespace Lachain.ConsensusTest
{
    public class HoneyBadgerMalicious : HoneyBadger
    {
        public HoneyBadgerMalicious(HoneyBadgerId honeyBadgerId, IPublicConsensusKeySet wallet, PrivateKeyShare privateKey, bool skipShareVerification, IConsensusBroadcaster broadcaster) : 
            base(honeyBadgerId, wallet, privateKey, skipShareVerification, broadcaster)
        {
        }
        
        protected override ConsensusMessage CreateDecryptedMessage(PartiallyDecryptedShare share)
        {
            var message = new ConsensusMessage
            {
                Decrypted = share.Encode()
            };
            message.Decrypted.Share = ByteString.CopyFrom((message.Decrypted.Share.ToByteArray().Reverse().ToArray()));
            return message;
        }
    }
}