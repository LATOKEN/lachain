﻿using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Phorkus.WebAssembly;
using Phorkus.WebAssembly.Instructions;

namespace Phorkus.WebAssemblyTest.Instructions
{
    /// <summary>
    /// Tests the <see cref="Float32Nearest"/> instruction.
    /// </summary>
    [TestClass]
    public class Float32NearestTests
    {
        /// <summary>
        /// Tests compilation and execution of the <see cref="Float32Nearest"/> instruction.
        /// </summary>
        [TestMethod]
        public void Float32Nearest_Compiled()
        {
            var exports = CompilerTestBase<float>.CreateInstance(
                new GetLocal(0),
                new Float32Nearest(),
                new End());

            foreach (var value in Samples.Single)
                Assert.AreEqual((float) Math.Round(value, MidpointRounding.ToEven), exports.Test(value));
        }
    }
}