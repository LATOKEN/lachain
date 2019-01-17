using System.Reflection.Emit;

namespace Phorkus.WebAssembly.Instructions
{
	/// <summary>
	/// (No conversion) store 8 bytes.
	/// </summary>
	public class Float64Store : MemoryWriteInstruction
	{
		/// <summary>
		/// Always <see cref="Float64Store"/>.
		/// </summary>
		public sealed override OpCode OpCode => OpCode.Float64Store;

		/// <summary>
		/// Creates a new  <see cref="Float64Store"/> instance.
		/// </summary>
		public Float64Store()
		{
		}

		internal Float64Store(Reader reader)
			: base(reader)
		{
		}

		internal sealed override ValueType Type => ValueType.Float64;

		internal sealed override byte Size => 8;

		internal sealed override System.Reflection.Emit.OpCode EmittedOpCode => OpCodes.Stind_R8;

		internal sealed override HelperMethod StoreHelper => HelperMethod.StoreFloat64;
	}
}