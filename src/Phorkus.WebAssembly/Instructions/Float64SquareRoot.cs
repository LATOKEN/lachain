using System;
using System.Linq;
using System.Reflection;

namespace Phorkus.WebAssembly.Instructions
{
	/// <summary>
	/// Square root.
	/// </summary>
	public class Float64SquareRoot : ValueOneToOneCallInstruction
	{
		/// <summary>
		/// Always <see cref="Float64SquareRoot"/>.
		/// </summary>
		public sealed override OpCode OpCode => OpCode.Float64SquareRoot;

		/// <summary>
		/// Creates a new  <see cref="Float64SquareRoot"/> instance.
		/// </summary>
		public Float64SquareRoot()
		{
		}

		internal sealed override MethodInfo MethodInfo => Method;

		internal sealed override ValueType ValueType => ValueType.Float64;

		internal static readonly RegeneratingWeakReference<MethodInfo> Method = new RegeneratingWeakReference<MethodInfo>(() =>
			typeof(Math).GetTypeInfo().DeclaredMethods.First(m =>
			{
				if (m.Name != nameof(Math.Sqrt))
					return false;

				var parms = m.GetParameters();
				return parms.Length == 1 && parms[0].ParameterType == typeof(double);
			}));
	}
}