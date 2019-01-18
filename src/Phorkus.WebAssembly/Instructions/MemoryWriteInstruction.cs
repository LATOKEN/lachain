﻿using System.Reflection.Emit;

namespace Phorkus.WebAssembly.Instructions
{
	/// <summary>
	/// Provides shared functionality for instructions that write to linear memory.
	/// </summary>
	public abstract class MemoryWriteInstruction : MemoryImmediateInstruction
	{
		internal MemoryWriteInstruction()
			: base()
		{
		}

		internal MemoryWriteInstruction(Reader reader)
			: base(reader)
		{
		}

		internal abstract HelperMethod StoreHelper { get; }

		internal sealed override void Compile(CompilationContext context)
		{
			var stack = context.Stack;
			if (stack.Count < 2)
				throw new StackTooSmallException(this.OpCode, 2, stack.Count);

			var type = stack.Pop();
			if (type != this.Type)
				throw new StackTypeInvalidException(this.OpCode, this.Type, type);

			type = stack.Pop();
			if (type != ValueType.Int32)
				throw new StackTypeInvalidException(this.OpCode, ValueType.Int32, type);

			Int32Constant.Emit(context, (int)this.Offset);
			context.EmitLoadThis();
			context.Emit(OpCodes.Call, context[this.StoreHelper, this.CreateStoreMethod]);
		}

		private MethodBuilder CreateStoreMethod(HelperMethod helper, CompilationContext context)
		{
			var builder = context.ExportsBuilder.DefineMethod(
				$"☣ {helper}",
				CompilationContext.HelperMethodAttributes,
				typeof(void),
				new[]
				{
					typeof(uint), //Address
					this.Type.ToSystemType(), //Value
					typeof(uint), //Offset
					context.ExportsBuilder, 
				}
				);
			var il = builder.GetILGenerator();

			il.Emit(OpCodes.Ldarg_0);
			il.Emit(OpCodes.Ldarg_2);
			il.Emit(OpCodes.Add_Ovf_Un);
			il.Emit(OpCodes.Ldarg_3);
			il.Emit(OpCodes.Call, context[this.RangeCheckHelper, CreateRangeCheck]);
			il.Emit(OpCodes.Ldarg_3);
			il.Emit(OpCodes.Ldfld, context.Memory);
			il.Emit(OpCodes.Call, Runtime.UnmanagedMemory.StartGetter);
			il.Emit(OpCodes.Add);
			il.Emit(OpCodes.Ldarg_1);
			il.Emit(this.EmittedOpCode);
			il.Emit(OpCodes.Ret);

			return builder;
		}
	}
}