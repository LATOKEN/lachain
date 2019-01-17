﻿using System.Reflection.Emit;

namespace Phorkus.WebAssembly.Instructions
{
	/// <summary>
	/// Sign-agnostic rotate left.
	/// </summary>
	public class Int64RotateLeft : SimpleInstruction
	{
		/// <summary>
		/// Always <see cref="Int64RotateLeft"/>.
		/// </summary>
		public sealed override OpCode OpCode => OpCode.Int64RotateLeft;

		/// <summary>
		/// Creates a new  <see cref="Int64RotateLeft"/> instance.
		/// </summary>
		public Int64RotateLeft()
		{
		}

		internal sealed override void Compile(CompilationContext context)
		{
			var stack = context.Stack;
			if (stack.Count < 2)
				throw new StackTooSmallException(OpCode.Int64RotateLeft, 2, stack.Count);

			var typeB = stack.Pop();
			var typeA = stack.Peek(); //Assuming validation passes, the remaining type will be this.

			if (typeA != ValueType.Int64)
				throw new StackTypeInvalidException(OpCode.Int64RotateLeft, ValueType.Int64, typeA);

			if (typeA != typeB)
				throw new StackParameterMismatchException(OpCode.Int64RotateLeft, typeA, typeB);

			context.Emit(OpCodes.Call, context[HelperMethod.Int64RotateLeft, (helper, c) =>
			{
				var builder = c.ExportsBuilder.DefineMethod(
					"☣ Int64RotateLeft",
					CompilationContext.HelperMethodAttributes,
					typeof(ulong),
					new[]
					{
							typeof(ulong),
							typeof(long),
					}
					);

				var il = builder.GetILGenerator();
				il.Emit(OpCodes.Ldarg_0);
				il.Emit(OpCodes.Ldarg_1);
				il.Emit(OpCodes.Conv_I4);
				il.Emit(OpCodes.Ldc_I4_S, 63);
				il.Emit(OpCodes.And);
				il.Emit(OpCodes.Shl);

				il.Emit(OpCodes.Ldarg_0);
				il.Emit(OpCodes.Ldc_I4_S, 64);
				il.Emit(OpCodes.Ldarg_1);
				il.Emit(OpCodes.Conv_I4);
				il.Emit(OpCodes.Sub);
				il.Emit(OpCodes.Ldc_I4_S, 63);
				il.Emit(OpCodes.And);
				il.Emit(OpCodes.Shr_Un);
				il.Emit(OpCodes.Or);

				il.Emit(OpCodes.Ret);
				return builder;
			}
			]);
		}
	}
}