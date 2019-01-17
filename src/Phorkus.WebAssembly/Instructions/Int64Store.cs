using System.Reflection.Emit;

namespace Phorkus.WebAssembly.Instructions
{
	/// <summary>
	/// (No conversion) store 8 bytes.
	/// </summary>
	public class Int64Store : MemoryWriteInstruction
	{
		/// <summary>
		/// Always <see cref="Int64Store"/>.
		/// </summary>
		public sealed override OpCode OpCode => OpCode.Int64Store;

		/// <summary>
		/// Creates a new  <see cref="Int64Store"/> instance.
		/// </summary>
		public Int64Store()
		{
		}

		internal Int64Store(Reader reader)
			: base(reader)
		{
		}

		internal sealed override ValueType Type => ValueType.Int64;

		internal sealed override byte Size => 8;

		internal sealed override System.Reflection.Emit.OpCode EmittedOpCode => OpCodes.Stind_I8;

		internal sealed override HelperMethod StoreHelper => HelperMethod.StoreInt64FromInt64;
	}
}