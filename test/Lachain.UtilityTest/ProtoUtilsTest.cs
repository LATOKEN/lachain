using System.Numerics;
using Lachain.Proto;
using Lachain.Utility;
using Lachain.Utility.Utils;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using System;

namespace Lachain.UtilityTest
{
    public class ProtoUtilsTest
    {
        [Test]
        [Repeat(10)]
        public void Test_ToMessageArrayCorrect()
        {
            List<TransactionReceipt> receipts = new List<TransactionReceipt>();
            for(int i = 0; i < 100; i++) {
                receipts.Add(TestUtils.GetRandomTransaction(false));
            }
            var decoded = receipts.ToByteArray();
            var encoded = decoded.ToMessageArray<TransactionReceipt>();
            Assert.True(receipts.SequenceEqual(encoded));
        }

        [Test]
        [Repeat(10)]
        public void Test_ToMessageArrayExtraBytesAtEnd()
        {
            List<TransactionReceipt> receipts = new List<TransactionReceipt>();
            for(int i = 0; i < 100; i++) {
                receipts.Add(TestUtils.GetRandomTransaction(false));
            }
            var decoded = receipts.ToByteArray();
            var junk = new byte[] {1, 2, 3};
            decoded = decoded.Concat(junk).ToArray();

            bool caughtException = false; 
            try
            {
                var encoded = decoded.ToMessageArray<TransactionReceipt>();
                Assert.IsTrue(1 == 2);
            }
            catch(Exception e)
            {
                // adding junk at the end should throw Exception;
                caughtException = true;
            }
            Assert.IsTrue(caughtException == true);
        }

        [Test]
        public void Test_ToMessageArrayRandomBytes()
        {
            var decoded = new byte[100];
            for(var i = 0; i < 100; i++) 
                decoded[i] = (byte) (i % 256);
            
            bool caughtException = false; 
            try
            {
                var encoded = decoded.ToMessageArray<TransactionReceipt>();
                Assert.IsTrue(1 == 2);
            }
            catch(Exception e)
            {
                // random bytes should not be decoded and throw an exception
                caughtException = true;
            }
            Assert.IsTrue(caughtException == true);
        }
    }
}