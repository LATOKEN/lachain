using System.Reflection.Emit;

namespace Phorkus.WebAssembly.Instructions
{
    /// <summary>
    /// Compare equal to zero (return 1 if operand is zero, 0 otherwise).
    /// </summary>
    public class Int32EqualZero : SimpleInstruction
    {
        /// <summary>
        /// Always <see cref="OpCode.Int32EqualZero"/>.
        /// </summary>
        public sealed override OpCode OpCode => OpCode.Int32EqualZero;

        /// <summary>
        /// Creates a new  <see cref="Int32EqualZero"/> instance.
        /// </summary>
        public Int32EqualZero()
        {
        }

        internal sealed override void Compile(CompilationContext context)
        {
            var stack = context.Stack;
            if (stack.Count < 1)
                throw new StackTooSmallException(this.OpCode, 1, stack.Count);

            var type = stack.Peek(); //Assuming validation passes, the remaining type will be this.

            if (type != ValueType.Int32)
                throw new StackTypeInvalidException(this.OpCode, ValueType.Int32, type);

            context.Emit(OpCodes.Ldc_I4_0);
            context.Emit(OpCodes.Ceq);
        }
    }
}