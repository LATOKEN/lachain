﻿using System.Reflection.Emit;

namespace Phorkus.WebAssembly.Instructions
{
	/// <summary>
	/// Copysign.
	/// </summary>
	public class Float64CopySign : SimpleInstruction
	{
		/// <summary>
		/// Always <see cref="Float64CopySign"/>.
		/// </summary>
		public sealed override OpCode OpCode => OpCode.Float64CopySign;

		/// <summary>
		/// Creates a new  <see cref="Float64CopySign"/> instance.
		/// </summary>
		public Float64CopySign()
		{
		}

		internal sealed override void Compile(CompilationContext context)
		{
			var stack = context.Stack;
			if (stack.Count < 1)
				throw new StackTooSmallException(OpCode.Float64CopySign, 1, stack.Count);

			var type = stack.Pop();
			if (type != ValueType.Float64)
				throw new StackTypeInvalidException(OpCode.Float64CopySign, ValueType.Float64, type);

			type = stack.Peek(); //Assuming validation passes, the remaining type will be this.
			if (type != ValueType.Float64)
				throw new StackTypeInvalidException(OpCode.Float64CopySign, ValueType.Float64, type);

			context.Emit(OpCodes.Call, context[HelperMethod.Float64CopySign, (helper, c) =>
			{
				var builder = c.ExportsBuilder.DefineMethod(
					"☣ Float64CopySign",
					CompilationContext.HelperMethodAttributes,
					typeof(double),
					new[]
					{
						typeof(double),
						typeof(double),
					}
					);

				var il = builder.GetILGenerator();
				var value = il.DeclareLocal(typeof(ulong));

				il.Emit(OpCodes.Ldarga_S, 0);
				il.Emit(OpCodes.Ldind_I8);
				il.Emit(OpCodes.Ldc_I8, 0x7fffffffffffffff);
				il.Emit(OpCodes.And);
				il.Emit(OpCodes.Ldarga_S, 1);
				il.Emit(OpCodes.Ldind_I8);
				il.Emit(OpCodes.Ldc_I8, unchecked((long)0x8000000000000000u));
				il.Emit(OpCodes.And);
				il.Emit(OpCodes.Or);
				il.Emit(OpCodes.Stloc_0);
				il.Emit(OpCodes.Ldloca_S, value);
				il.Emit(OpCodes.Ldind_R8);
				il.Emit(OpCodes.Ret);

				return builder;
			}
			]);
		}
	}
}