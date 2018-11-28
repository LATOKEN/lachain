using System.Collections.Generic;

namespace Phorkus.VM
{
    public class OpcodeDesc
    {
        public static Dictionary<Opcode, OpcodeDesc> Opcodes = new Dictionary<Opcode, OpcodeDesc>
        {
            {Opcode.Stop, new OpcodeDesc("STOP", 0, 0, 0, true, GasPriceTier.ZeroTier)},
            {Opcode.Add, new OpcodeDesc("ADD", 0, 2, 1, false, GasPriceTier.VeryLowTier)},
            {Opcode.Sub, new OpcodeDesc("SUB", 0, 2, 1, false, GasPriceTier.VeryLowTier)},
            {Opcode.Mul, new OpcodeDesc("MUL", 0, 2, 1, false, GasPriceTier.LowTier)},
            {Opcode.Div, new OpcodeDesc("DIV", 0, 2, 1, false, GasPriceTier.LowTier)},
            {Opcode.Sdiv, new OpcodeDesc("SDIV", 0, 2, 1, false, GasPriceTier.LowTier)},
            {Opcode.Mod, new OpcodeDesc("MOD", 0, 2, 1, false, GasPriceTier.LowTier)},
            {Opcode.Smod, new OpcodeDesc("SMOD", 0, 2, 1, false, GasPriceTier.LowTier)},
            {Opcode.Exp, new OpcodeDesc("EXP", 0, 2, 1, false, GasPriceTier.SpecialTier)},
            {Opcode.Not, new OpcodeDesc("NOT", 0, 1, 1, false, GasPriceTier.VeryLowTier)},
            {Opcode.Lt, new OpcodeDesc("LT", 0, 2, 1, false, GasPriceTier.VeryLowTier)},
            {Opcode.Gt, new OpcodeDesc("GT", 0, 2, 1, false, GasPriceTier.VeryLowTier)},
            {Opcode.Slt, new OpcodeDesc("SLT", 0, 2, 1, false, GasPriceTier.VeryLowTier)},
            {Opcode.Sgt, new OpcodeDesc("SGT", 0, 2, 1, false, GasPriceTier.VeryLowTier)},
            {Opcode.Eq, new OpcodeDesc("EQ", 0, 2, 1, false, GasPriceTier.VeryLowTier)},
            {Opcode.Iszero, new OpcodeDesc("ISZERO", 0, 1, 1, false, GasPriceTier.VeryLowTier)},
            {Opcode.And, new OpcodeDesc("AND", 0, 2, 1, false, GasPriceTier.VeryLowTier)},
            {Opcode.Or, new OpcodeDesc("OR", 0, 2, 1, false, GasPriceTier.VeryLowTier)},
            {Opcode.Xor, new OpcodeDesc("XOR", 0, 2, 1, false, GasPriceTier.VeryLowTier)},
            {Opcode.Byte, new OpcodeDesc("BYTE", 0, 2, 1, false, GasPriceTier.VeryLowTier)},
            {Opcode.Addmod, new OpcodeDesc("ADDMOD", 0, 3, 1, false, GasPriceTier.MidTier)},
            {Opcode.Mulmod, new OpcodeDesc("MULMOD", 0, 3, 1, false, GasPriceTier.MidTier)},
            {Opcode.Signextend, new OpcodeDesc("SIGNEXTEND", 0, 2, 1, false, GasPriceTier.LowTier)},
            {Opcode.Sha3, new OpcodeDesc("SHA3", 0, 2, 1, false, GasPriceTier.SpecialTier)},
            {Opcode.Address, new OpcodeDesc("ADDRESS", 0, 0, 1, false, GasPriceTier.BaseTier)},
            {Opcode.Balance, new OpcodeDesc("BALANCE", 0, 1, 1, false, GasPriceTier.ExtTier)},
            {Opcode.Origin, new OpcodeDesc("ORIGIN", 0, 0, 1, false, GasPriceTier.BaseTier)},
            {Opcode.Caller, new OpcodeDesc("CALLER", 0, 0, 1, false, GasPriceTier.BaseTier)},
            {Opcode.Callvalue, new OpcodeDesc("CALLVALUE", 0, 0, 1, false, GasPriceTier.BaseTier)},
            {Opcode.Calldataload, new OpcodeDesc("CALLDATALOAD", 0, 1, 1, false, GasPriceTier.VeryLowTier)},
            {Opcode.Calldatasize, new OpcodeDesc("CALLDATASIZE", 0, 0, 1, false, GasPriceTier.BaseTier)},
            {Opcode.Calldatacopy, new OpcodeDesc("CALLDATACOPY", 0, 3, 0, true, GasPriceTier.VeryLowTier)},
            {Opcode.Codesize, new OpcodeDesc("CODESIZE", 0, 0, 1, false, GasPriceTier.BaseTier)},
            {Opcode.Codecopy, new OpcodeDesc("CODECOPY", 0, 3, 0, true, GasPriceTier.VeryLowTier)},
            {Opcode.Gasprice, new OpcodeDesc("GASPRICE", 0, 0, 1, false, GasPriceTier.BaseTier)},
            {Opcode.Extcodesize, new OpcodeDesc("EXTCODESIZE", 0, 1, 1, false, GasPriceTier.ExtTier)},
            {Opcode.Extcodecopy, new OpcodeDesc("EXTCODECOPY", 0, 4, 0, true, GasPriceTier.ExtTier)},
            {Opcode.Blockhash, new OpcodeDesc("BLOCKHASH", 0, 1, 1, false, GasPriceTier.ExtTier)},
            {Opcode.Coinbase, new OpcodeDesc("COINBASE", 0, 0, 1, false, GasPriceTier.BaseTier)},
            {Opcode.Timestamp, new OpcodeDesc("TIMESTAMP", 0, 0, 1, false, GasPriceTier.BaseTier)},
            {Opcode.Number, new OpcodeDesc("NUMBER", 0, 0, 1, false, GasPriceTier.BaseTier)},
            {Opcode.Difficulty, new OpcodeDesc("DIFFICULTY", 0, 0, 1, false, GasPriceTier.BaseTier)},
            {Opcode.Gaslimit, new OpcodeDesc("GASLIMIT", 0, 0, 1, false, GasPriceTier.BaseTier)},
            {Opcode.Pop, new OpcodeDesc("POP", 0, 1, 0, false, GasPriceTier.BaseTier)},
            {Opcode.Mload, new OpcodeDesc("MLOAD", 0, 1, 1, false, GasPriceTier.VeryLowTier)},
            {Opcode.Mstore, new OpcodeDesc("MSTORE", 0, 2, 0, true, GasPriceTier.VeryLowTier)},
            {Opcode.Mstore8, new OpcodeDesc("MSTORE8", 0, 2, 0, true, GasPriceTier.VeryLowTier)},
            {Opcode.Sload, new OpcodeDesc("SLOAD", 0, 1, 1, false, GasPriceTier.SpecialTier)},
            {Opcode.Sstore, new OpcodeDesc("SSTORE", 0, 2, 0, true, GasPriceTier.SpecialTier)},
            {Opcode.Jump, new OpcodeDesc("JUMP", 0, 1, 0, true, GasPriceTier.MidTier)},
            {Opcode.Jumpi, new OpcodeDesc("JUMPI", 0, 2, 0, true, GasPriceTier.HighTier)},
            {Opcode.Getpc, new OpcodeDesc("PC", 0, 0, 1, false, GasPriceTier.BaseTier)},
            {Opcode.Msize, new OpcodeDesc("MSIZE", 0, 0, 1, false, GasPriceTier.BaseTier)},
            {Opcode.Gas, new OpcodeDesc("GAS", 0, 0, 1, false, GasPriceTier.BaseTier)},
            {Opcode.Jumpdest, new OpcodeDesc("JUMPDEST", 0, 0, 0, true, GasPriceTier.SpecialTier)},
            {Opcode.Push1, new OpcodeDesc("PUSH1", 1, 0, 1, false, GasPriceTier.VeryLowTier)},
            {Opcode.Push2, new OpcodeDesc("PUSH2", 2, 0, 1, false, GasPriceTier.VeryLowTier)},
            {Opcode.Push3, new OpcodeDesc("PUSH3", 3, 0, 1, false, GasPriceTier.VeryLowTier)},
            {Opcode.Push4, new OpcodeDesc("PUSH4", 4, 0, 1, false, GasPriceTier.VeryLowTier)},
            {Opcode.Push5, new OpcodeDesc("PUSH5", 5, 0, 1, false, GasPriceTier.VeryLowTier)},
            {Opcode.Push6, new OpcodeDesc("PUSH6", 6, 0, 1, false, GasPriceTier.VeryLowTier)},
            {Opcode.Push7, new OpcodeDesc("PUSH7", 7, 0, 1, false, GasPriceTier.VeryLowTier)},
            {Opcode.Push8, new OpcodeDesc("PUSH8", 8, 0, 1, false, GasPriceTier.VeryLowTier)},
            {Opcode.Push9, new OpcodeDesc("PUSH9", 9, 0, 1, false, GasPriceTier.VeryLowTier)},
            {Opcode.Push10, new OpcodeDesc("PUSH10", 10, 0, 1, false, GasPriceTier.VeryLowTier)},
            {Opcode.Push11, new OpcodeDesc("PUSH11", 11, 0, 1, false, GasPriceTier.VeryLowTier)},
            {Opcode.Push12, new OpcodeDesc("PUSH12", 12, 0, 1, false, GasPriceTier.VeryLowTier)},
            {Opcode.Push13, new OpcodeDesc("PUSH13", 13, 0, 1, false, GasPriceTier.VeryLowTier)},
            {Opcode.Push14, new OpcodeDesc("PUSH14", 14, 0, 1, false, GasPriceTier.VeryLowTier)},
            {Opcode.Push15, new OpcodeDesc("PUSH15", 15, 0, 1, false, GasPriceTier.VeryLowTier)},
            {Opcode.Push16, new OpcodeDesc("PUSH16", 16, 0, 1, false, GasPriceTier.VeryLowTier)},
            {Opcode.Push17, new OpcodeDesc("PUSH17", 17, 0, 1, false, GasPriceTier.VeryLowTier)},
            {Opcode.Push18, new OpcodeDesc("PUSH18", 18, 0, 1, false, GasPriceTier.VeryLowTier)},
            {Opcode.Push19, new OpcodeDesc("PUSH19", 19, 0, 1, false, GasPriceTier.VeryLowTier)},
            {Opcode.Push20, new OpcodeDesc("PUSH20", 20, 0, 1, false, GasPriceTier.VeryLowTier)},
            {Opcode.Push21, new OpcodeDesc("PUSH21", 21, 0, 1, false, GasPriceTier.VeryLowTier)},
            {Opcode.Push22, new OpcodeDesc("PUSH22", 22, 0, 1, false, GasPriceTier.VeryLowTier)},
            {Opcode.Push23, new OpcodeDesc("PUSH23", 23, 0, 1, false, GasPriceTier.VeryLowTier)},
            {Opcode.Push24, new OpcodeDesc("PUSH24", 24, 0, 1, false, GasPriceTier.VeryLowTier)},
            {Opcode.Push25, new OpcodeDesc("PUSH25", 25, 0, 1, false, GasPriceTier.VeryLowTier)},
            {Opcode.Push26, new OpcodeDesc("PUSH26", 26, 0, 1, false, GasPriceTier.VeryLowTier)},
            {Opcode.Push27, new OpcodeDesc("PUSH27", 27, 0, 1, false, GasPriceTier.VeryLowTier)},
            {Opcode.Push28, new OpcodeDesc("PUSH28", 28, 0, 1, false, GasPriceTier.VeryLowTier)},
            {Opcode.Push29, new OpcodeDesc("PUSH29", 29, 0, 1, false, GasPriceTier.VeryLowTier)},
            {Opcode.Push30, new OpcodeDesc("PUSH30", 30, 0, 1, false, GasPriceTier.VeryLowTier)},
            {Opcode.Push31, new OpcodeDesc("PUSH31", 31, 0, 1, false, GasPriceTier.VeryLowTier)},
            {Opcode.Push32, new OpcodeDesc("PUSH32", 32, 0, 1, false, GasPriceTier.VeryLowTier)},
            {Opcode.Dup1, new OpcodeDesc("DUP1", 0, 1, 2, false, GasPriceTier.VeryLowTier)},
            {Opcode.Dup2, new OpcodeDesc("DUP2", 0, 2, 3, false, GasPriceTier.VeryLowTier)},
            {Opcode.Dup3, new OpcodeDesc("DUP3", 0, 3, 4, false, GasPriceTier.VeryLowTier)},
            {Opcode.Dup4, new OpcodeDesc("DUP4", 0, 4, 5, false, GasPriceTier.VeryLowTier)},
            {Opcode.Dup5, new OpcodeDesc("DUP5", 0, 5, 6, false, GasPriceTier.VeryLowTier)},
            {Opcode.Dup6, new OpcodeDesc("DUP6", 0, 6, 7, false, GasPriceTier.VeryLowTier)},
            {Opcode.Dup7, new OpcodeDesc("DUP7", 0, 7, 8, false, GasPriceTier.VeryLowTier)},
            {Opcode.Dup8, new OpcodeDesc("DUP8", 0, 8, 9, false, GasPriceTier.VeryLowTier)},
            {Opcode.Dup9, new OpcodeDesc("DUP9", 0, 9, 10, false, GasPriceTier.VeryLowTier)},
            {Opcode.Dup10, new OpcodeDesc("DUP10", 0, 10, 11, false, GasPriceTier.VeryLowTier)},
            {Opcode.Dup11, new OpcodeDesc("DUP11", 0, 11, 12, false, GasPriceTier.VeryLowTier)},
            {Opcode.Dup12, new OpcodeDesc("DUP12", 0, 12, 13, false, GasPriceTier.VeryLowTier)},
            {Opcode.Dup13, new OpcodeDesc("DUP13", 0, 13, 14, false, GasPriceTier.VeryLowTier)},
            {Opcode.Dup14, new OpcodeDesc("DUP14", 0, 14, 15, false, GasPriceTier.VeryLowTier)},
            {Opcode.Dup15, new OpcodeDesc("DUP15", 0, 15, 16, false, GasPriceTier.VeryLowTier)},
            {Opcode.Dup16, new OpcodeDesc("DUP16", 0, 16, 17, false, GasPriceTier.VeryLowTier)},
            {Opcode.Swap1, new OpcodeDesc("SWAP1", 0, 2, 2, false, GasPriceTier.VeryLowTier)},
            {Opcode.Swap2, new OpcodeDesc("SWAP2", 0, 3, 3, false, GasPriceTier.VeryLowTier)},
            {Opcode.Swap3, new OpcodeDesc("SWAP3", 0, 4, 4, false, GasPriceTier.VeryLowTier)},
            {Opcode.Swap4, new OpcodeDesc("SWAP4", 0, 5, 5, false, GasPriceTier.VeryLowTier)},
            {Opcode.Swap5, new OpcodeDesc("SWAP5", 0, 6, 6, false, GasPriceTier.VeryLowTier)},
            {Opcode.Swap6, new OpcodeDesc("SWAP6", 0, 7, 7, false, GasPriceTier.VeryLowTier)},
            {Opcode.Swap7, new OpcodeDesc("SWAP7", 0, 8, 8, false, GasPriceTier.VeryLowTier)},
            {Opcode.Swap8, new OpcodeDesc("SWAP8", 0, 9, 9, false, GasPriceTier.VeryLowTier)},
            {Opcode.Swap9, new OpcodeDesc("SWAP9", 0, 10, 10, false, GasPriceTier.VeryLowTier)},
            {Opcode.Swap10, new OpcodeDesc("SWAP10", 0, 11, 11, false, GasPriceTier.VeryLowTier)},
            {Opcode.Swap11, new OpcodeDesc("SWAP11", 0, 12, 12, false, GasPriceTier.VeryLowTier)},
            {Opcode.Swap12, new OpcodeDesc("SWAP12", 0, 13, 13, false, GasPriceTier.VeryLowTier)},
            {Opcode.Swap13, new OpcodeDesc("SWAP13", 0, 14, 14, false, GasPriceTier.VeryLowTier)},
            {Opcode.Swap14, new OpcodeDesc("SWAP14", 0, 15, 15, false, GasPriceTier.VeryLowTier)},
            {Opcode.Swap15, new OpcodeDesc("SWAP15", 0, 16, 16, false, GasPriceTier.VeryLowTier)},
            {Opcode.Swap16, new OpcodeDesc("SWAP16", 0, 17, 17, false, GasPriceTier.VeryLowTier)},
            {Opcode.Log0, new OpcodeDesc("LOG0", 0, 2, 0, true, GasPriceTier.SpecialTier)},
            {Opcode.Log1, new OpcodeDesc("LOG1", 0, 3, 0, true, GasPriceTier.SpecialTier)},
            {Opcode.Log2, new OpcodeDesc("LOG2", 0, 4, 0, true, GasPriceTier.SpecialTier)},
            {Opcode.Log3, new OpcodeDesc("LOG3", 0, 5, 0, true, GasPriceTier.SpecialTier)},
            {Opcode.Log4, new OpcodeDesc("LOG4", 0, 6, 0, true, GasPriceTier.SpecialTier)},
            {Opcode.Create, new OpcodeDesc("CREATE", 0, 3, 1, true, GasPriceTier.SpecialTier)},
            {Opcode.Call, new OpcodeDesc("CALL", 0, 7, 1, true, GasPriceTier.SpecialTier)},
            {Opcode.Callcode, new OpcodeDesc("CALLCODE", 0, 7, 1, true, GasPriceTier.SpecialTier)},
            {Opcode.Return, new OpcodeDesc("RETURN", 0, 2, 0, true, GasPriceTier.ZeroTier)},
            {Opcode.Delegatecall, new OpcodeDesc("DELEGATECALL", 0, 6, 1, true, GasPriceTier.SpecialTier)},
        };

        private OpcodeDesc(string name, int additional, int args, int ret, bool sideEffects, GasPriceTier gasPriceTier)
        {
            Name = name;
            Additional = additional;
            Args = args;
            Ret = ret;
            SideEffects = sideEffects;
            GasPriceTier = gasPriceTier;
        }

        /// <summary>
        /// The name of the Opcode.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Additional items required in memory for this instructions (only for PUSH).
        /// </summary>
        public int Additional { get; }

        /// <summary>
        /// Number of items required on the stack for this Opcode (and, for the purposes of ret, the number taken from the stack).
        /// </summary>
        public int Args { get; }

        /// <summary>
        /// Number of items placed (back) on the stack by this Opcode, assuming args items were removed.
        /// </summary>
        public int Ret { get; }

        /// <summary>
        /// false if the only effect on the execution environment (apart from gas usage) is a change to a topmost segment of the stack
        /// </summary>
        public bool SideEffects { get; }

        /// <summary>
        /// Tier for gas pricing.
        /// </summary>
        public GasPriceTier GasPriceTier { get; }
    }
}