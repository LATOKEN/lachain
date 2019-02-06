namespace Phorkus.WebAssembly.Instructions
{
    /// <summary>
    /// Signed greater than or equal.
    /// </summary>
    public class Int32GreaterThanOrEqualSigned : ValueTwoToInt32NotEqualZeroInstruction
    {
        /// <summary>
        /// Always <see cref="OpCode.Int32GreaterThanOrEqualSigned"/>.
        /// </summary>
        public sealed override OpCode OpCode => OpCode.Int32GreaterThanOrEqualSigned;

        internal sealed override ValueType ValueType => ValueType.Int32;

        internal sealed override System.Reflection.Emit.OpCode EmittedOpCode =>
            System.Reflection.Emit.OpCodes.Clt; //The result is compared for equality to zero, reversing it.

        /// <summary>
        /// Creates a new  <see cref="Int32GreaterThanOrEqualSigned"/> instance.
        /// </summary>
        public Int32GreaterThanOrEqualSigned()
        {
        }
    }
}