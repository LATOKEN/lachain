namespace Phorkus.WebAssembly
{
    /// <summary>
    /// Binary opcode values.
    /// </summary>
    public enum OpCode : byte
    {
        /// <summary>
        /// An instruction which always traps.
        /// </summary>
        /// <remarks>It is intended to be used for example after calls to functions which are known by the producer not to return.</remarks>
        [OpCodeCharacteristics("unreachable", IsFlowControl = true)]
        Unreachable = 0x00,

        /// <summary>
        /// No operation, no effect.
        /// </summary>
        [OpCodeCharacteristics("nop")]
        NoOperation = 0x01,

        /// <summary>
        /// The beginning of a block construct, a sequence of instructions with a label at the end.
        /// </summary>
        [OpCodeCharacteristics("block", IsFlowControl = true)]
        Block = 0x02,

        /// <summary>
        /// A block with a label at the beginning which may be used to form loops.
        /// </summary>
        [OpCodeCharacteristics("loop", IsFlowControl = true)]
        Loop = 0x03,

        /// <summary>
        /// The beginning of an if construct with an implicit "then" block.
        /// </summary>
        [OpCodeCharacteristics("if", IsFlowControl = true)]
        If = 0x04,

        /// <summary>
        /// Marks the else block of an <see cref="If"/>.
        /// </summary>
        [OpCodeCharacteristics("else", 90, IsFlowControl = true)]
        Else = 0x05,

        /// <summary>
        /// An instruction that marks the end of a block, loop, if, or function.
        /// </summary>
        [OpCodeCharacteristics("end", IsFlowControl = true)]
        End = 0x0b,

        /// <summary>
        /// Branch to a given label in an enclosing construct.
        /// </summary>
        [OpCodeCharacteristics("br", 90, IsFlowControl = true)]
        Branch = 0x0c,

        /// <summary>
        /// Conditionally branch to a given label in an enclosing construct.
        /// </summary>
        [OpCodeCharacteristics("br_if", 90, IsFlowControl = true)]
        BranchIf = 0x0d,

        /// <summary>
        /// A jump table which jumps to a label in an enclosing construct.
        /// </summary>
        [OpCodeCharacteristics("br_table", 120, IsFlowControl = true)]
        BranchTable = 0x0e,

        /// <summary>
        /// Return zero or more values from this function.
        /// </summary>
        [OpCodeCharacteristics("return", 90, IsFlowControl = true)]
        Return = 0x0f,

        /// <summary>
        /// Call function directly.
        /// </summary>
        [OpCodeCharacteristics("call", 90, IsFlowControl = true)]
        Call = 0x10,

        /// <summary>
        /// Call function indirectly.
        /// </summary>
        [OpCodeCharacteristics("call_indirect", 10000, IsFlowControl = true)]
        CallIndirect = 0x11,

        /// <summary>
        /// A unary operator that discards the value of its operand.
        /// </summary>
        [OpCodeCharacteristics("drop", 120)]
        Drop = 0x1a,

        /// <summary>
        /// A ternary operator with two operands, which have the same type as each other, plus a boolean (i32) condition. Returns the first operand if the condition operand is non-zero, or the second otherwise.
        /// </summary>
        [OpCodeCharacteristics("select", 120, IsFlowControl = true)]
        Select = 0x1b,

        /// <summary>
        /// Read the current value of a local variable.
        /// </summary>
        [OpCodeCharacteristics("get_local", 120)]
        GetLocal = 0x20,

        /// <summary>
        /// Set the current value of a local variable.
        /// </summary>
        [OpCodeCharacteristics("set_local", 120)]
        SetLocal = 0x21,

        /// <summary>
        /// Like <see cref="SetLocal"/>, but also returns the set value.
        /// </summary>
        [OpCodeCharacteristics("tee_local", 120)]
        TeeLocal = 0x22,

        /// <summary>
        /// (i32 index){T} => {T}; Read a global variable.
        /// </summary>
        [OpCodeCharacteristics("get_global", 120)]
        GetGlobal = 0x23,

        /// <summary>
        /// (i32 index, T value){T} => i32 index; Write a global variable.
        /// </summary>
        [OpCodeCharacteristics("set_global", 120)]
        SetGlobal = 0x24,

        /// <summary>
        /// Load 4 bytes as i32.
        /// </summary>
        [OpCodeCharacteristics("i32.load", 120)]
        Int32Load = 0x28,

        /// <summary>
        /// Load 8 bytes as i64.
        /// </summary>
        [OpCodeCharacteristics("i64.load", 120)]
        Int64Load = 0x29,

        /// <summary>
        /// Load 4 bytes as f32.
        /// </summary>
        [OpCodeCharacteristics("f32.load", 120)]
        Float32Load = 0x2a,

        /// <summary>
        /// Load 8 bytes as f64.
        /// </summary>
        [OpCodeCharacteristics("f64.load", 120)]
        Float64Load = 0x2b,

        /// <summary>
        /// Load 1 byte and sign-extend i8 to i32.
        /// </summary>
        [OpCodeCharacteristics("i32.load8_s", 120)]
        Int32Load8Signed = 0x2c,

        /// <summary>
        /// Load 1 byte and zero-extend i8 to i32.
        /// </summary>
        [OpCodeCharacteristics("i32.load8_u", 120)]
        Int32Load8Unsigned = 0x2d,

        /// <summary>
        /// Load 2 bytes and sign-extend i16 to i32.
        /// </summary>
        [OpCodeCharacteristics("i32.load16_s", 120)]
        Int32Load16Signed = 0x2e,

        /// <summary>
        /// Load 2 bytes and zero-extend i16 to i32.
        /// </summary>
        [OpCodeCharacteristics("i32.load16_u", 120)]
        Int32Load16Unsigned = 0x2f,

        /// <summary>
        /// Load 1 byte and sign-extend i8 to i64.
        /// </summary>
        [OpCodeCharacteristics("i64.load8_s", 120)]
        Int64Load8Signed = 0x30,

        /// <summary>
        /// Load 1 byte and zero-extend i8 to i64.
        /// </summary>
        [OpCodeCharacteristics("i64.load8_u", 120)]
        Int64Load8Unsigned = 0x31,

        /// <summary>
        /// Load 2 bytes and sign-extend i16 to i64.
        /// </summary>
        [OpCodeCharacteristics("i64.load16_s", 120)]
        Int64Load16Signed = 0x32,

        /// <summary>
        /// Load 2 bytes and zero-extend i16 to i64.
        /// </summary>
        [OpCodeCharacteristics("i64.load16_u", 120)]
        Int64Load16Unsigned = 0x33,

        /// <summary>
        /// Load 4 bytes and sign-extend i32 to i64.
        /// </summary>
        [OpCodeCharacteristics("i64.load32_s", 120)]
        Int64Load32Signed = 0x34,

        /// <summary>
        /// Load 4 bytes and zero-extend i32 to i64.
        /// </summary>
        [OpCodeCharacteristics("i64.load32_u", 120)]
        Int64Load32Unsigned = 0x35,

        /// <summary>
        /// (No conversion) store 4 bytes.
        /// </summary>
        [OpCodeCharacteristics("i32.store", 120)]
        Int32Store = 0x36,

        /// <summary>
        /// (No conversion) store 8 bytes.
        /// </summary>
        [OpCodeCharacteristics("i64.store", 120)]
        Int64Store = 0x37,

        /// <summary>
        /// (No conversion) store 4 bytes.
        /// </summary>
        [OpCodeCharacteristics("f32.store", 120)]
        Float32Store = 0x38,

        /// <summary>
        /// (No conversion) store 8 bytes.
        /// </summary>
        [OpCodeCharacteristics("f64.store", 120)]
        Float64Store = 0x39,

        /// <summary>
        /// Wrap i32 to i8 and store 1 byte.
        /// </summary>
        [OpCodeCharacteristics("i32.store8", 120)]
        Int32Store8 = 0x3a,

        /// <summary>
        /// Wrap i32 to i16 and store 2 bytes.
        /// </summary>
        [OpCodeCharacteristics("i32.store16", 120)]
        Int32Store16 = 0x3b,

        /// <summary>
        /// Wrap i64 to i8 and store 1 byte.
        /// </summary>
        [OpCodeCharacteristics("i64.store8", 120)]
        Int64Store8 = 0x3c,

        /// <summary>
        /// Wrap i64 to i16 and store 2 bytes.
        /// </summary>
        [OpCodeCharacteristics("i64.store16", 120)]
        Int64Store16 = 0x3d,

        /// <summary>
        /// Wrap i64 to i32 and store 4 bytes.
        /// </summary>
        [OpCodeCharacteristics("i64.store32", 120)]
        Int64Store32 = 0x3e,

        /// <summary>
        /// Return the current memory size in units of 65536-byte pages.
        /// </summary>
        [OpCodeCharacteristics("current_memory", 100)]
        CurrentMemory = 0x3f,

        /// <summary>
        /// Grow linear memory by a given unsigned delta of 65536-byte pages. Return the previous memory size in units of pages or -1 on failure.
        /// </summary>
        [OpCodeCharacteristics("grow_memory", 10000)]
        GrowMemory = 0x40,

        /// <summary>
        /// Produce the value of an i32 immediate.
        /// </summary>
        [OpCodeCharacteristics("i32.const", 1)]
        Int32Constant = 0x41,

        /// <summary>
        /// Produce the value of an i64 immediate.
        /// </summary>
        [OpCodeCharacteristics("i64.const", 1)]
        Int64Constant = 0x42,

        /// <summary>
        /// Produce the value of an f32 immediate.
        /// </summary>
        [OpCodeCharacteristics("f32.const", 1)]
        Float32Constant = 0x43,

        /// <summary>
        /// Produce the value of an f64 immediate.
        /// </summary>
        [OpCodeCharacteristics("f64.const", 1)]
        Float64Constant = 0x44,

        /// <summary>
        /// Compare equal to zero (return 1 if operand is zero, 0 otherwise).
        /// </summary>
        [OpCodeCharacteristics("i32.eqz", 45)]
        Int32EqualZero = 0x45,

        /// <summary>
        /// Sign-agnostic compare equal.
        /// </summary>
        [OpCodeCharacteristics("i32.eq", 45)]
        Int32Equal = 0x46,

        /// <summary>
        /// Sign-agnostic compare unequal.
        /// </summary>
        [OpCodeCharacteristics("i32.ne", 45)]
        Int32NotEqual = 0x47,

        /// <summary>
        /// Signed less than.
        /// </summary>
        [OpCodeCharacteristics("i32.lt_s", 45)]
        Int32LessThanSigned = 0x48,

        /// <summary>
        /// Unsigned less than.
        /// </summary>
        [OpCodeCharacteristics("i32.lt_u", 45)]
        Int32LessThanUnsigned = 0x49,

        /// <summary>
        /// Signed greater than.
        /// </summary>
        [OpCodeCharacteristics("i32.gt_s", 45)]
        Int32GreaterThanSigned = 0x4a,

        /// <summary>
        /// Unsigned greater than.
        /// </summary>
        [OpCodeCharacteristics("i32.gt_u", 45)]
        Int32GreaterThanUnsigned = 0x4b,

        /// <summary>
        /// Signed less than or equal.
        /// </summary>
        [OpCodeCharacteristics("i32.le_s", 45)]
        Int32LessThanOrEqualSigned = 0x4c,

        /// <summary>
        /// Unsigned less than or equal.
        /// </summary>
        [OpCodeCharacteristics("i32.le_u", 45)]
        Int32LessThanOrEqualUnsigned = 0x4d,

        /// <summary>
        /// Signed greater than or equal.
        /// </summary>
        [OpCodeCharacteristics("i32.ge_s", 45)]
        Int32GreaterThanOrEqualSigned = 0x4e,

        /// <summary>
        /// Unsigned greater than or equal.
        /// </summary>
        [OpCodeCharacteristics("i32.ge_u", 45)]
        Int32GreaterThanOrEqualUnsigned = 0x4f,

        /// <summary>
        /// Compare equal to zero (return 1 if operand is zero, 0 otherwise).
        /// </summary>
        [OpCodeCharacteristics("i64.eqz", 45)]
        Int64EqualZero = 0x50,

        /// <summary>
        /// Sign-agnostic compare equal.
        /// </summary>
        [OpCodeCharacteristics("i64.eq", 45)]
        Int64Equal = 0x51,

        /// <summary>
        /// Sign-agnostic compare unequal.
        /// </summary>
        [OpCodeCharacteristics("i64.ne", 45)]
        Int64NotEqual = 0x52,

        /// <summary>
        /// Signed less than.
        /// </summary>
        [OpCodeCharacteristics("i64.lt_s", 45)]
        Int64LessThanSigned = 0x53,

        /// <summary>
        /// Unsigned less than.
        /// </summary>
        [OpCodeCharacteristics("i64.lt_u", 45)]
        Int64LessThanUnsigned = 0x54,

        /// <summary>
        /// Signed greater than.
        /// </summary>
        [OpCodeCharacteristics("i64.gt_s", 45)]
        Int64GreaterThanSigned = 0x55,

        /// <summary>
        /// Unsigned greater than.
        /// </summary>
        [OpCodeCharacteristics("i64.gt_u", 45)]
        Int64GreaterThanUnsigned = 0x56,

        /// <summary>
        /// Signed less than or equal.
        /// </summary>
        [OpCodeCharacteristics("i64.le_s", 45)]
        Int64LessThanOrEqualSigned = 0x57,

        /// <summary>
        /// Unsigned less than or equal.
        /// </summary>
        [OpCodeCharacteristics("i64.le_u", 45)]
        Int64LessThanOrEqualUnsigned = 0x58,

        /// <summary>
        /// Signed greater than or equal.
        /// </summary>
        [OpCodeCharacteristics("i64.ge_s", 45)]
        Int64GreaterThanOrEqualSigned = 0x59,

        /// <summary>
        /// Unsigned greater than or equal.
        /// </summary>
        [OpCodeCharacteristics("i64.ge_u", 45)]
        Int64GreaterThanOrEqualUnsigned = 0x5a,

        /// <summary>
        /// Compare ordered and equal.
        /// </summary>
        [OpCodeCharacteristics("f32.eq", 45)]
        Float32Equal = 0x5b,

        /// <summary>
        /// Compare unordered or unequal.
        /// </summary>
        [OpCodeCharacteristics("f32.ne", 45)]
        Float32NotEqual = 0x5c,

        /// <summary>
        /// Compare ordered and less than.
        /// </summary>
        [OpCodeCharacteristics("f32.lt", 45)]
        Float32LessThan = 0x5d,

        /// <summary>
        /// Compare ordered and greater than.
        /// </summary>
        [OpCodeCharacteristics("f32.gt", 45)]
        Float32GreaterThan = 0x5e,

        /// <summary>
        /// Compare ordered and less than or equal.
        /// </summary>
        [OpCodeCharacteristics("f32.le", 45)]
        Float32LessThanOrEqual = 0x5f,

        /// <summary>
        /// Compare ordered and greater than or equal.
        /// </summary>
        [OpCodeCharacteristics("f32.ge", 45)]
        Float32GreaterThanOrEqual = 0x60,

        /// <summary>
        /// Compare ordered and equal.
        /// </summary>
        [OpCodeCharacteristics("f64.eq", 45)]
        Float64Equal = 0x61,

        /// <summary>
        /// Compare unordered or unequal.
        /// </summary>
        [OpCodeCharacteristics("f64.ne", 45)]
        Float64NotEqual = 0x62,

        /// <summary>
        /// Compare ordered and less than.
        /// </summary>
        [OpCodeCharacteristics("f64.lt", 45)]
        Float64LessThan = 0x63,

        /// <summary>
        /// Compare ordered and greater than.
        /// </summary>
        [OpCodeCharacteristics("f64.gt", 45)]
        Float64GreaterThan = 0x64,

        /// <summary>
        /// Compare ordered and less than or equal.
        /// </summary>
        [OpCodeCharacteristics("f64.le", 45)]
        Float64LessThanOrEqual = 0x65,

        /// <summary>
        /// Compare ordered and greater than or equal.
        /// </summary>
        [OpCodeCharacteristics("f64.ge", 45)]
        Float64GreaterThanOrEqual = 0x66,

        /// <summary>
        /// Sign-agnostic count leading zero bits.  All zero bits are considered leading if the value is zero.
        /// </summary>
        [OpCodeCharacteristics("i32.clz", 45)]
        Int32CountLeadingZeroes = 0x67,

        /// <summary>
        /// Sign-agnostic count trailing zero bits.  All zero bits are considered trailing if the value is zero.
        /// </summary>
        [OpCodeCharacteristics("i32.ctz", 45)]
        Int32CountTrailingZeroes = 0x68,

        /// <summary>
        /// Sign-agnostic count number of one bits.
        /// </summary>
        [OpCodeCharacteristics("i32.popcnt", 100)]
        Int32CountOneBits = 0x69,

        /// <summary>
        /// Sign-agnostic addition.
        /// </summary>
        [OpCodeCharacteristics("i32.add", 45)]
        Int32Add = 0x6a,

        /// <summary>
        /// Sign-agnostic subtraction.
        /// </summary>
        [OpCodeCharacteristics("i32.sub", 45)]
        Int32Subtract = 0x6b,

        /// <summary>
        /// Sign-agnostic multiplication (lower 32-bits).
        /// </summary>
        [OpCodeCharacteristics("i32.mul", 45)]
        Int32Multiply = 0x6c,

        /// <summary>
        /// Signed division (result is truncated toward zero).
        /// </summary>
        [OpCodeCharacteristics("i32.div_s", 900)]
        Int32DivideSigned = 0x6d,

        /// <summary>
        /// Unsigned division (result is floored).
        /// </summary>
        [OpCodeCharacteristics("i32.div_u", 900)]
        Int32DivideUnsigned = 0x6e,

        /// <summary>
        /// Signed remainder (result has the sign of the dividend).
        /// </summary>
        [OpCodeCharacteristics("i32.rem_s", 900)]
        Int32RemainderSigned = 0x6f,

        /// <summary>
        /// Unsigned remainder.
        /// </summary>
        [OpCodeCharacteristics("i32.rem_u", 900)]
        Int32RemainderUnsigned = 0x70,

        /// <summary>
        /// Sign-agnostic bitwise and.
        /// </summary>
        [OpCodeCharacteristics("i32.and", 45)]
        Int32And = 0x71,

        /// <summary>
        /// Sign-agnostic bitwise inclusive or.
        /// </summary>
        [OpCodeCharacteristics("i32.or", 45)]
        Int32Or = 0x72,

        /// <summary>
        /// Sign-agnostic bitwise exclusive or.
        /// </summary>
        [OpCodeCharacteristics("i32.xor", 45)]
        Int32ExclusiveOr = 0x73,

        /// <summary>
        /// Sign-agnostic shift left.
        /// </summary>
        [OpCodeCharacteristics("i32.shl", 67)]
        Int32ShiftLeft = 0x74,

        /// <summary>
        /// Zero-replicating (logical) shift right.
        /// </summary>
        [OpCodeCharacteristics("i32.shr_s", 67)]
        Int32ShiftRightSigned = 0x75,

        /// <summary>
        /// Sign-replicating (arithmetic) shift right.
        /// </summary>
        [OpCodeCharacteristics("i32.shr_u", 67)]
        Int32ShiftRightUnsigned = 0x76,

        /// <summary>
        /// Sign-agnostic rotate left.
        /// </summary>
        [OpCodeCharacteristics("i32.rotl", 90)]
        Int32RotateLeft = 0x77,

        /// <summary>
        /// Sign-agnostic rotate right.
        /// </summary>
        [OpCodeCharacteristics("i32.rotr", 90)]
        Int32RotateRight = 0x78,

        /// <summary>
        /// Sign-agnostic count leading zero bits.  All zero bits are considered leading if the value is zero.
        /// </summary>
        [OpCodeCharacteristics("i64.clz", 45)]
        Int64CountLeadingZeroes = 0x79,

        /// <summary>
        /// Sign-agnostic count trailing zero bits.  All zero bits are considered trailing if the value is zero.
        /// </summary>
        [OpCodeCharacteristics("i64.ctz", 45)]
        Int64CountTrailingZeroes = 0x7a,

        /// <summary>
        /// Sign-agnostic count number of one bits.
        /// </summary>
        [OpCodeCharacteristics("i64.popcnt", 100)]
        Int64CountOneBits = 0x7b,

        /// <summary>
        /// Sign-agnostic addition.
        /// </summary>
        [OpCodeCharacteristics("i64.add", 45)]
        Int64Add = 0x7c,

        /// <summary>
        /// Sign-agnostic subtraction.
        /// </summary>
        [OpCodeCharacteristics("i64.sub", 45)]
        Int64Subtract = 0x7d,

        /// <summary>
        /// Sign-agnostic multiplication (lower 64-bits).
        /// </summary>
        [OpCodeCharacteristics("i64.mul", 45)]
        Int64Multiply = 0x7e,

        /// <summary>
        /// Signed division (result is truncated toward zero).
        /// </summary>
        [OpCodeCharacteristics("i64.div_s", 900)]
        Int64DivideSigned = 0x7f,

        /// <summary>
        /// Unsigned division (result is floored).
        /// </summary>
        [OpCodeCharacteristics("i64.div_u", 900)]
        Int64DivideUnsigned = 0x80,

        /// <summary>
        /// Signed remainder (result has the sign of the dividend).
        /// </summary>
        [OpCodeCharacteristics("i64.rem_s", 900)]
        Int64RemainderSigned = 0x81,

        /// <summary>
        /// Unsigned remainder.
        /// </summary>
        [OpCodeCharacteristics("i64.rem_u", 900)]
        Int64RemainderUnsigned = 0x82,

        /// <summary>
        /// Sign-agnostic bitwise and.
        /// </summary>
        [OpCodeCharacteristics("i64.and", 45)]
        Int64And = 0x83,

        /// <summary>
        /// Sign-agnostic bitwise inclusive or.
        /// </summary>
        [OpCodeCharacteristics("i64.or", 45)]
        Int64Or = 0x84,

        /// <summary>
        /// Sign-agnostic bitwise exclusive or.
        /// </summary>
        [OpCodeCharacteristics("i64.xor", 45)]
        Int64ExclusiveOr = 0x85,

        /// <summary>
        /// Sign-agnostic shift left.
        /// </summary>
        [OpCodeCharacteristics("i64.shl", 67)]
        Int64ShiftLeft = 0x86,

        /// <summary>
        /// Zero-replicating (logical) shift right.
        /// </summary>
        [OpCodeCharacteristics("i64.shr_s", 67)]
        Int64ShiftRightSigned = 0x87,

        /// <summary>
        /// Sign-replicating (arithmetic) shift right.
        /// </summary>
        [OpCodeCharacteristics("i64.shr_u", 67)]
        Int64ShiftRightUnsigned = 0x88,

        /// <summary>
        /// Sign-agnostic rotate left.
        /// </summary>
        [OpCodeCharacteristics("i64.rotl", 90)]
        Int64RotateLeft = 0x89,

        /// <summary>
        /// Sign-agnostic rotate right.
        /// </summary>
        [OpCodeCharacteristics("i64.rotr", 90)]
        Int64RotateRight = 0x8a,

        /// <summary>
        /// Absolute value.
        /// </summary>
        [OpCodeCharacteristics("f32.abs", 90)]
        Float32Absolute = 0x8b,

        /// <summary>
        /// Negation.
        /// </summary>
        [OpCodeCharacteristics("f32.neg", 90)]
        Float32Negate = 0x8c,

        /// <summary>
        /// Ceiling operator.
        /// </summary>
        [OpCodeCharacteristics("f32.ceil", 90)]
        Float32Ceiling = 0x8d,

        /// <summary>
        /// Floor operator.
        /// </summary>
        [OpCodeCharacteristics("f32.floor", 90)]
        Float32Floor = 0x8e,

        /// <summary>
        /// Round to nearest integer towards zero.
        /// </summary>
        [OpCodeCharacteristics("f32.trunc", 90)]
        Float32Truncate = 0x8f,

        /// <summary>
        /// Round to nearest integer, ties to even.
        /// </summary>
        [OpCodeCharacteristics("f32.nearest", 90)]
        Float32Nearest = 0x90,

        /// <summary>
        /// Square root.
        /// </summary>
        [OpCodeCharacteristics("f32.sqrt", 90)]
        Float32SquareRoot = 0x91,

        /// <summary>
        /// Addition.
        /// </summary>
        [OpCodeCharacteristics("f32.add", 90)]
        Float32Add = 0x92,

        /// <summary>
        /// Subtraction.
        /// </summary>
        [OpCodeCharacteristics("f32.sub", 90)]
        Float32Subtract = 0x93,

        /// <summary>
        /// Multiplication.
        /// </summary>
        [OpCodeCharacteristics("f32.mul", 90)]
        Float32Multiply = 0x94,

        /// <summary>
        /// Division.
        /// </summary>
        [OpCodeCharacteristics("f32.div", 90)]
        Float32Divide = 0x95,

        /// <summary>
        /// Minimum (binary operator); if either operand is NaN, returns NaN.
        /// </summary>
        [OpCodeCharacteristics("f32.min", 90)]
        Float32Minimum = 0x96,

        /// <summary>
        /// Maximum (binary operator); if either operand is NaN, returns NaN.
        /// </summary>
        [OpCodeCharacteristics("f32.max", 90)]
        Float32Maximum = 0x97,

        /// <summary>
        /// Copysign.
        /// </summary>
        [OpCodeCharacteristics("f32.copysign", 90)]
        Float32CopySign = 0x98,

        /// <summary>
        /// Absolute value.
        /// </summary>
        [OpCodeCharacteristics("f64.abs", 90)]
        Float64Absolute = 0x99,

        /// <summary>
        /// Negation.
        /// </summary>
        [OpCodeCharacteristics("f64.neg", 90)]
        Float64Negate = 0x9a,

        /// <summary>
        /// Ceiling operator.
        /// </summary>
        [OpCodeCharacteristics("f64.ceil", 90)]
        Float64Ceiling = 0x9b,

        /// <summary>
        /// Floor operator.
        /// </summary>
        [OpCodeCharacteristics("f64.floor", 90)]
        Float64Floor = 0x9c,

        /// <summary>
        /// Round to nearest integer towards zero.
        /// </summary>
        [OpCodeCharacteristics("f64.trunc", 90)]
        Float64Truncate = 0x9d,

        /// <summary>
        /// Round to nearest integer, ties to even.
        /// </summary>
        [OpCodeCharacteristics("f64.nearest", 90)]
        Float64Nearest = 0x9e,

        /// <summary>
        /// Square root.
        /// </summary>
        [OpCodeCharacteristics("f64.sqrt", 90)]
        Float64SquareRoot = 0x9f,

        /// <summary>
        /// Addition.
        /// </summary>
        [OpCodeCharacteristics("f64.add", 90)]
        Float64Add = 0xa0,

        /// <summary>
        /// Subtraction.
        /// </summary>
        [OpCodeCharacteristics("f64.sub", 90)]
        Float64Subtract = 0xa1,

        /// <summary>
        /// Multiplication.
        /// </summary>
        [OpCodeCharacteristics("f64.mul", 90)]
        Float64Multiply = 0xa2,

        /// <summary>
        /// Division.
        /// </summary>
        [OpCodeCharacteristics("f64.div", 90)]
        Float64Divide = 0xa3,

        /// <summary>
        /// Minimum (binary operator); if either operand is NaN, returns NaN.
        /// </summary>
        [OpCodeCharacteristics("f64.min", 90)]
        Float64Minimum = 0xa4,

        /// <summary>
        /// Maximum (binary operator); if either operand is NaN, returns NaN.
        /// </summary>
        [OpCodeCharacteristics("f64.max", 90)]
        Float64Maximum = 0xa5,

        /// <summary>
        /// Copysign.
        /// </summary>
        [OpCodeCharacteristics("f64.copysign", 90)]
        Float64CopySign = 0xa6,

        /// <summary>
        /// Wrap a 64-bit integer to a 32-bit integer.
        /// </summary>
        [OpCodeCharacteristics("i32.wrap/i64", 90)]
        Int32WrapInt64 = 0xa7,

        /// <summary>
        /// Truncate a 32-bit float to a signed 32-bit integer.
        /// </summary>
        [OpCodeCharacteristics("i32.trunc_s/f32", 90)]
        Int32TruncateSignedFloat32 = 0xa8,

        /// <summary>
        /// Truncate a 32-bit float to an unsigned 32-bit integer.
        /// </summary>
        [OpCodeCharacteristics("i32.trunc_u/f32", 90)]
        Int32TruncateUnsignedFloat32 = 0xa9,

        /// <summary>
        /// Truncate a 64-bit float to a signed 32-bit integer.
        /// </summary>
        [OpCodeCharacteristics("i32.trunc_s/f64", 90)]
        Int32TruncateSignedFloat64 = 0xaa,

        /// <summary>
        /// Truncate a 64-bit float to an unsigned 32-bit integer.
        /// </summary>
        [OpCodeCharacteristics("i32.trunc_u/f64", 90)]
        Int32TruncateUnsignedFloat64 = 0xab,

        /// <summary>
        /// Extend a signed 32-bit integer to a 64-bit integer.
        /// </summary>
        [OpCodeCharacteristics("i64.extend_s/i32", 90)]
        Int64ExtendSignedInt32 = 0xac,

        /// <summary>
        /// Extend an unsigned 32-bit integer to a 64-bit integer.
        /// </summary>
        [OpCodeCharacteristics("i64.extend_u/i32", 90)]
        Int64ExtendUnsignedInt32 = 0xad,

        /// <summary>
        /// Truncate a 32-bit float to a signed 64-bit integer.
        /// </summary>
        [OpCodeCharacteristics("i64.trunc_s/f32", 90)]
        Int64TruncateSignedFloat32 = 0xae,

        /// <summary>
        /// Truncate a 32-bit float to an unsigned 64-bit integer.
        /// </summary>
        [OpCodeCharacteristics("i64.trunc_u/f32", 90)]
        Int64TruncateUnsignedFloat32 = 0xaf,

        /// <summary>
        /// Truncate a 64-bit float to a signed 64-bit integer.
        /// </summary>
        [OpCodeCharacteristics("i64.trunc_s/f64", 90)]
        Int64TruncateSignedFloat64 = 0xb0,

        /// <summary>
        /// Truncate a 64-bit float to an unsigned 64-bit integer.
        /// </summary>
        [OpCodeCharacteristics("i64.trunc_u/f64", 90)]
        Int64TruncateUnsignedFloat64 = 0xb1,

        /// <summary>
        /// Convert a signed 32-bit integer to a 32-bit float.
        /// </summary>
        [OpCodeCharacteristics("f32.convert_s/i32", 90)]
        Float32ConvertSignedInt32 = 0xb2,

        /// <summary>
        /// Convert an unsigned 32-bit integer to a 32-bit float.
        /// </summary>
        [OpCodeCharacteristics("f32.convert_u/i32", 90)]
        Float32ConvertUnsignedInt32 = 0xb3,

        /// <summary>
        /// Convert a signed 64-bit integer to a 32-bit float.
        /// </summary>
        [OpCodeCharacteristics("f32.convert_s/i64", 90)]
        Float32ConvertSignedInt64 = 0xb4,

        /// <summary>
        /// Convert an unsigned 64-bit integer to a 32-bit float.
        /// </summary>
        [OpCodeCharacteristics("f32.convert_u/i64", 90)]
        Float32ConvertUnsignedInt64 = 0xb5,

        /// <summary>
        /// Demote a 64-bit float to a 32-bit float.
        /// </summary>
        [OpCodeCharacteristics("f32.demote/f64", 90)]
        Float32DemoteFloat64 = 0xb6,

        /// <summary>
        /// Convert a signed 32-bit integer to a 64-bit float.
        /// </summary>
        [OpCodeCharacteristics("f64.convert_s/i32", 90)]
        Float64ConvertSignedInt32 = 0xb7,

        /// <summary>
        /// Convert an unsigned 32-bit integer to a 64-bit float.
        /// </summary>
        [OpCodeCharacteristics("f64.convert_u/i32", 90)]
        Float64ConvertUnsignedInt32 = 0xb8,

        /// <summary>
        /// Convert a signed 64-bit integer to a 64-bit float.
        /// </summary>
        [OpCodeCharacteristics("f64.convert_s/i64", 90)]
        Float64ConvertSignedInt64 = 0xb9,

        /// <summary>
        /// Convert an unsigned 64-bit integer to a 64-bit float.
        /// </summary>
        [OpCodeCharacteristics("f64.convert_u/i64", 90)]
        Float64ConvertUnsignedInt64 = 0xba,

        /// <summary>
        /// Promote a 32-bit float to a 64-bit float.
        /// </summary>
        [OpCodeCharacteristics("f64.promote/f32", 90)]
        Float64PromoteFloat32 = 0xbb,

        /// <summary>
        /// Reinterpret the bits of a 32-bit float as a 32-bit integer.
        /// </summary>
        [OpCodeCharacteristics("i32.reinterpret/f32", 90)]
        Int32ReinterpretFloat32 = 0xbc,

        /// <summary>
        /// Reinterpret the bits of a 64-bit float as a 64-bit integer.
        /// </summary>
        [OpCodeCharacteristics("i64.reinterpret/f64", 90)]
        Int64ReinterpretFloat64 = 0xbd,

        /// <summary>
        /// Reinterpret the bits of a 32-bit integer as a 32-bit float.
        /// </summary>
        [OpCodeCharacteristics("f32.reinterpret/i32", 90)]
        Float32ReinterpretInt32 = 0xbe,

        /// <summary>
        /// Reinterpret the bits of a 64-bit integer as a 64-bit float.
        /// </summary>
        [OpCodeCharacteristics("f64.reinterpret/i64", 90)]
        Float64ReinterpretInt64 = 0xbf,
    }
}