﻿using System.Reflection.Emit;

namespace Phorkus.WebAssembly.Instructions
{
	/// <summary>
	/// (No conversion) store 4 bytes.
	/// </summary>
	public class Int32Store : MemoryWriteInstruction
	{
		/// <summary>
		/// Always <see cref="Int32Store"/>.
		/// </summary>
		public sealed override OpCode OpCode => OpCode.Int32Store;

		/// <summary>
		/// Creates a new  <see cref="Int32Store"/> instance.
		/// </summary>
		public Int32Store()
		{
		}

		internal Int32Store(Reader reader)
			: base(reader)
		{
		}

		internal sealed override ValueType Type => ValueType.Int32;

		internal sealed override byte Size => 4;

		internal sealed override System.Reflection.Emit.OpCode EmittedOpCode => OpCodes.Stind_I4;

		internal sealed override HelperMethod StoreHelper => HelperMethod.StoreInt32FromInt32;
	}
}