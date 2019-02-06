using System.Reflection.Emit;

namespace Phorkus.WebAssembly.Instructions
{
    /// <summary>
    /// Load 4 bytes and zero-extend i32 to i64.
    /// </summary>
    public class Int64Load32Unsigned : MemoryReadInstruction
    {
        /// <summary>
        /// Always <see cref="OpCode.Int64Load32Unsigned"/>.
        /// </summary>
        public sealed override OpCode OpCode => OpCode.Int64Load32Unsigned;

        /// <summary>
        /// Creates a new  <see cref="Int64Load32Unsigned"/> instance.
        /// </summary>
        public Int64Load32Unsigned()
        {
        }

        internal Int64Load32Unsigned(Reader reader)
            : base(reader)
        {
        }

        internal sealed override ValueType Type => ValueType.Int64;

        internal sealed override byte Size => 4;

        internal sealed override System.Reflection.Emit.OpCode EmittedOpCode => OpCodes.Ldind_U4;

        internal sealed override System.Reflection.Emit.OpCode ConversionOpCode => OpCodes.Conv_U8;
    }
}