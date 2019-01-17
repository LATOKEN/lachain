﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using Phorkus.WebAssembly;
using Phorkus.WebAssembly.Instructions;

namespace Phorkus.VirtualMachineTest.Instructions
{
	/// <summary>
	/// Tests the <see cref="Int64EqualZero"/> instruction.
	/// </summary>
	[TestClass]
	public class Int64EqualZeroTests
	{
		/// <summary>
		/// A simple test class.
		/// </summary>
		public abstract class TestClass
		{
			/// <summary>
			/// A simple test method.
			/// </summary>
			public abstract int Test(long value);
		}

		/// <summary>
		/// Tests compilation and execution of the <see cref="Int64EqualZero"/> instruction.
		/// </summary>
		[TestMethod]
		public void Int64EqualZero_Compiled()
		{
			var exports = AssemblyBuilder.CreateInstance<TestClass>("Test", ValueType.Int32, new[]
			{
					ValueType.Int64,
				},
				new GetLocal(0),
				new Int64EqualZero(),
				new End());

			foreach (var value in Samples.Int64)
				Assert.AreEqual(value == 0, exports.Test(value) != 0);
		}
	}
}