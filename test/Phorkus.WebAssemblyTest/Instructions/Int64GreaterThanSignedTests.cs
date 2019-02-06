﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using Phorkus.WebAssembly;
using Phorkus.WebAssembly.Instructions;

namespace Phorkus.WebAssemblyTest.Instructions
{
    /// <summary>
    /// Tests the <see cref="Int64GreaterThanSigned"/> instruction.
    /// </summary>
    [TestClass]
    public class Int64GreaterThanSignedTests
    {
        /// <summary>
        /// Tests compilation and execution of the <see cref="Int64GreaterThanSigned"/> instruction.
        /// </summary>
        [TestMethod]
        public void Int64GreaterThanSigned_Compiled()
        {
            var exports = ComparisonTestBase<long>.CreateInstance(
                new GetLocal(0),
                new GetLocal(1),
                new Int64GreaterThanSigned(),
                new End());

            var values = new[]
            {
                -1,
                0,
                1,
                0x00,
                0x0F,
                0xF0,
                0xFF,
                byte.MaxValue,
                short.MinValue,
                short.MaxValue,
                ushort.MaxValue,
                int.MinValue,
                int.MaxValue,
                uint.MaxValue,
                long.MinValue,
                long.MaxValue,
            };

            foreach (var comparand in values)
            {
                foreach (var value in values)
                    Assert.AreEqual(comparand > value, exports.Test(comparand, value) != 0);

                foreach (var value in values)
                    Assert.AreEqual(value > comparand, exports.Test(value, comparand) != 0);
            }
        }
    }
}