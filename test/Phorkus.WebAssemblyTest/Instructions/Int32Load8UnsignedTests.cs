﻿using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Phorkus.WebAssembly;
using Phorkus.WebAssembly.Instructions;

namespace Phorkus.WebAssemblyTest.Instructions
{
    /// <summary>
    /// Tests the <see cref="Int32Load8Unsigned"/> instruction.
    /// </summary>
    [TestClass]
    public class Int32Load8UnsignedTests
    {
        /// <summary>
        /// Tests compilation and execution of the <see cref="Int32Load8Unsigned"/> instruction.
        /// </summary>
        [TestMethod]
        public void Int32Load8Unsigned_Compiled_Offset0()
        {
            var compiled = MemoryReadTestBase<int>.CreateInstance(
                new GetLocal(),
                new Int32Load8Unsigned(),
                new End()
            );

            using (compiled)
            {
                Assert.IsNotNull(compiled);
                Assert.IsNotNull(compiled.Exports);
                var memory = compiled.Exports.Memory;
                Assert.AreNotEqual(IntPtr.Zero, memory.Start);

                var exports = compiled.Exports;
                Assert.AreEqual(0, exports.Test(0));

                var testData = Samples.Memory;
                Marshal.Copy(testData, 0, memory.Start, testData.Length);
                Assert.AreEqual(254, exports.Test(0));
                Assert.AreEqual(2, exports.Test(1));
                Assert.AreEqual(3, exports.Test(2));
                Assert.AreEqual(4, exports.Test(3));
                Assert.AreEqual(5, exports.Test(4));
                Assert.AreEqual(6, exports.Test(5));
                Assert.AreEqual(7, exports.Test(6));
                Assert.AreEqual(8, exports.Test(7));
                Assert.AreEqual(61, exports.Test(8));

                Assert.AreEqual(0, exports.Test((int) Memory.PageSize - 4));

                MemoryAccessOutOfRangeException x;

                x = Assert.ThrowsException<MemoryAccessOutOfRangeException>(() => exports.Test((int) Memory.PageSize));
                Assert.AreEqual(Memory.PageSize, x.Offset);
                Assert.AreEqual(1u, x.Length);

                Assert.ThrowsException<OverflowException>(() => exports.Test(unchecked((int) uint.MaxValue)));
            }
        }

        /// <summary>
        /// Tests compilation and execution of the <see cref="Int32Load8Unsigned"/> instruction.
        /// </summary>
        [TestMethod]
        public void Int32Load8Unsigned_Compiled_Offset1()
        {
            var compiled = MemoryReadTestBase<int>.CreateInstance(
                new GetLocal(),
                new Int32Load8Unsigned
                {
                    Offset = 1,
                },
                new End()
            );

            using (compiled)
            {
                Assert.IsNotNull(compiled);
                Assert.IsNotNull(compiled.Exports);
                var memory = compiled.Exports.Memory;
                Assert.AreNotEqual(IntPtr.Zero, memory.Start);

                var exports = compiled.Exports;
                Assert.AreEqual(0, exports.Test(0));

                var testData = Samples.Memory;
                Marshal.Copy(testData, 0, memory.Start, testData.Length);
                Assert.AreEqual(2, exports.Test(0));
                Assert.AreEqual(3, exports.Test(1));
                Assert.AreEqual(4, exports.Test(2));
                Assert.AreEqual(5, exports.Test(3));
                Assert.AreEqual(6, exports.Test(4));
                Assert.AreEqual(7, exports.Test(5));
                Assert.AreEqual(8, exports.Test(6));
                Assert.AreEqual(61, exports.Test(7));
                Assert.AreEqual(216, exports.Test(8));

                Assert.AreEqual(0, exports.Test((int) Memory.PageSize - 5));

                MemoryAccessOutOfRangeException x;

                x = Assert.ThrowsException<MemoryAccessOutOfRangeException>(() =>
                    exports.Test((int) Memory.PageSize - 1));
                Assert.AreEqual(Memory.PageSize, x.Offset);
                Assert.AreEqual(1u, x.Length);

                Assert.ThrowsException<OverflowException>(() => exports.Test(unchecked((int) uint.MaxValue)));
            }
        }
    }
}