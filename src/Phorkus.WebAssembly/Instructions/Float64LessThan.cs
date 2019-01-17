namespace Phorkus.WebAssembly.Instructions
{
	/// <summary>
	/// Compare ordered and less than.
	/// </summary>
	public class Float64LessThan : ValueTwoToInt32Instruction
	{
		/// <summary>
		/// Always <see cref="Float64LessThan"/>.
		/// </summary>
		public sealed override OpCode OpCode => OpCode.Float64LessThan;

		internal sealed override ValueType ValueType => ValueType.Float64;

		internal sealed override System.Reflection.Emit.OpCode EmittedOpCode =>
			System.Reflection.Emit.OpCodes.Clt;


		/// <summary>
		/// Creates a new  <see cref="Float64LessThan"/> instance.
		/// </summary>
		public Float64LessThan()
		{
		}
	}
}