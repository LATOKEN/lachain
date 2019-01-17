﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using Phorkus.WebAssembly.Instructions;

namespace Phorkus.VirtualMachineTest.Instructions
{
	/// <summary>
	/// Tests the <see cref="Float32Subtract"/> instruction.
	/// </summary>
	[TestClass]
	public class Float32SubtractTests
	{
		/// <summary>
		/// Tests compilation and execution of the <see cref="Float32Subtract"/> instruction.
		/// </summary>
		[TestMethod]
		public void Float32Subtract_Compiled()
		{
			var exports = CompilerTestBase<float>.CreateInstance(
				new GetLocal(0),
				new Float32Constant(1),
				new Float32Subtract(),
				new End());

			Assert.AreEqual(-1, exports.Test(0));
			Assert.AreEqual(4, exports.Test(5));
		}
	}
}