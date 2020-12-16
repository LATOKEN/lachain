using System.Numerics;
using Lachain.Proto;
using Lachain.Storage.Trie;
using Lachain.Utility;
using Lachain.Utility.Utils;
using NUnit.Framework;

namespace Lachain.UtilityTest
{
    public class MoneyTest
    {
        [Test]
        public void Test_Money_Converting()
        {
            var money = Money.Parse("1.0");
            Assert.IsTrue(VerifyMoney(money.ToUInt256()));

            Assert.AreEqual(
                new BigInteger(900000) * BigInteger.Pow(new BigInteger(10), Money.DecimalDigits),
                Money.Parse("900000").ToUInt256().ToBigInteger()
            );
        }
        
        private static bool VerifyMoney(UInt256 value)
        {
            return value.ToBigInteger() <= Money.MaxValue.ToUInt256().ToBigInteger();
        }
    }
}