using System.Reflection.Emit;

namespace Phorkus.WebAssembly.Instructions
{
    /// <summary>
    /// Load 4 bytes as i32.
    /// </summary>
    public class Int32Load : MemoryReadInstruction
    {
        /// <summary>
        /// Always <see cref="OpCode.Int32Load"/>.
        /// </summary>
        public sealed override OpCode OpCode => OpCode.Int32Load;

        /// <summary>
        /// Creates a new  <see cref="Int32Load"/> instance.
        /// </summary>
        public Int32Load()
        {
        }

        internal Int32Load(Reader reader)
            : base(reader)
        {
        }

        internal sealed override ValueType Type => ValueType.Int32;

        internal sealed override byte Size => 4;

        internal sealed override System.Reflection.Emit.OpCode EmittedOpCode => OpCodes.Ldind_I4;
    }
}