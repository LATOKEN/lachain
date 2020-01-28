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
        static int LEN = 32;
        public int t;

        public PublicKey(G1 _Y, int _t)
        {
            Y = _Y;
            t = _t;
        }

        public EncryptedShare Encrypt(IRawShare rawShare)
        {
            var r = Fr.GetRandom();

            G1 U = G1.Generator * r;

            G1 T = Y * r;
            byte[] V = Utils.XOR(Utils.G(T), rawShare.ToBytes());

            G2 W = Utils.H(U, V) * r;

            return new EncryptedShare(U, V, W, rawShare.Id);
        }

        public PartiallyDecryptedShare Decode(TPKEPartiallyDecryptedShareMessage message)
        {
            var Ui = G1.FromBytes(message.Share.ToByteArray());
            return new PartiallyDecryptedShare(Ui, message.DecryptorId, message.ShareId);
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
            if (us.Count < t)
            {
                throw new Exception("Unsufficient number of shares!");
            }

            var ids = new HashSet<int>();
            foreach (var part in us)
            {
                if (ids.Contains(part.DecryptorId))
                    throw new Exception($"Id {part.DecryptorId} was provided more than once!");
                if (part.ShareId != share.Id)
                    throw new Exception($"Share id mismatch for decryptor {part.DecryptorId}");
            }

            var ys = new List<G1>();
            var xs = new List<Fr>();

            foreach (var part in us)
            {
                xs.Add(Fr.FromInt(part.DecryptorId + 1));
                ys.Add(part.Ui);
            }

            var U = Mcl.LagrangeInterpolateG1(xs.ToArray(), ys.ToArray());
            return new RawShare(Utils.XOR(Utils.G(U), share.V), share.Id);
        }

        public byte[] ToByteArray()
        {
            return BitConverter.GetBytes(t).Concat(G1.ToBytes(Y)).ToArray();
        }

        public static PublicKey FromBytes(byte[] buffer)
        {
            var decT = BitConverter.ToInt32(buffer, 0);
            var decY = G1.FromBytes(buffer.Skip(4).ToArray());
            return new PublicKey(decY, decT);
        }
    }
}