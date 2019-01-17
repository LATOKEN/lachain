﻿using System.Reflection.Emit;

namespace Phorkus.WebAssembly.Instructions
{
	/// <summary>
	/// Reinterpret the bits of a 64-bit integer as a 64-bit float.
	/// </summary>
	public class Float64ReinterpretInt64 : SimpleInstruction
	{
		/// <summary>
		/// Always <see cref="Float64ReinterpretInt64"/>.
		/// </summary>
		public sealed override OpCode OpCode => OpCode.Float64ReinterpretInt64;

		/// <summary>
		/// Creates a new  <see cref="Float64ReinterpretInt64"/> instance.
		/// </summary>
		public Float64ReinterpretInt64()
		{
		}

		internal sealed override void Compile(CompilationContext context)
		{
			var stack = context.Stack;
			if (stack.Count < 1)
				throw new StackTooSmallException(OpCode.Float64ReinterpretInt64, 1, stack.Count);

			var type = stack.Pop();
			if (type != ValueType.Int64)
				throw new StackTypeInvalidException(OpCode.Float64ReinterpretInt64, ValueType.Int64, type);

			stack.Push(ValueType.Float64);

			context.Emit(OpCodes.Call, context[HelperMethod.Float64ReinterpretInt64, (helper, c) =>
			{
				var builder = c.ExportsBuilder.DefineMethod(
					"☣ Float64ReinterpretInt64",
					CompilationContext.HelperMethodAttributes,
					typeof(double),
					new[]
					{
							typeof(long),
					}
					);

				var il = builder.GetILGenerator();
				il.Emit(OpCodes.Ldarga_S, 0);
				il.Emit(OpCodes.Ldind_R8);
				il.Emit(OpCodes.Ret);
				return builder;
			}
			]);
		}
	}
}