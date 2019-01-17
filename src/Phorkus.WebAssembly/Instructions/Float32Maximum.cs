using System;
using System.Linq;
using System.Reflection;

namespace Phorkus.WebAssembly.Instructions
{
	/// <summary>
	/// Maximum (binary operator); if either operand is NaN, returns NaN.
	/// </summary>
	public class Float32Maximum : ValueTwoToOneCallInstruction
	{
		/// <summary>
		/// Always <see cref="Float32Maximum"/>.
		/// </summary>
		public sealed override OpCode OpCode => OpCode.Float32Maximum;

		/// <summary>
		/// Creates a new  <see cref="Float32Maximum"/> instance.
		/// </summary>
		public Float32Maximum()
		{
		}

		internal sealed override MethodInfo MethodInfo => method;

		internal sealed override ValueType ValueType => ValueType.Float32;

		private static readonly RegeneratingWeakReference<MethodInfo> method = new RegeneratingWeakReference<MethodInfo>(() =>
			typeof(Math).GetTypeInfo().DeclaredMethods.First(m =>
			{
				if (m.Name != nameof(Math.Max))
					return false;

				var parms = m.GetParameters();
				return
					parms.Length == 2 &&
					parms[0].ParameterType == typeof(float) &&
					parms[1].ParameterType == typeof(float)
					;
			}));
	}
}