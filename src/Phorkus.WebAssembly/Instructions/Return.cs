using System.Diagnostics;
using System.Reflection.Emit;

namespace Phorkus.WebAssembly.Instructions
{
	/// <summary>
	/// Return zero or more values from this function.
	/// </summary>
	public class Return : SimpleInstruction
	{
		/// <summary>
		/// Always <see cref="Return"/>.
		/// </summary>
		public sealed override OpCode OpCode => OpCode.Return;

		/// <summary>
		/// Creates a new  <see cref="Return"/> instance.
		/// </summary>
		public Return()
		{
		}

		internal sealed override void Compile(CompilationContext context)
		{
			Debug.Assert(context != null);

			var returns = context.Signature.RawReturnTypes;
			var stack = context.Stack;
			Debug.Assert(stack != null);

			var returnsLength = returns.Length;
			Debug.Assert(returnsLength == 0 || returnsLength == 1); //WebAssembly doesn't currently offer multiple returns, which should be blocked earlier.

			var stackCount = stack.Count;

			if (stackCount < returnsLength)
				throw new StackTooSmallException(OpCode.Return, returnsLength, 0);

			if (stackCount > returnsLength)
			{
				if (returnsLength == 0)
				{
					for (var i = 0; i < stackCount - returnsLength; i++)
						context.Emit(OpCodes.Pop);
				}
				else
				{
					var value = context.DeclareLocal(returns[0].ToSystemType());
					context.Emit(OpCodes.Stloc, value.LocalIndex);

					for (var i = 0; i < stackCount - returnsLength; i++)
						context.Emit(OpCodes.Pop);

					context.Emit(OpCodes.Ldloc, value.LocalIndex);
				}
			}
			else if (returnsLength == 1)
			{
				var type = stack.Pop();
				if (type != returns[0])
					throw new StackTypeInvalidException(OpCode.Return, returns[0], type);
			}

			context.Emit(OpCodes.Ret);
		}
	}
}