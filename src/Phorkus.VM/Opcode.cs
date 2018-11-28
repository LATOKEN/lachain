namespace Phorkus.VM
{
    /// <summary>
    /// https://github.com/trailofbits/evm-opcodes
    /// </summary>
    public enum Opcode
    {
        Stop = 0x00,
        Add,
        Mul,
        Sub,
        Div,
        Sdiv,
        Mod,
        Smod,
        Addmod,
        Mulmod,
        Exp,
        Signextend,

        /* Unused: 0x0c - 0x0f (free 3) */

        Lt = 0x10,
        Gt,
        Slt,
        Sgt,
        Eq,
        Iszero,
        And,
        Or,
        Xor,
        Not,
        Byte,
        Sha3 = 0x20,

        /* Unused: 0x21 - 0x2f (free 14) */

        Address = 0x30,
        Balance,
        Origin,
        Caller,
        Callvalue,
        Calldataload,
        Calldatasize,
        Calldatacopy,
        Codesize,
        Codecopy,
        Gasprice,
        Extcodesize,
        Extcodecopy,
        Returndatasize,
        Returndatacopy,

        /* Unused: 0x3f (free 1) */

        Blockhash = 0x40,
        Coinbase,
        Timestamp,
        Number,
        Difficulty,
        Gaslimit,

        /* Unused: 0x46 - 0x4f (free 9) */

        Pop = 0x50,
        Mload,
        Mstore,
        Mstore8,
        Sload,
        Sstore,
        Jump,
        Jumpi,
        Getpc,
        Msize,
        Gas,
        Jumpdest,

        /* Uused: 0x5c - 0x5f (free 3) */

        Push1 = 0x60,
        Push2,
        Push3,
        Push4,
        Push5,
        Push6,
        Push7,
        Push8,
        Push9,
        Push10,
        Push11,
        Push12,
        Push13,
        Push14,
        Push15,
        Push16,
        Push17,
        Push18,
        Push19,
        Push20,
        Push21,
        Push22,
        Push23,
        Push24,
        Push25,
        Push26,
        Push27,
        Push28,
        Push29,
        Push30,
        Push31,
        Push32,

        Dup1 = 0x80,
        Dup2,
        Dup3,
        Dup4,
        Dup5,
        Dup6,
        Dup7,
        Dup8,
        Dup9,
        Dup10,
        Dup11,
        Dup12,
        Dup13,
        Dup14,
        Dup15,
        Dup16,

        Swap1 = 0x90,
        Swap2,
        Swap3,
        Swap4,
        Swap5,
        Swap6,
        Swap7,
        Swap8,
        Swap9,
        Swap10,
        Swap11,
        Swap12,
        Swap13,
        Swap14,
        Swap15,
        Swap16,

        Log0 = 0xa0,
        Log1,
        Log2,
        Log3,
        Log4,

        /* Unused: 0xa5 - 0xaf (10 free) */

        Jumpto = 0xb0,
        Jumpsub,
        Jumpsubv,
        Beginsub,
        Begindata,
        Returnsub,
        Putlocal,
        Getlocal,

        /* Unused: 0xbb - 0xe0 (37 free) */

        Sloadbytes = 0xe1,
        Sstorebytes,
        Ssize,

        /* Unused: 0xe4 - 0xef (11 free) */

        Create = 0xf0,
        Call,
        Callcode,
        Return,
        Delegatecall,
        Callblackbox,

        /* Unused: 0xf6 - 0xf9 (3 free) */

        Staticcall = 0xfa,
        Create2,
        Txexecgas,
        Revert,
        Invalid,
        Selfdestruct,
        
        /* Total free: 91 */
    }
}