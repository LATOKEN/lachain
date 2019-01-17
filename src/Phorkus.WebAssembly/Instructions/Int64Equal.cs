namespace Phorkus.WebAssembly.Instructions
{
	/// <summary>
	/// Sign-agnostic compare equal.
	/// </summary>
	public class Int64Equal : ValueTwoToInt32Instruction
	{
		/// <summary>
		/// Always <see cref="Int64Equal"/>.
		/// </summary>
		public sealed override OpCode OpCode => OpCode.Int64Equal;

		internal sealed override ValueType ValueType => ValueType.Int64;

		internal sealed override System.Reflection.Emit.OpCode EmittedOpCode =>
			System.Reflection.Emit.OpCodes.Ceq;

		/// <summary>
		/// Creates a new  <see cref="Int64Equal"/> instance.
		/// </summary>
		public Int64Equal()
		{
		}
	}
}