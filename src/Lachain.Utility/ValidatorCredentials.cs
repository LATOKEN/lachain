using System;
using System.Linq;
using System.Text;
using Google.Protobuf;
using Lachain.Proto;
using Nethereum.RLP;

namespace Lachain.Utility
{
    public class ValidatorCredentials
    {
        public ValidatorCredentials(ECDSAPublicKey publicKey, string resolvableAddress,
            byte[] thresholdSignaturePublicKey)
        {
            PublicKey = publicKey;
            ResolvableAddress = resolvableAddress;
            ThresholdSignaturePublicKey = thresholdSignaturePublicKey;
        }

        public ECDSAPublicKey PublicKey { get; }
        public string ResolvableAddress { get; }
        public byte[] ThresholdSignaturePublicKey { get; }
        
        public static ValidatorCredentials FromBytes(ReadOnlySpan<byte> bytes)
        {
            var decoded = (RLPCollection) RLP.Decode(bytes.ToArray());
            var publicKey = new ECDSAPublicKey {Buffer = ByteString.CopyFrom(decoded[0].RLPData)};
            var tsKey = decoded[1].RLPData;
            var address = Encoding.UTF8.GetString(decoded[2].RLPData ?? Array.Empty<byte>());
            return new ValidatorCredentials(publicKey, address, tsKey);
        }

        public byte[] ToBytes()
        {
            return RLP.EncodeList(
                RLP.EncodeElement(PublicKey.Buffer.ToArray()),
                RLP.EncodeElement(ThresholdSignaturePublicKey),
                RLP.EncodeElement(Encoding.UTF8.GetBytes(ResolvableAddress))
            );
        }
    }
}