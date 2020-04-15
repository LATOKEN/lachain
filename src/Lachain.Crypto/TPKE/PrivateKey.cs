using System;
using System.Linq;
using Lachain.Crypto.MCL.BLS12_381;

namespace Lachain.Crypto.TPKE
{
    public class PrivateKey
    {
        // todo add degree to fields?
        public Fr X;
        private int Id { get; }

        public PrivateKey(Fr x, int id)
        {
            X = x;
            Id = id;
        }

        public PartiallyDecryptedShare Decrypt(EncryptedShare share)
        {
            var h = Utils.HashToG2(share.U, share.V);
            if (!Mcl.Pairing(G1.Generator, share.W).Equals(Mcl.Pairing(share.U, h)))
            {
                // todo add appropriate catch
                throw new Exception("Invalid share!");
            }

            var ui = share.U * X;
            return new PartiallyDecryptedShare(ui, Id, share.Id);
        }

        public byte[] ToByteArray()
        {
            return BitConverter.GetBytes(Id).Concat(Fr.ToBytes(X)).ToArray();
        }

        public static PrivateKey FromBytes(byte[] buffer)
        {
            var decId = BitConverter.ToInt32(buffer, 0);
            var decX = Fr.FromBytes(buffer.Skip(4).ToArray());
            return new PrivateKey(decX, decId);
        }
    }
}