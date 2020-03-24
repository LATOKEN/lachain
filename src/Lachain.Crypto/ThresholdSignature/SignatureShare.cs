using System.Collections.Generic;
using System.Linq;
using Lachain.Crypto.MCL.BLS12_381;

namespace Lachain.Crypto.ThresholdSignature
{
    public class SignatureShare : Signature
    {
        internal SignatureShare(G2 signature) : base(signature)
        {
        }

        public new static SignatureShare FromBytes(IEnumerable<byte> bytes)
        {
            var byteArray = bytes as byte[] ?? bytes.ToArray();
            return new SignatureShare(G2.FromBytes(byteArray));
        }
    }
}