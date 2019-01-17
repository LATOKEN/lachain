using System;
using System.Linq;
using System.Reflection;

namespace Phorkus.WebAssembly.Instructions
{
	/// <summary>
	/// Absolute value.
	/// </summary>
	public class Float64Absolute : ValueOneToOneCallInstruction
	{
		/// <summary>
		/// Always <see cref="Float64Absolute"/>.
		/// </summary>
		public sealed override OpCode OpCode => OpCode.Float64Absolute;

		/// <summary>
		/// Creates a new  <see cref="Float64Absolute"/> instance.
		/// </summary>
		public Float64Absolute()
		{
		}

		internal sealed override MethodInfo MethodInfo => method;

		internal sealed override ValueType ValueType => ValueType.Float64;

		private static readonly RegeneratingWeakReference<MethodInfo> method = new RegeneratingWeakReference<MethodInfo>(() =>
			typeof(Math).GetTypeInfo().DeclaredMethods.First(m =>
			{
				if (m.Name != nameof(Math.Abs))
					return false;

				var parms = m.GetParameters();
				return parms.Length == 1 && parms[0].ParameterType == typeof(double);
			}));
	}
}