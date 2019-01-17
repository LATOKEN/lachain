﻿using System.Reflection.Emit;

namespace Phorkus.WebAssembly.Instructions
{
	/// <summary>
	/// Grow linear memory by a given unsigned delta of 65536-byte pages. Return the previous memory size in units of pages or -1 on failure.
	/// </summary>
	public class GrowMemory : Instruction
	{
		/// <summary>
		/// Always <see cref="GrowMemory"/>.
		/// </summary>
		public sealed override OpCode OpCode => OpCode.GrowMemory;

		/// <summary>
		/// Not currently used.
		/// </summary>
		public byte Reserved { get; set; }

		/// <summary>
		/// Creates a new  <see cref="GrowMemory"/> instance.
		/// </summary>
		public GrowMemory()
		{
		}

		internal GrowMemory(Reader reader)
		{
			Reserved = reader.ReadVarUInt1();
		}

		internal sealed override void WriteTo(Writer writer)
		{
			writer.Write((byte)OpCode.GrowMemory);
			writer.Write(this.Reserved);
		}

		/// <summary>
		/// Determines whether this instruction is identical to another.
		/// </summary>
		/// <param name="other">The instruction to compare against.</param>
		/// <returns>True if they have the same type and value, otherwise false.</returns>
		public override bool Equals(Instruction other) =>
			other is GrowMemory instruction
			&& instruction.Reserved == this.Reserved
			;

		/// <summary>
		/// Returns a simple hash code based on the value of the instruction.
		/// </summary>
		/// <returns>The hash code.</returns>
		public override int GetHashCode() => HashCode.Combine((int)this.OpCode, this.Reserved);

		internal sealed override void Compile(CompilationContext context)
		{
			var stack = context.Stack;
			if (stack.Count < 1)
				throw new StackTooSmallException(OpCode.GrowMemory, 1, stack.Count);

			var type = stack.Peek(); //Assuming validation passes, the remaining type will be this.
			if (type != ValueType.Int32)
				throw new StackTypeInvalidException(OpCode.GrowMemory, ValueType.Int32, type);

			context.EmitLoadThis();
			context.Emit(OpCodes.Ldfld, context.Memory);
			context.Emit(OpCodes.Call, context[HelperMethod.GrowMemory, (helper, c) =>
			{
				var builder = c.ExportsBuilder.DefineMethod(
					"☣ GrowMemory",
					CompilationContext.HelperMethodAttributes,
					typeof(uint),
					new[]
					{
						typeof(uint), //Delta
						typeof(Runtime.UnmanagedMemory),
					}
					);

				var il = builder.GetILGenerator();
				il.Emit(OpCodes.Ldarg_1);
				il.Emit(OpCodes.Ldarg_0);
				il.Emit(OpCodes.Call, Runtime.UnmanagedMemory.GrowMethod);
				il.Emit(OpCodes.Ret);

				return builder;
			}
			]);
		}
	}
}