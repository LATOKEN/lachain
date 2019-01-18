﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using Phorkus.WebAssembly.Instructions;

namespace Phorkus.WebAssemblyTest.Instructions
{
	/// <summary>
	/// Tests the <see cref="Int32Or"/> instruction.
	/// </summary>
	[TestClass]
	public class Int32OrTests
	{
		/// <summary>
		/// Tests compilation and execution of the <see cref="Int32Or"/> instruction.
		/// </summary>
		[TestMethod]
		public void Int32Or_Compiled()
		{
			const int comparand = 0xF;

			var exports = CompilerTestBase<int>.CreateInstance(
				new GetLocal(0),
				new Int32Constant(comparand),
				new Int32Or(),
				new End());

			foreach (var value in new[] { 0x00, 0x0F, 0xF0, 0xFF, })
				Assert.AreEqual(value | comparand, exports.Test(value));
		}
	}
}