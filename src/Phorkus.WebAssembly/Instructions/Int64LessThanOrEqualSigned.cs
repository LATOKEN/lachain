namespace Phorkus.WebAssembly.Instructions
{
	/// <summary>
	/// Signed less than or equal.
	/// </summary>
	public class Int64LessThanOrEqualSigned : ValueTwoToInt32NotEqualZeroInstruction
	{
		/// <summary>
		/// Always <see cref="Int64LessThanOrEqualSigned"/>.
		/// </summary>
		public sealed override OpCode OpCode => OpCode.Int64LessThanOrEqualSigned;

		internal sealed override ValueType ValueType => ValueType.Int64;

		internal sealed override System.Reflection.Emit.OpCode EmittedOpCode =>
			System.Reflection.Emit.OpCodes.Cgt; //The result is compared for equality to zero, reversing it.

		/// <summary>
		/// Creates a new  <see cref="Int64LessThanOrEqualSigned"/> instance.
		/// </summary>
		public Int64LessThanOrEqualSigned()
		{
		}
	}
}