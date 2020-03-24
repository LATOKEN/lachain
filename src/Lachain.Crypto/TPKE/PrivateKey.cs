using System;
using System.Linq;
using Lachain.Crypto.MCL.BLS12_381;

namespace Lachain.Crypto.TPKE
{
    public class PrivateKey
    {
        // todo add degree to fields?
        public Fr x;
        public G1 Y;
        public int Id { get; }

        public PrivateKey(Fr _x, int id)
        {
            x = _x;
            Y = G1.Generator * x;
            Id = id;
        }

        public PartiallyDecryptedShare Decrypt(EncryptedShare share)
        {
            var H = Utils.HashToG2(share.U, share.V);
            if (!Mcl.Pairing(G1.Generator, share.W).Equals(Mcl.Pairing(share.U, H)))
            {
                // todo add appropriate catch
                throw new Exception("Invalid share!");
            }

            var Ui = share.U * x;

            return new PartiallyDecryptedShare(Ui, Id, share.Id);
        }

        public byte[] ToByteArray()
        {
            return BitConverter.GetBytes(Id).Concat(Fr.ToBytes(x)).ToArray();
        }

        public static PrivateKey FromBytes(byte[] buffer)
        {
            var decId = BitConverter.ToInt32(buffer, 0);
            var decX = Fr.FromBytes(buffer.Skip(4).ToArray());
            return new PrivateKey(decX, decId);
        }
    }
}