using System.Collections.Generic;
using Phorkus.Proto;

namespace Phorkus.Core.Hermes
{
    public interface IHermesSigner
    {
        ThresholdKey GeneratePrivateKey(IEnumerable<PublicKey> validators);
        
        Signature SignData(IEnumerable<PublicKey> validators, string curveType, byte[] data);
    }
}