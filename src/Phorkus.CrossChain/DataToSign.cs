namespace Phorkus.CrossChain
{
    public class DataToSign
    {
        public byte[] TransactionHash { get; set; }
        
        public EllipticCurveType EllipticCurveType { get; set; }
    }
}