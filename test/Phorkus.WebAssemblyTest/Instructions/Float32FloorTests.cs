﻿using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Phorkus.WebAssembly.Instructions;

namespace Phorkus.WebAssemblyTest.Instructions
{
	/// <summary>
	/// Tests the <see cref="Float32Floor"/> instruction.
	/// </summary>
	[TestClass]
	public class Float32FloorTests
	{
		/// <summary>
		/// Tests compilation and execution of the <see cref="Float32Floor"/> instruction.
		/// </summary>
		[TestMethod]
		public void Float32Floor_Compiled()
		{
			var exports = CompilerTestBase<float>.CreateInstance(
				new GetLocal(0),
				new Float32Floor(),
				new End());

			foreach (var value in Samples.Single)
				Assert.AreEqual((float)Math.Floor(value), exports.Test(value));
		}
	}
}