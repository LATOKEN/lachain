using System;
using System.Linq;
using System.Reflection;

namespace Phorkus.WebAssembly.Instructions
{
    /// <summary>
    /// Round to nearest integer towards zero.
    /// </summary>
    public class Float64Truncate : ValueOneToOneCallInstruction
    {
        /// <summary>
        /// Always <see cref="OpCode.Float64Truncate"/>.
        /// </summary>
        public sealed override OpCode OpCode => OpCode.Float64Truncate;

        /// <summary>
        /// Creates a new  <see cref="Float64Truncate"/> instance.
        /// </summary>
        public Float64Truncate()
        {
        }

        internal sealed override MethodInfo MethodInfo => Method;

        internal sealed override ValueType ValueType => ValueType.Float64;

        internal static readonly RegeneratingWeakReference<MethodInfo> Method = new RegeneratingWeakReference<MethodInfo>(() =>
            typeof(Math).GetTypeInfo().DeclaredMethods.First(m =>
            {
                if (m.Name != nameof(Math.Truncate))
                    return false;

                var parms = m.GetParameters();
                return parms.Length == 1 && parms[0].ParameterType == typeof(double);
            }));
    }
}