using System.Reflection;

namespace Phorkus.WebAssembly.Instructions
{
    /// <summary>
    /// Ceiling operator.
    /// </summary>
    public class Float32Ceiling : Float64CallWrapperInstruction
    {
        /// <summary>
        /// Always <see cref="OpCode.Float32Ceiling"/>.
        /// </summary>
        public sealed override OpCode OpCode => OpCode.Float32Ceiling;

        /// <summary>
        /// Creates a new  <see cref="Float32Ceiling"/> instance.
        /// </summary>
        public Float32Ceiling()
        {
        }

        internal sealed override MethodInfo MethodInfo => Float64Ceiling.Method;
    }
}