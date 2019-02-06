namespace Phorkus.WebAssembly.Instructions
{
    /// <summary>
    /// Unsigned less than.
    /// </summary>
    public class Int32LessThanUnsigned : ValueTwoToOneInstruction
    {
        /// <summary>
        /// Always <see cref="OpCode.Int32LessThanUnsigned"/>.
        /// </summary>
        public sealed override OpCode OpCode => OpCode.Int32LessThanUnsigned;

        internal sealed override ValueType ValueType => ValueType.Int32;

        internal sealed override System.Reflection.Emit.OpCode EmittedOpCode =>
            System.Reflection.Emit.OpCodes.Clt_Un;

        /// <summary>
        /// Creates a new  <see cref="Int32LessThanUnsigned"/> instance.
        /// </summary>
        public Int32LessThanUnsigned()
        {
        }
    }
}