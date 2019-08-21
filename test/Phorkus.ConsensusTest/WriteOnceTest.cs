using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Phorkus.Consensus;
using Phorkus.Consensus.BinaryAgreement;
using Phorkus.Consensus.Messages;
using Phorkus.Utility.Utils;

namespace Phorkus.ConsensusTest
{
    [TestFixture]
    public class WriteOnceTest
    {
        // todo fix WriteOnce
        [Test]
        public void ValuePreservationTest()
        {
            var value = new WriteOnce<int>();
            value.Value = 10;
            Assert.True(value.Equals(10));
        }
    }
}