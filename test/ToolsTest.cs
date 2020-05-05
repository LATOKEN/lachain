using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Phorkus.Consensus.ReliableBroadcast;
using Phorkus.Crypto.MCL.BLS12_381;
using Phorkus.Crypto.TPKE;

namespace Phorkus.ConsensusTest
{
    [TestFixture]
    public class ToolsTest
    {

        [SetUp]
        public void SetUp()
        {
            Mcl.Init();
        }

        [Test]
        public void TestConversionByteToInt()
        {
            Console.WriteLine(ByteToInt(new byte[] {1, 2, 3, 4}).SequenceEqual(new[] {1, 2, 3, 4})
                ? "Test pass"
                : "Test NOT pass");
        }

        
        [Test]
        public void TestToByte()
        {
            var encryptedShare1 = new EncryptedShare(G1.Generator, new byte[]{1,2,3,4,5,6,6}, G2.Generator, 0);
            var serializedEncryptedShare = encryptedShare1.ToByte();
            var encryptedShare2 = EncryptedShare.FromByte(serializedEncryptedShare);
            Console.WriteLine(encryptedShare1.Equals(encryptedShare2) ? "Test pass" : "Test NOT pass");
        }

        [Test]
        [Repeat(100)]
        public void TestCorrectInput()
        {
            var N = 22;
            var rnd = new Random();
            var randomLengthInput = rnd.Next(1000, 5000);
            var randomInput = ReliableBroadcast.GetInput(randomLengthInput);
            var correctInput = RBTools.GetCorrectInput(randomInput, N);
            Console.WriteLine(correctInput.Count % N == 0 
                ? $"Test pass Length: {randomLengthInput} remainder: {randomLengthInput % N} correctInputLength: {correctInput.Count}" 
                : "Test NOT pass");
            
            Console.WriteLine(randomInput.SequenceEqual(RBTools.GetOriginalInput(correctInput.ToArray()))
                ? "EQUAL"
                : "NOT EQUAL");
        }        

        
        private int[] ByteToInt(byte[] bytes)
        {
            const int limit = byte.MaxValue;
            var result = new int[bytes.Length];
            for (var i = 0; i < bytes.Length; i++)
            {
                if(bytes[i] <= limit)
                    result[i] = bytes[i];
            }
            return result;
        }
    }
}