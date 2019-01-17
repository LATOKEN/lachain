﻿using System;
using System.Diagnostics;

namespace Phorkus.WebAssembly
{
	/// <summary>
	/// Desribes a table within the assembly.
	/// </summary>
	public class Table
	{
		/// <summary>
		/// The type of elements.
		/// </summary>
		public ElementType ElementType { get; set; }

		[DebuggerBrowsable(DebuggerBrowsableState.Never)] //Wrapped by a property
		private ResizableLimits resizableLimits;

		/// <summary>
		/// A packed tuple that describes the limits of the table.
		/// </summary>
		public ResizableLimits ResizableLimits
		{
			get => resizableLimits ?? (resizableLimits = new ResizableLimits());
			set => resizableLimits = value ?? throw new ArgumentNullException(nameof(value));
		}

		/// <summary>
		/// Creates a new <see cref="Table"/> instance.
		/// </summary>
		public Table()
		{
		}

		/// <summary>
		/// Creates a new <see cref="Table"/> from a binary data stream.
		/// </summary>
		/// <param name="reader">The source of data.</param>
		/// <exception cref="ArgumentNullException"><paramref name="reader"/> cannot be null.</exception>
		internal Table(Reader reader)
		{
			if (reader == null)
				throw new ArgumentNullException(nameof(reader));

			this.ElementType = (ElementType)reader.ReadVarInt7();
			this.resizableLimits = new ResizableLimits(reader);
		}

		/// <summary>
		/// Expresses the value of this instance as a string.
		/// </summary>
		/// <returns>A string representation of this instance.</returns>
		public override string ToString() => $"Table {ElementType}, {ResizableLimits}";

		internal void WriteTo(Writer writer)
		{
			writer.WriteVar((sbyte)this.ElementType);
			this.ResizableLimits.WriteTo(writer);
		}
	}
}