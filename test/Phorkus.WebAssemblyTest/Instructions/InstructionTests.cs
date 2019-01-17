using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Phorkus.WebAssembly;

namespace Phorkus.VirtualMachineTest.Instructions
{
	/// <summary>
	/// Tests <see cref="Instruction"/> inheritors for proper behavior
	/// </summary>
	[TestClass]
	public class InstructionTests
	{
		/// <summary>
		/// Ensures that all instructions are public.
		/// </summary>
		[TestMethod]
		public void Instruction_AllPublic()
		{
			var nonPublic = string.Join(", ",
			typeof(Instruction)
				.GetTypeInfo()
				.Assembly.GetTypes()
				.Where(type => type.IsDescendantOf(typeof(Instruction)) && type.GetTypeInfo().IsPublic == false)
				);

			Assert.AreEqual("", nonPublic, $"Non-public instructions: {nonPublic}");
		}
	}
}