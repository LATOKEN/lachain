namespace Phorkus.WebAssembly.Instructions
{
	/// <summary>
	/// Unsigned division (result is floored).
	/// </summary>
	public class Int32DivideUnsigned : ValueTwoToOneInstruction
	{
		/// <summary>
		/// Always <see cref="Int32DivideUnsigned"/>.
		/// </summary>
		public sealed override OpCode OpCode => OpCode.Int32DivideUnsigned;

		internal sealed override ValueType ValueType => ValueType.Int32;

		internal sealed override System.Reflection.Emit.OpCode EmittedOpCode =>
			System.Reflection.Emit.OpCodes.Div_Un;

		/// <summary>
		/// Creates a new  <see cref="Int32DivideUnsigned"/> instance.
		/// </summary>
		public Int32DivideUnsigned()
		{
		}
	}
}