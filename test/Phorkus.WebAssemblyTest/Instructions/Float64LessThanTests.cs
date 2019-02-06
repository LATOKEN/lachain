﻿using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Phorkus.WebAssembly;
using Phorkus.WebAssembly.Instructions;

namespace Phorkus.WebAssemblyTest.Instructions
{
    /// <summary>
    /// Tests the <see cref="Float64LessThan"/> instruction.
    /// </summary>
    [TestClass]
    public class Float64LessThanTests
    {
        /// <summary>
        /// Tests compilation and execution of the <see cref="Float64LessThan"/> instruction.
        /// </summary>
        [TestMethod]
        public void Float64LessThan_Compiled()
        {
            var exports = ComparisonTestBase<double>.CreateInstance(
                new GetLocal(0),
                new GetLocal(1),
                new Float64LessThan(),
                new End());

            var values = new[]
            {
                0.0,
                1.0,
                -1.0,
                -Math.PI,
                Math.PI,
                double.NaN,
                double.NegativeInfinity,
                double.PositiveInfinity,
                double.Epsilon,
                -double.Epsilon,
            };

            foreach (var comparand in values)
            {
                foreach (var value in values)
                    Assert.AreEqual(comparand < value, exports.Test(comparand, value) != 0);

                foreach (var value in values)
                    Assert.AreEqual(value < comparand, exports.Test(value, comparand) != 0);
            }
        }
    }
}