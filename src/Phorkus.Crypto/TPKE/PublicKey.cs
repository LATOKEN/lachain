using System;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;
using Phorkus.Consensus;
using Phorkus.Consensus.TPKE;
using Phorkus.Crypto.MCL.BLS12_381;
using Phorkus.Proto;

namespace Phorkus.Crypto.TPKE
{
    public class PublicKey
    {
        public G1 Y;
        private readonly int _t;

        public PublicKey(G1 y, int t)
        {
            Y = y;
            _t = t;
        }

        public EncryptedShare Encrypt(IRawShare rawShare)
        {
            var r = Fr.GetRandom();
            var u = G1.Generator * r;
            var shareBytes = rawShare.ToBytes();
            var t = Y * r;
            var v = Utils.XorWithHash(t, shareBytes);
            var w = Utils.HashToG2(u, v) * r;
            return new EncryptedShare(u, v, w, rawShare.Id);
        }

        public PartiallyDecryptedShare Decode(TPKEPartiallyDecryptedShareMessage message)
        {
            var u = G1.FromBytes(message.Share.ToByteArray());
            return new PartiallyDecryptedShare(u, message.DecryptorId, message.ShareId);
        }

        public TPKEPartiallyDecryptedShareMessage Encode(PartiallyDecryptedShare share)
        {
            return new TPKEPartiallyDecryptedShareMessage
            {
                Share = ByteString.CopyFrom(G1.ToBytes(share.Ui)),
                DecryptorId = share.DecryptorId,
                ShareId = share.ShareId
            };
        }

        public RawShare FullDecrypt(EncryptedShare share, List<PartiallyDecryptedShare> us)
        {
            if (us.Count < _t)
            {
                throw new Exception("Insufficient number of shares!");
            }

            var ids = new HashSet<int>();
            foreach (var part in us)
            {
                if (ids.Contains(part.DecryptorId))
                    throw new Exception($"Id {part.DecryptorId} was provided more than once!");
                if (part.ShareId != share.Id)
                    throw new Exception($"Share id mismatch for decryptor {part.DecryptorId}");
                ids.Add(part.DecryptorId);
            }

            var ys = new List<G1>();
            var xs = new List<Fr>();

            foreach (var part in us)
            {
                xs.Add(Fr.FromInt(part.DecryptorId + 1));
                ys.Add(part.Ui);
            }

            var u = Mcl.LagrangeInterpolateG1(xs.ToArray(), ys.ToArray());
            return new RawShare(Utils.XorWithHash(u, share.V), share.Id);
        }

        public byte[] ToByteArray()
        {
            return BitConverter.GetBytes(_t).Concat(G1.ToBytes(Y)).ToArray();
        }

        public static PublicKey FromBytes(byte[] buffer)
        {
            var decT = BitConverter.ToInt32(buffer, 0);
            var decY = G1.FromBytes(buffer.Skip(4).ToArray());
            return new PublicKey(decY, decT);
        }
    }
}