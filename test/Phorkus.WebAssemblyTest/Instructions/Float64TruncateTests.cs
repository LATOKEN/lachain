﻿using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Phorkus.WebAssembly.Instructions;

namespace Phorkus.WebAssemblyTest.Instructions
{
	/// <summary>
	/// Tests the <see cref="Float64Truncate"/> instruction.
	/// </summary>
	[TestClass]
	public class Float64TruncateTests
	{
		/// <summary>
		/// Tests compilation and execution of the <see cref="Float64Truncate"/> instruction.
		/// </summary>
		[TestMethod]
		public void Float64Truncate_Compiled()
		{
			var exports = CompilerTestBase<double>.CreateInstance(
				new GetLocal(0),
				new Float64Truncate(),
				new End());

			foreach (var value in new[] { 1f, -1f, -Math.PI, Math.PI })
				Assert.AreEqual(Math.Truncate(value), exports.Test(value));
		}
	}
}