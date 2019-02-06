﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using Phorkus.WebAssembly;
using Phorkus.WebAssembly.Instructions;

namespace Phorkus.WebAssemblyTest.Instructions
{
    /// <summary>
    /// Tests the <see cref="Float32DemoteFloat64"/> instruction.
    /// </summary>
    [TestClass]
    public class Float32DemoteFloat64Tests
    {
        /// <summary>
        /// Tests compilation and execution of the <see cref="Float32DemoteFloat64"/> instruction.
        /// </summary>
        [TestMethod]
        public void Float32DemoteFloat64_Compiled()
        {
            var exports = ConversionTestBase<double, float>.CreateInstance(
                new GetLocal(0),
                new Float32DemoteFloat64(),
                new End());

            foreach (var value in Samples.Double)
                Assert.AreEqual((float) value, exports.Test(value));
        }
    }
}