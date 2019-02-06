namespace Phorkus.WebAssembly.Instructions
{
    /// <summary>
    /// Multiplication.
    /// </summary>
    public class Float32Multiply : ValueTwoToOneInstruction
    {
        /// <summary>
        /// Always <see cref="OpCode.Float32Multiply"/>.
        /// </summary>
        public sealed override OpCode OpCode => OpCode.Float32Multiply;

        internal sealed override ValueType ValueType => ValueType.Float32;

        internal sealed override System.Reflection.Emit.OpCode EmittedOpCode =>
            System.Reflection.Emit.OpCodes.Mul;

        /// <summary>
        /// Creates a new  <see cref="Float32Multiply"/> instance.
        /// </summary>
        public Float32Multiply()
        {
        }
    }
}