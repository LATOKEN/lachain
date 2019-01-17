﻿namespace Phorkus.WebAssembly
{
	/// <summary>
	/// Used by the compiler to indicate that the value types being fed as parameters to an operation that requires them to match, do not match.
	/// </summary>
	class StackParameterMismatchException : OpCodeCompilationException
	{
		/// <summary>
		/// Creates a new <see cref="StackParameterMismatchException"/> with the provided parameters.
		/// </summary>
		/// <param name="opCode">The operation attempted.</param>
		/// <param name="first">The first parameter type.</param>
		/// <param name="second">The second parameter type.</param>
		public StackParameterMismatchException(OpCode opCode, ValueType first, ValueType second)
			: base(opCode, $"requires the type parameters to match, found {first} and {second}.")
		{
			this.First = first;
			this.Second = second;
		}

		/// <summary>
		/// The first parameter type.
		/// </summary>
		public ValueType First { get; }

		/// <summary>
		/// The second parameter type.
		/// </summary>
		public ValueType Second { get; }
	}
}