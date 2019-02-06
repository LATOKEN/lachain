using System;
using System.Linq;
using System.Reflection;

namespace Phorkus.WebAssembly.Instructions
{
    /// <summary>
    /// Ceiling operator.
    /// </summary>
    public class Float64Ceiling : ValueOneToOneCallInstruction
    {
        /// <summary>
        /// Always <see cref="OpCode.Float64Ceiling"/>.
        /// </summary>
        public sealed override OpCode OpCode => OpCode.Float64Ceiling;

        /// <summary>
        /// Creates a new  <see cref="Float64Ceiling"/> instance.
        /// </summary>
        public Float64Ceiling()
        {
        }

        internal sealed override MethodInfo MethodInfo => Method;

        internal sealed override ValueType ValueType => ValueType.Float64;

        internal static readonly RegeneratingWeakReference<MethodInfo> Method = new RegeneratingWeakReference<MethodInfo>(() =>
            typeof(Math).GetTypeInfo().DeclaredMethods.First(m =>
            {
                if (m.Name != nameof(Math.Ceiling))
                    return false;

                var parms = m.GetParameters();
                return parms.Length == 1 && parms[0].ParameterType == typeof(double);
            }));
    }
}