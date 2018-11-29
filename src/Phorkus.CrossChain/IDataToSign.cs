using System.Collections.Generic;

namespace Phorkus.CrossChain
{
    public interface IDataToSign
    {
        IReadOnlyCollection<byte[]> DataToSign { get; }
        
        EllipticCurveType EllipticCurveType { get; }
    }
}