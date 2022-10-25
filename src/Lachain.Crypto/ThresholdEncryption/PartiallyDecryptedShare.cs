using Lachain.Proto;
using Google.Protobuf;
using MCL.BLS12_381.Net;

namespace Lachain.Crypto.ThresholdEncryption
{
    public class PartiallyDecryptedShare
    {
        public G1 Ui { get; }
        public int DecryptorId { get; }

        public int ShareId { get; }


        public PartiallyDecryptedShare(G1 _ui, int decryptorId, int shareId)
        {
            Ui = _ui;
            DecryptorId = decryptorId;
            ShareId = shareId;
        }

        public static PartiallyDecryptedShare Decode(TPKEPartiallyDecryptedShareMessage message)
        {
            var u = G1.FromBytes(message.Share.ToByteArray());
            return new PartiallyDecryptedShare(u, message.DecryptorId, message.ShareId);
        }

        public TPKEPartiallyDecryptedShareMessage Encode()
        {
            return new TPKEPartiallyDecryptedShareMessage
            {
                Share = ByteString.CopyFrom(Ui.ToBytes()),
                DecryptorId = DecryptorId,
                ShareId = ShareId
            };
        }
    }
}