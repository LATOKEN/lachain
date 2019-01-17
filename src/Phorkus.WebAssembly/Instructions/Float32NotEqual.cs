namespace Phorkus.WebAssembly.Instructions
{
	/// <summary>
	/// Compare unordered or unequal.
	/// </summary>
	public class Float32NotEqual : ValueTwoToInt32NotEqualZeroInstruction
	{
		/// <summary>
		/// Always <see cref="Float32NotEqual"/>.
		/// </summary>
		public sealed override OpCode OpCode => OpCode.Float32NotEqual;

		internal sealed override ValueType ValueType => ValueType.Float32;

		internal sealed override System.Reflection.Emit.OpCode EmittedOpCode =>
			System.Reflection.Emit.OpCodes.Ceq; //The result is compared for equality to zero, reversing it.

		/// <summary>
		/// Creates a new  <see cref="Float32NotEqual"/> instance.
		/// </summary>
		public Float32NotEqual()
		{
		}
	}
}