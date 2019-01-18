﻿using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Phorkus.WebAssembly.Instructions;

namespace Phorkus.WebAssemblyTest.Instructions
{
	/// <summary>
	/// Tests the <see cref="Float64ReinterpretInt64"/> instruction.
	/// </summary>
	[TestClass]
	public class Float64ReinterpretInt64Tests
	{
		[StructLayout(LayoutKind.Explicit)]
		struct Overlap64
		{
			[FieldOffset(0)]
			public long Int64;

			[FieldOffset(0)]
			public double Float64;
		}

		/// <summary>
		/// Tests compilation and execution of the <see cref="Float64ReinterpretInt64"/> instruction.
		/// </summary>
		[TestMethod]
		public void Float64ReinterpretInt64_Compiled()
		{
			var exports = ConversionTestBase<long, double>.CreateInstance(
				new GetLocal(0),
				new Float64ReinterpretInt64(),
				new End());

			foreach (var value in Samples.Int64)
				Assert.AreEqual(new Overlap64 { Int64 = value }.Float64, exports.Test(value));
		}
	}
}