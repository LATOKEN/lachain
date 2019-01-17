using System.Reflection.Emit;

namespace Phorkus.WebAssembly.Instructions
{
	/// <summary>
	/// Negation.
	/// </summary>
	public class Float64Negate : ValueOneToOneInstruction
	{
		/// <summary>
		/// Always <see cref="Float64Negate"/>.
		/// </summary>
		public sealed override OpCode OpCode => OpCode.Float64Negate;

		/// <summary>
		/// Creates a new  <see cref="Float64Negate"/> instance.
		/// </summary>
		public Float64Negate()
		{
		}

		internal sealed override System.Reflection.Emit.OpCode EmittedOpCode => OpCodes.Neg;

		internal sealed override ValueType ValueType => ValueType.Float64;
	}
}