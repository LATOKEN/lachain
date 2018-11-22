using System;
using System.Linq;
using System.Numerics;

namespace Phorkus.Core.Cryptography
{
    public class __PublicKey : IComparable<__PublicKey>
    {
        /// <summary>
        /// Infinity
        /// </summary>
        public static readonly __PublicKey Infinity = new __PublicKey();

        /// <summary>
        /// Encoded data
        /// </summary>
        public readonly byte[] EncodedData;
        /// <summary>
        /// Decoded data
        /// </summary>
        public readonly byte[] DecodedData;
        /// <summary>
        /// Is infinite
        /// </summary>
        public readonly bool IsInfinity;

        /// <summary>
        /// X,Y
        /// </summary>
        public readonly BigInteger X, Y;

        /// <summary>
        /// Infinite constructor
        /// </summary>
        private __PublicKey()
        {
            IsInfinity = true;

            DecodedData = EncodedData = new byte[] { 0x00 };

            X = BigInteger.Zero;
            Y = BigInteger.Zero;
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="point">Point</param>
        public __PublicKey(byte[] point)
        {
            if (point == null || point.All(u => u == 0x00))
            {
                IsInfinity = true;

                DecodedData = EncodedData = new byte[] { 0x00 };

                X = BigInteger.Zero;
                Y = BigInteger.Zero;
            }
            else
            {
                IsInfinity = false;

//                EncodedData = Crypto.Default.DecodePublicKey(point, true, out X, out Y);
//                DecodedData = Crypto.Default.DecodePublicKey(point, false, out X, out Y);
            }
        }

        /// <summary>
        /// Compare ECPoint
        /// </summary>
        /// <param name="other">ECPoint to compare</param>
        /// <returns>Return compare value</returns>
        public int CompareTo(__PublicKey other)
        {
            if (other == this) return 0;

            int result = X.CompareTo(other.X);
            if (result != 0) return result;

            return Y.CompareTo(other.Y);
        }

        /// <summary>
        /// String representation
        /// </summary>
        public override string ToString()
        {
//            return EncodedData.ToHexString();
            throw new NotImplementedException();
        }
        
        /// <summary>
        /// String representation
        /// </summary>
        /// <param name="append0x">Append 0x</param>
        public string ToString(bool append0x)
        {
            throw new NotImplementedException();
//            return EncodedData.ToHexString(append0x);
        }
    }
}
