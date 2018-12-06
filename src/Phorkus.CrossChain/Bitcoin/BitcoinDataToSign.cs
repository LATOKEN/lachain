using System.Collections.Generic;

namespace Phorkus.CrossChain.Bitcoin
{
    public class BitcoinDataToSign : IDataToSign
    {
        public IReadOnlyCollection<byte[]> DataToSign { get; set; }

        public EllipticCurveType EllipticCurveType { get; set; }
    }
}