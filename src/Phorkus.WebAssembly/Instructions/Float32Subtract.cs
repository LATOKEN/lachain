namespace Phorkus.WebAssembly.Instructions
{
    /// <summary>
    /// Subtraction.
    /// </summary>
    public class Float32Subtract : ValueTwoToOneInstruction
    {
        /// <summary>
        /// Always <see cref="OpCode.Float32Subtract"/>.
        /// </summary>
        public sealed override OpCode OpCode => OpCode.Float32Subtract;

        internal sealed override ValueType ValueType => ValueType.Float32;

        internal sealed override System.Reflection.Emit.OpCode EmittedOpCode =>
            System.Reflection.Emit.OpCodes.Sub;

        /// <summary>
        /// Creates a new  <see cref="Float32Subtract"/> instance.
        /// </summary>
        public Float32Subtract()
        {
        }
    }
}