﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using Phorkus.WebAssembly.Instructions;

namespace Phorkus.WebAssemblyTest.Instructions
{
	/// <summary>
	/// Tests the <see cref="Int32TruncateUnsignedFloat64"/> instruction.
	/// </summary>
	[TestClass]
	public class Int32TruncateUnsignedFloat64Tests
	{
		/// <summary>
		/// Tests compilation and execution of the <see cref="Int32TruncateUnsignedFloat64"/> instruction.
		/// </summary>
		[TestMethod]
		public void Int32TruncateUnsignedFloat64_Compiled()
		{
			var exports = ConversionTestBase<double, int>.CreateInstance(
				new GetLocal(0),
				new Int32TruncateUnsignedFloat64(),
				new End());

			foreach (var value in new[] { 0, 1.5, -1.5 })
				Assert.AreEqual((int)value, exports.Test(value));

			const double exceptional = 123445678901234.0;
			Assert.ThrowsException<System.OverflowException>(() => exports.Test(exceptional));
		}
	}
}