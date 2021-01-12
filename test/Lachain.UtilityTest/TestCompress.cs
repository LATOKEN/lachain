using System;
using System.Linq;
using Lachain.Utility.Utils;
using NUnit.Framework;

namespace Lachain.UtilityTest
{
    public class TestCompress
    {
        [Test]
        public void Test_CompressDecompress()
        {
            var data = Enumerable.Range(0, 10000).SelectMany(_ => "0xdeadbeef".HexToBytes()).ToArray();
            Console.WriteLine($"{data.Length}: {data.ToHex()}");
            var c = CompressUtils.DeflateCompress(data).ToArray();
            Console.WriteLine($"{c.Length}: {c.ToHex()}");
            var restored = CompressUtils.DeflateDecompress(c).ToArray();
            Console.WriteLine($"{restored.ToHex()}");
            Assert.AreEqual(data, restored);
        }
    }
}