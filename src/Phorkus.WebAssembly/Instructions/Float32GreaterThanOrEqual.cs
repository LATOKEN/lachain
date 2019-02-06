namespace Phorkus.WebAssembly.Instructions
{
    /// <summary>
    /// Compare ordered and greater than or equal.
    /// </summary>
    public class Float32GreaterThanOrEqual : ValueTwoToInt32NotEqualZeroInstruction
    {
        /// <summary>
        /// Always <see cref="OpCode.Float32GreaterThanOrEqual"/>.
        /// </summary>
        public sealed override OpCode OpCode => OpCode.Float32GreaterThanOrEqual;

        internal sealed override ValueType ValueType => ValueType.Float32;

        internal sealed override System.Reflection.Emit.OpCode EmittedOpCode =>
            System.Reflection.Emit.OpCodes.Clt_Un; //The result is compared for equality to zero, reversing it.

        /// <summary>
        /// Creates a new  <see cref="Float32GreaterThanOrEqual"/> instance.
        /// </summary>
        public Float32GreaterThanOrEqual()
        {
        }
    }
}