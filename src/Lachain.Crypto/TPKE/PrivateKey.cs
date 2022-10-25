using System;
using Lachain.Crypto.ThresholdEncryption;
using Lachain.Utility.Serialization;
using MCL.BLS12_381.Net;

namespace Lachain.Crypto.TPKE
{
    public class PrivateKey : IFixedWidth
    {
        private readonly Fr _x;
        private readonly int _id;

        public PrivateKey(Fr x, int id)
        {
            _x = x;
            _id = id;
        }

        public PartiallyDecryptedShare Decrypt(EncryptedShare share)
        {
            var h = Utils.HashToG2(share.U, share.V);
            if (!GT.Pairing(G1.Generator, share.W).Equals(GT.Pairing(share.U, h)))
                throw new Exception("Invalid share!");
            var ui = share.U * _x;
            return new PartiallyDecryptedShare(ui, _id, share.Id);
        }

        public static PrivateKey FromBytes(ReadOnlyMemory<byte> bytes)
        {
            var res = FixedWithSerializer.Deserialize(bytes, out _, typeof(int), typeof(Fr));
            return new PrivateKey((Fr) res[1], (int) res[0]);
        }

        public void Serialize(Memory<byte> bytes)
        {
            FixedWithSerializer.SerializeToMemory(bytes, new dynamic[] {_id, _x});
        }

        public static int Width()
        {
            return sizeof(int) + Fr.ByteSize;
        }
    }
}