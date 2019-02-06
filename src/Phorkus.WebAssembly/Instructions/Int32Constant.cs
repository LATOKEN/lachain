﻿using System.Reflection.Emit;

namespace Phorkus.WebAssembly.Instructions
{
    /// <summary>
    /// Produce the value of an i32 immediate.
    /// </summary>
    public class Int32Constant : Instruction
    {
        /// <summary>
        /// Always <see cref="OpCode.Int32Constant"/>.
        /// </summary>
        public sealed override OpCode OpCode => OpCode.Int32Constant;

        /// <summary>
        /// Gets or sets the value of the constant.
        /// </summary>
        public int Value { get; set; }

        /// <summary>
        /// Creates a new <see cref="Int32Constant"/> instance.
        /// </summary>
        public Int32Constant()
        {
        }

        /// <summary>
        /// Creates a new <see cref="Int32Constant"/> instance with the provided value.
        /// </summary>
        /// <param name="value">The value of the constant.  This is passed to the <see cref="Value"/> property.</param>
        public Int32Constant(int value) => Value = value;

        /// <summary>
        /// Creates a new <see cref="Int32Constant"/> instance with the provided value.
        /// </summary>
        /// <param name="value">The value of the constant.  This is passed to the <see cref="Value"/> property with an unchecked cast.</param>
        public Int32Constant(uint value) => Value = unchecked((int)value);

        /// <summary>
        /// Creates a new <see cref="Int32Constant"/> instance from binary data.
        /// </summary>
        /// <param name="reader">The source of binary data.</param>
        internal Int32Constant(Reader reader)
        {
            Value = reader.ReadVarInt32();
        }

        internal override void WriteTo(Writer writer)
        {
            writer.Write((byte)OpCode.Int32Constant);
            writer.WriteVar(this.Value);
        }

        /// <summary>
        /// Determines whether this instruction is identical to another.
        /// </summary>
        /// <param name="other">The instruction to compare against.</param>
        /// <returns>True if they have the same type and value, otherwise false.</returns>
        public override bool Equals(Instruction other) =>
            other is Int32Constant instruction
            && instruction.Value == this.Value
            ;

        /// <summary>
        /// Returns a simple hash code based on the value of the instruction.
        /// </summary>
        /// <returns>The hash code.</returns>
        public override int GetHashCode() => HashCode.Combine((int)this.OpCode, this.Value);

        internal sealed override void Compile(CompilationContext context)
        {
            context.Stack.Push(ValueType.Int32);
            Emit(context, this.Value);
        }

        internal static void Emit(CompilationContext context, int value)
        {
            switch (value)
            {
                default:
                    context.Emit(OpCodes.Ldc_I4, value);
                    break;

                case -1: context.Emit(OpCodes.Ldc_I4_M1); break;
                case 0: context.Emit(OpCodes.Ldc_I4_0); break;
                case 1: context.Emit(OpCodes.Ldc_I4_1); break;
                case 2: context.Emit(OpCodes.Ldc_I4_2); break;
                case 3: context.Emit(OpCodes.Ldc_I4_3); break;
                case 4: context.Emit(OpCodes.Ldc_I4_4); break;
                case 5: context.Emit(OpCodes.Ldc_I4_5); break;
                case 6: context.Emit(OpCodes.Ldc_I4_6); break;
                case 7: context.Emit(OpCodes.Ldc_I4_7); break;
                case 8: context.Emit(OpCodes.Ldc_I4_8); break;
            }
        }

        internal static void Emit(ILGenerator il, int value)
        {
            switch (value)
            {
                default:
                    il.Emit(OpCodes.Ldc_I4, value);
                    break;

                case -1: il.Emit(OpCodes.Ldc_I4_M1); break;
                case 0: il.Emit(OpCodes.Ldc_I4_0); break;
                case 1: il.Emit(OpCodes.Ldc_I4_1); break;
                case 2: il.Emit(OpCodes.Ldc_I4_2); break;
                case 3: il.Emit(OpCodes.Ldc_I4_3); break;
                case 4: il.Emit(OpCodes.Ldc_I4_4); break;
                case 5: il.Emit(OpCodes.Ldc_I4_5); break;
                case 6: il.Emit(OpCodes.Ldc_I4_6); break;
                case 7: il.Emit(OpCodes.Ldc_I4_7); break;
                case 8: il.Emit(OpCodes.Ldc_I4_8); break;
            }
        }
    }
}