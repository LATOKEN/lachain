namespace Phorkus.WebAssembly.Instructions
{
	/// <summary>
	/// Sign-agnostic shift left.
	/// </summary>
	public class Int32ShiftLeft : ValueTwoToOneInstruction
	{
		/// <summary>
		/// Always <see cref="Int32ShiftLeft"/>.
		/// </summary>
		public sealed override OpCode OpCode => OpCode.Int32ShiftLeft;

		internal sealed override ValueType ValueType => ValueType.Int32;

		internal sealed override System.Reflection.Emit.OpCode EmittedOpCode =>
			System.Reflection.Emit.OpCodes.Shl;

		/// <summary>
		/// Creates a new  <see cref="Int32ShiftLeft"/> instance.
		/// </summary>
		public Int32ShiftLeft()
		{
		}
	}
}