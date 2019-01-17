﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using Phorkus.WebAssembly.Instructions;

namespace Phorkus.VirtualMachineTest.Instructions
{
	/// <summary>
	/// Tests the <see cref="Int32GreaterThanUnsigned"/> instruction.
	/// </summary>
	[TestClass]
	public class Int32GreaterThanUnsignedTests
	{
		/// <summary>
		/// Tests compilation and execution of the <see cref="Int32GreaterThanUnsigned"/> instruction.
		/// </summary>
		[TestMethod]
		public void Int32GreaterThanUnsigned_Compiled()
		{
			var exports = ComparisonTestBase<int>.CreateInstance(
				new GetLocal(0),
				new GetLocal(1),
				new Int32GreaterThanUnsigned(),
				new End());

			var values = new uint[]
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
			};

			foreach (var comparand in values)
			{
				foreach (var value in values)
					Assert.AreEqual(comparand > value, exports.Test((int)comparand, (int)value) != 0);

				foreach (var value in values)
					Assert.AreEqual(value > comparand, exports.Test((int)value, (int)comparand) != 0);
			}
		}
	}
}