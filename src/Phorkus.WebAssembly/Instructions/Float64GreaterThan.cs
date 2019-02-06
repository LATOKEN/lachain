namespace Phorkus.WebAssembly.Instructions
{
    /// <summary>
    /// Compare ordered and greater than.
    /// </summary>
    public class Float64GreaterThan : ValueTwoToInt32Instruction
    {
        /// <summary>
        /// Always <see cref="OpCode.Float64GreaterThan"/>.
        /// </summary>
        public sealed override OpCode OpCode => OpCode.Float64GreaterThan;

        internal sealed override ValueType ValueType => ValueType.Float64;

        internal sealed override System.Reflection.Emit.OpCode EmittedOpCode =>
            System.Reflection.Emit.OpCodes.Cgt;


        /// <summary>
        /// Creates a new  <see cref="Float64GreaterThan"/> instance.
        /// </summary>
        public Float64GreaterThan()
        {
        }
    }
}