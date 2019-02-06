﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using Phorkus.WebAssembly;
using Phorkus.WebAssembly.Instructions;

namespace Phorkus.WebAssemblyTest.Instructions
{
    /// <summary>
    /// Tests the <see cref="Float64Subtract"/> instruction.
    /// </summary>
    [TestClass]
    public class Float64SubtractTests
    {
        /// <summary>
        /// Tests compilation and execution of the <see cref="Float64Subtract"/> instruction.
        /// </summary>
        [TestMethod]
        public void Float64Subtract_Compiled()
        {
            var exports = CompilerTestBase<double>.CreateInstance(
                new GetLocal(0),
                new Float64Constant(1),
                new Float64Subtract(),
                new End());

            Assert.AreEqual(-1, exports.Test(0));
            Assert.AreEqual(4, exports.Test(5));
        }
    }
}