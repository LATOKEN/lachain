namespace Phorkus.WebAssembly.Instructions
{
	/// <summary>
	/// Unsigned greater than or equal.
	/// </summary>
	public class Int32GreaterThanOrEqualUnsigned : ValueTwoToInt32NotEqualZeroInstruction
	{
		/// <summary>
		/// Always <see cref="Int32GreaterThanOrEqualUnsigned"/>.
		/// </summary>
		public sealed override OpCode OpCode => OpCode.Int32GreaterThanOrEqualUnsigned;

		internal sealed override ValueType ValueType => ValueType.Int32;

		internal sealed override System.Reflection.Emit.OpCode EmittedOpCode =>
			System.Reflection.Emit.OpCodes.Clt_Un; //The result is compared for equality to zero, reversing it.

		/// <summary>
		/// Creates a new  <see cref="Int32GreaterThanOrEqualUnsigned"/> instance.
		/// </summary>
		public Int32GreaterThanOrEqualUnsigned()
		{
		}
	}
}