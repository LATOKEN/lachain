using System.Collections.Generic;

namespace Phorkus.Hermes
{
    public interface IPartyManager
    {
        IGeneratorProtocol CreateGeneratorProtocol(uint parties, uint threshold, ulong keyLength);

        ISignerProtocol CreateSignerProtocol(byte[] share, IEnumerable<byte> privateKey, byte[] publicKey, string curveType);
    }
}