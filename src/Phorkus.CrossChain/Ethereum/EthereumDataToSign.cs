using System.Collections.Generic;

namespace Phorkus.CrossChain.Ethereum
{
    public class EthereumDataToSign : IDataToSign
    {
        
        public IReadOnlyCollection<byte[]> DataToSign { get; set; }
         
        public EllipticCurveType EllipticCurveType { get; set; }
    }
}