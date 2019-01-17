using System.Reflection.Emit;

namespace Phorkus.WebAssembly.Instructions
{
	/// <summary>
	/// Load 1 byte and sign-extend i8 to i64.
	/// </summary>
	public class Int64Load8Signed : MemoryReadInstruction
	{
		/// <summary>
		/// Always <see cref="Int64Load8Signed"/>.
		/// </summary>
		public sealed override OpCode OpCode => OpCode.Int64Load8Signed;

		/// <summary>
		/// Creates a new  <see cref="Int64Load8Signed"/> instance.
		/// </summary>
		public Int64Load8Signed()
		{
		}

		internal Int64Load8Signed(Reader reader)
			: base(reader)
		{
		}

		internal sealed override ValueType Type => ValueType.Int64;

		internal sealed override byte Size => 1;

		internal sealed override System.Reflection.Emit.OpCode EmittedOpCode => OpCodes.Ldind_I1;
	}
}