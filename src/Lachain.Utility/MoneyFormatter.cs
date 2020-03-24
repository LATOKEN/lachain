using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Lachain.Utility
{
    public static class MoneyFormatter
    {
        public static BigInteger Ten = new BigInteger(10);

        public static Money FormatMoney(BigInteger value, int fromDecimals)
        {
            if (Money.DecimalDigits >= fromDecimals)
                return new Money(value * BigInteger.Pow(Ten, Money.DecimalDigits - fromDecimals));
            return new Money(value / BigInteger.Pow(Ten, fromDecimals - Money.DecimalDigits));
        }
        
        public static Money FormatMoney(IEnumerable<byte> buffer, int fromDecimals)
        {
            var value = new BigInteger(buffer.Concat(new byte[1]).ToArray());
            return FormatMoney(value, fromDecimals);
        }
    }
}