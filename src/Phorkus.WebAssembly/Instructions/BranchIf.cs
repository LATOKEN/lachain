using System;
using System.Reflection.Emit;

namespace Phorkus.WebAssembly.Instructions
{
    /// <summary>
    /// Conditionally branch to a given label in an enclosing construct.
    /// </summary>
    public class BranchIf : Instruction
    {
        /// <summary>
        /// Always <see cref="OpCode.BranchIf"/>.
        /// </summary>
        public sealed override OpCode OpCode => OpCode.BranchIf;

        /// <summary>
        /// The number of ancestor blocks to climb; 0 is the immediate parent.
        /// </summary>
        public uint Index { get; set; }

        /// <summary>
        /// Creates a new  <see cref="BranchIf"/> instance.
        /// </summary>
        public BranchIf()
        {
        }

        /// <summary>
        /// Creates a new <see cref="BranchIf"/> instance with the provided index.
        /// </summary>
        /// <param name="index">The number of ancestor blocks to climb; 0 is the immediate parent.</param>
        public BranchIf(uint index)
        {
            this.Index = index;
        }

        internal BranchIf(Reader reader)
        {
            if (reader == null)
                throw new ArgumentNullException(nameof(reader));

            Index = reader.ReadVarUInt32();
        }

        internal sealed override void WriteTo(Writer writer)
        {
            writer.Write((byte)OpCode.BranchIf);
            writer.WriteVar(this.Index);
        }

        /// <summary>
        /// Determines whether this instruction is identical to another.
        /// </summary>
        /// <param name="other">The instruction to compare against.</param>
        /// <returns>True if they have the same type and value, otherwise false.</returns>
        public override bool Equals(Instruction other) =>
            other is BranchIf instruction
            && instruction.Index == this.Index
            ;

        /// <summary>
        /// Returns a simple hash code based on the value of the instruction.
        /// </summary>
        /// <returns>The hash code.</returns>
        public override int GetHashCode() => HashCode.Combine((int)this.OpCode, (int)this.Index);

        internal sealed override void Compile(CompilationContext context)
        {
            var stack = context.Stack;
            if (stack.Count == 0)
                throw new StackTooSmallException(OpCode.If, 1, 0);

            var type = stack.Pop();
            if (type != ValueType.Int32)
                throw new StackTypeInvalidException(OpCode.If, ValueType.Int32, type);

            context.Emit(OpCodes.Brtrue, context.Labels[checked((uint)context.Depth.Count) - this.Index - 1]);
        }
    }
}