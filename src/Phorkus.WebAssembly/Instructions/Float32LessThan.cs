namespace Phorkus.WebAssembly.Instructions
{
	/// <summary>
	/// Compare ordered and less than.
	/// </summary>
	public class Float32LessThan : ValueTwoToInt32Instruction
	{
		/// <summary>
		/// Always <see cref="Float32LessThan"/>.
		/// </summary>
		public sealed override OpCode OpCode => OpCode.Float32LessThan;

		internal sealed override ValueType ValueType => ValueType.Float32;

		internal sealed override System.Reflection.Emit.OpCode EmittedOpCode =>
			System.Reflection.Emit.OpCodes.Clt;

		/// <summary>
		/// Creates a new  <see cref="Float32LessThan"/> instance.
		/// </summary>
		public Float32LessThan()
		{
		}
	}
}