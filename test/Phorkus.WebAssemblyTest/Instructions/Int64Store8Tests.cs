﻿using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Phorkus.WebAssembly;
using Phorkus.WebAssembly.Instructions;

namespace Phorkus.WebAssemblyTest.Instructions
{
	/// <summary>
	/// Tests the <see cref="Int64Store8"/> instruction.
	/// </summary>
	[TestClass]
	public class Int64Store8Tests
	{
		/// <summary>
		/// Tests compilation and execution of the <see cref="Int64Store8"/> instruction.
		/// </summary>
		[TestMethod]
		public void Int64Store8_Compiled_Offset0()
		{
			var compiled = MemoryWriteTestBase<long>.CreateInstance(
				new GetLocal(0),
				new GetLocal(1),
				new Int64Store8(),
				new End()
			);
			Assert.IsNotNull(compiled);

			using (compiled)
			{
				Assert.IsNotNull(compiled.Exports);
				var memory = compiled.Exports.Memory;
				Assert.AreNotEqual(IntPtr.Zero, memory.Start);

				var exports = compiled.Exports;
				exports.Test(0, 128);
				Assert.AreEqual(128, Marshal.ReadInt32(memory.Start));
				Assert.AreEqual(0, Marshal.ReadInt32(memory.Start, 1));

				exports.Test((int)Memory.PageSize - 8, 1);

				Assert.AreEqual(1, Marshal.ReadInt64(memory.Start, (int)Memory.PageSize - 8));

				MemoryAccessOutOfRangeException x;

				x = Assert.ThrowsException<MemoryAccessOutOfRangeException>(() => exports.Test((int)Memory.PageSize, 0));
				Assert.AreEqual(Memory.PageSize, x.Offset);
				Assert.AreEqual(1u, x.Length);

				Assert.ThrowsException<OverflowException>(() => exports.Test(unchecked((int)uint.MaxValue), 0));
			}
		}

		/// <summary>
		/// Tests compilation and execution of the <see cref="Int64Store8"/> instruction.
		/// </summary>
		[TestMethod]
		public void Int64Store8_Compiled_Offset1()
		{
			var compiled = MemoryWriteTestBase<long>.CreateInstance(
				new GetLocal(0),
				new GetLocal(1),
				new Int64Store8() { Offset = 1 },
				new End()
			);
			Assert.IsNotNull(compiled);

			using (compiled)
			{
				Assert.IsNotNull(compiled.Exports);
				var memory = compiled.Exports.Memory;
				Assert.AreNotEqual(IntPtr.Zero, memory.Start);

				var exports = compiled.Exports;
				exports.Test(0, 128);
				Assert.AreEqual(32768, Marshal.ReadInt32(memory.Start));
				Assert.AreEqual(128, Marshal.ReadInt32(memory.Start, 1));
				Assert.AreEqual(0, Marshal.ReadInt32(memory.Start, 2));

				exports.Test((int)Memory.PageSize - 8 - 1, 1);

				Assert.AreEqual(1, Marshal.ReadInt64(memory.Start, (int)Memory.PageSize - 8));

				MemoryAccessOutOfRangeException x;

				x = Assert.ThrowsException<MemoryAccessOutOfRangeException>(() => exports.Test((int)Memory.PageSize - 1, 0));
				Assert.AreEqual(Memory.PageSize, x.Offset);
				Assert.AreEqual(1u, x.Length);

				Assert.ThrowsException<OverflowException>(() => exports.Test(unchecked((int)uint.MaxValue), 0));
			}
		}
	}
}