namespace Phorkus.WebAssembly.Instructions
{
	/// <summary>
	/// Signed remainder (result has the sign of the dividend).
	/// </summary>
	public class Int32RemainderSigned : ValueTwoToOneInstruction
	{
		/// <summary>
		/// Always <see cref="Int32RemainderSigned"/>.
		/// </summary>
		public sealed override OpCode OpCode => OpCode.Int32RemainderSigned;

		internal sealed override ValueType ValueType => ValueType.Int32;

		internal sealed override System.Reflection.Emit.OpCode EmittedOpCode =>
			System.Reflection.Emit.OpCodes.Rem;

		/// <summary>
		/// Creates a new  <see cref="Int32RemainderSigned"/> instance.
		/// </summary>
		public Int32RemainderSigned()
		{
		}
	}
}