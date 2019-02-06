﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using Phorkus.WebAssembly;
using Phorkus.WebAssembly.Instructions;

namespace Phorkus.WebAssemblyTest.Instructions
{
    /// <summary>
    /// Tests the <see cref="Float32CopySign"/> instruction.
    /// </summary>
    [TestClass]
    public class Float32CopySignTests
    {
        /// <summary>
        /// Tests compilation and execution of the <see cref="Float32CopySign"/> instruction.
        /// </summary>
        [TestMethod]
        public void Float32CopySign_Compiled()
        {
            var exports = CompilerTestBase2<float>.CreateInstance(
                new GetLocal(0),
                new GetLocal(1),
                new Float32CopySign(),
                new End());

            Assert.AreEqual(1, exports.Test(1, +2));
            Assert.AreEqual(-1, exports.Test(1, -2));
            Assert.AreEqual(-float.PositiveInfinity, exports.Test(float.PositiveInfinity, -2));
            Assert.AreEqual(-float.NaN, exports.Test(float.NaN, -2));
        }
    }
}