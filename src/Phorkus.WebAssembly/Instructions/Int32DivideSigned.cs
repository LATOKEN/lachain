namespace Phorkus.WebAssembly.Instructions
{
    /// <summary>
    /// Signed division (result is truncated toward zero).
    /// </summary>
    public class Int32DivideSigned : ValueTwoToOneInstruction
    {
        /// <summary>
        /// Always <see cref="OpCode.Int32DivideSigned"/>.
        /// </summary>
        public sealed override OpCode OpCode => OpCode.Int32DivideSigned;

        internal sealed override ValueType ValueType => ValueType.Int32;

        internal sealed override System.Reflection.Emit.OpCode EmittedOpCode =>
            System.Reflection.Emit.OpCodes.Div;

        /// <summary>
        /// Creates a new  <see cref="Int32DivideSigned"/> instance.
        /// </summary>
        public Int32DivideSigned()
        {
        }
    }
}