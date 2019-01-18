﻿using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Phorkus.WebAssembly.Instructions;

namespace Phorkus.WebAssemblyTest.Instructions
{
	/// <summary>
	/// Tests the <see cref="Float32SquareRoot"/> instruction.
	/// </summary>
	[TestClass]
	public class Float32SquareRootTests
	{
		/// <summary>
		/// Tests compilation and execution of the <see cref="Float32SquareRoot"/> instruction.
		/// </summary>
		[TestMethod]
		public void Float32SquareRoot_Compiled()
		{
			var exports = CompilerTestBase<float>.CreateInstance(
				new GetLocal(0),
				new Float32SquareRoot(),
				new End());

			foreach (var value in Samples.Single)
				Assert.AreEqual((float)Math.Sqrt(value), exports.Test(value));
		}
	}
}