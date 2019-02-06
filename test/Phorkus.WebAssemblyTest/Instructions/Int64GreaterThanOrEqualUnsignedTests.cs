﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using Phorkus.WebAssembly;
using Phorkus.WebAssembly.Instructions;

namespace Phorkus.WebAssemblyTest.Instructions
{
    /// <summary>
    /// Tests the <see cref="Int64GreaterThanOrEqualUnsigned"/> instruction.
    /// </summary>
    [TestClass]
    public class Int64GreaterThanOrEqualUnsignedTests
    {
        /// <summary>
        /// Tests compilation and execution of the <see cref="Int64GreaterThanOrEqualUnsigned"/> instruction.
        /// </summary>
        [TestMethod]
        public void Int64GreaterThanOrEqualUnsigned_Compiled()
        {
            var exports = ComparisonTestBase<long>.CreateInstance(
                new GetLocal(0),
                new GetLocal(1),
                new Int64GreaterThanOrEqualUnsigned(),
                new End());

            var values = new ulong[]
            {
                0,
                1,
                0x00,
                0x0F,
                0xF0,
                0xFF,
                byte.MaxValue,
                ushort.MaxValue,
                int.MaxValue,
                uint.MaxValue,
                long.MaxValue,
                ulong.MaxValue,
            };

            foreach (var comparand in values)
            {
                foreach (var value in values)
                    Assert.AreEqual(comparand >= value, exports.Test((long) comparand, (long) value) != 0);

                foreach (var value in values)
                    Assert.AreEqual(value >= comparand, exports.Test((long) value, (long) comparand) != 0);
            }
        }
    }
}