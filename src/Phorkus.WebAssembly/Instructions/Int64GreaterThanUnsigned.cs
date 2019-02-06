namespace Phorkus.WebAssembly.Instructions
{
    /// <summary>
    /// Unsigned greater than.
    /// </summary>
    public class Int64GreaterThanUnsigned : ValueTwoToInt32Instruction
    {
        /// <summary>
        /// Always <see cref="OpCode.Int64GreaterThanUnsigned"/>.
        /// </summary>
        public sealed override OpCode OpCode => OpCode.Int64GreaterThanUnsigned;

        internal sealed override ValueType ValueType => ValueType.Int64;

        internal sealed override System.Reflection.Emit.OpCode EmittedOpCode =>
            System.Reflection.Emit.OpCodes.Cgt_Un;

        /// <summary>
        /// Creates a new  <see cref="Int64GreaterThanUnsigned"/> instance.
        /// </summary>
        public Int64GreaterThanUnsigned()
        {
        }
    }
}