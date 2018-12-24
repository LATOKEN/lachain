using System.Collections.Generic;

namespace Phorkus.CrossChain
{
    public class DataToSign
    {
        public IReadOnlyCollection<byte> TransactionHash { get; set; }
        
        public EllipticCurveType EllipticCurveType { get; set; }
    }
}