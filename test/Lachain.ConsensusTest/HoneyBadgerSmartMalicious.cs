using System;
using System.Linq;
using Google.Protobuf;
using Lachain.Consensus;
using Lachain.Consensus.HoneyBadger;
using Lachain.Crypto.ThresholdSignature;
using Lachain.Crypto.TPKE;
using Lachain.Proto;
using Lachain.Utility.Utils;
using TestUtility = Lachain.UtilityTest.TestUtils;
using MCL.BLS12_381.Net;

namespace Lachain.ConsensusTest
{
    public class HoneyBadgerSmartMalicious : HoneyBadger
    {
        public HoneyBadgerSmartMalicious(HoneyBadgerId honeyBadgerId, IPublicConsensusKeySet wallet, PrivateKey privateKey, IConsensusBroadcaster broadcaster) : 
            base(honeyBadgerId, wallet, privateKey, broadcaster)
        {
        }

        protected override ConsensusMessage CreateDecryptedMessage(PartiallyDecryptedShare share)
        {
            var message = new ConsensusMessage
            {
                Decrypted = Wallet.TpkePublicKey.Encode(share)
            };
            byte[] randomValue;
            ulong waitTime = 5000;
            var startTime = TimeUtils.CurrentTimeMillis();
            while (true)
            {
                if (TimeUtils.CurrentTimeMillis() - startTime >= waitTime)
                {
                    Console.WriteLine($"waiting too long for valid G1: {TimeUtils.CurrentTimeMillis() - startTime}");
                }
                randomValue = TestUtility.GetRandomBytes();
                try
                {
                    var g1 = G1.FromBytes(randomValue);
                    break;
                }
                catch (Exception)
                {

                }
            }
            message.Decrypted.Share = ByteString.CopyFrom(randomValue);
            return message;
        }
    }
}