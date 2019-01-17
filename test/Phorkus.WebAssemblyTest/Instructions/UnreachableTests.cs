﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using Phorkus.WebAssembly;
using Phorkus.WebAssembly.Instructions;

namespace Phorkus.VirtualMachineTest.Instructions
{
	/// <summary>
	/// Tests the <see cref="Unreachable"/> instruction.
	/// </summary>
	[TestClass]
	public class UnreachableTests
	{
		/// <summary>
		/// Tests compilation and execution of the <see cref="Unreachable"/> instruction.
		/// </summary>
		[TestMethod]
		public void Unreachable_Compiled()
		{
			Assert.ThrowsException<UnreachableException>(() => {
                AssemblyBuilder.CreateInstance<dynamic>("Test", null, new Unreachable(), new End()).Test();
                });
		}
	}
}