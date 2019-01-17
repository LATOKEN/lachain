namespace Phorkus.WebAssembly.Instructions
{
	/// <summary>
	/// Signed greater than or equal.
	/// </summary>
	public class Int64GreaterThanOrEqualSigned : ValueTwoToInt32NotEqualZeroInstruction
	{
		/// <summary>
		/// Always <see cref="Int64GreaterThanOrEqualSigned"/>.
		/// </summary>
		public sealed override OpCode OpCode => OpCode.Int64GreaterThanOrEqualSigned;

		internal sealed override ValueType ValueType => ValueType.Int64;

		internal sealed override System.Reflection.Emit.OpCode EmittedOpCode =>
			System.Reflection.Emit.OpCodes.Clt; //The result is compared for equality to zero, reversing it.

		/// <summary>
		/// Creates a new  <see cref="Int64GreaterThanOrEqualSigned"/> instance.
		/// </summary>
		public Int64GreaterThanOrEqualSigned()
		{
		}
	}
}