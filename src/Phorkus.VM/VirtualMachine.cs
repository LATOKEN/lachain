using System;
using System.Numerics;

namespace Phorkus.VM
{
    public class VirtualMachine
    {
        public void Step(State state)
        {
            var opcode = state.GetCurrentInstruction();
            var instructionInfo = GetInstructionInfo(opcode);
            switch ((Opcode) opcode)
            {
                case Opcode.Stop:
                    break;
                case Opcode.Add:
                    break;
                case Opcode.Mul:
                    break;
                case Opcode.Sub:
                    break;
                case Opcode.Div:
                    break;
                case Opcode.Sdiv:
                    break;
                case Opcode.Mod:
                    break;
                case Opcode.Smod:
                    break;
                case Opcode.Addmod:
                    break;
                case Opcode.Mulmod:
                    break;
                case Opcode.Exp:
                    break;
                case Opcode.Signextend:
                    break;
                case Opcode.Lt:
                    var ltBytes1 = state.StackPop();
                    var ltBytes2 = state.StackPop();
                    if (BitConverter.IsLittleEndian)
                    {
//                        ltBytes1 = ltBytes1.Reverse().ToArray();
//                        ltBytes2 = ltBytes2.Reverse().ToArray();
                    }

                    state.StackPush(new[] {new BigInteger(ltBytes1) < new BigInteger(ltBytes2) ? (byte) 1 : (byte) 0});
                    state.Step();
                    break;
                case Opcode.Gt:
                    var gtBytes1 = state.StackPop();
                    var gtBytes2 = state.StackPop();
                    if (BitConverter.IsLittleEndian)
                    {
//                        gtBytes1 = gtBytes1.Reverse().ToArray();
//                        gtBytes2 = gtBytes2.Reverse().ToArray();
                    }

                    state.StackPush(new[] {new BigInteger(gtBytes1) > new BigInteger(gtBytes2) ? (byte) 1 : (byte) 0});
                    state.Step();
                    break;
                case Opcode.Slt:
                    var sltBytes1 = state.StackPop();
                    var sltBytes2 = state.StackPop();
                    if (BitConverter.IsLittleEndian)
                    {
//                        sltBytes1 = sltBytes1.Reverse().ToArray();
//                        sltBytes2 = sltBytes2.Reverse().ToArray();
                    }

                    state.StackPush(new[]
                        {new BigInteger(sltBytes1) < new BigInteger(sltBytes2) ? (byte) 1 : (byte) 0});
                    state.Step();
                    break;
                case Opcode.Sgt:
                    var sgtBytes1 = state.StackPop();
                    var sgtBytes2 = state.StackPop();
                    if (BitConverter.IsLittleEndian)
                    {
//                        sgtBytes1 = sgtBytes1.Reverse().ToArray();
//                        sgtBytes2 = sgtBytes2.Reverse().ToArray();
                    }

                    state.StackPush(new[]
                        {new BigInteger(sgtBytes1) > new BigInteger(sgtBytes2) ? (byte) 1 : (byte) 0});

                    state.Step();
                    break;
                case Opcode.Eq:
                    var eqBytes1 = state.StackPop();
                    var eqBytes2 = state.StackPop();
                    //check endianism
                    if (BitConverter.IsLittleEndian)
                    {
//                        eqBytes1 = eqBytes1.Reverse().ToArray();
//                        eqBytes2 = eqBytes2.Reverse().ToArray();
                    }

                    state.StackPush(new[] {new BigInteger(eqBytes1) == new BigInteger(eqBytes2) ? (byte) 1 : (byte) 0});
                    state.Step();

                    break;
                case Opcode.Iszero:
                    var isZeroBytes = state.StackPop();
                    //check endianism
                    state.StackPush(new BigInteger(isZeroBytes) == 0 ? new[] {(byte) 1} : new[] {(byte) 0});
                    state.Step();
                    break;
                case Opcode.And:

                    var andBytes1 = state.StackPop();
                    var andBytes2 = state.StackPop();
                    //check endianism
                    var andB1 = new BigInteger(andBytes1);
                    andB1 = andB1 & new BigInteger(andBytes2);

                    state.StackPush(andB1.ToByteArray());
                    state.Step();

                    break;
                case Opcode.Or:

                    var orBytes1 = state.StackPop();
                    var orBytes2 = state.StackPop();
                    //check endianism
                    var orB1 = new BigInteger(orBytes1);
                    orB1 = orB1 | new BigInteger(orBytes2);

                    state.StackPush(orB1.ToByteArray());
                    state.Step();
                    break;
                case Opcode.Xor:
                    var xorBytes1 = state.StackPop();
                    var xorBytes2 = state.StackPop();
                    //check endianism
                    var xorB1 = new BigInteger(xorBytes1);
                    xorB1 = xorB1 ^ new BigInteger(xorBytes2);

                    state.StackPush(xorB1.ToByteArray());
                    state.Step();
                    break;
                case Opcode.Not:
                    var notBytes1 = state.StackPop();
                    //check endianism
                    var notB1 = new BigInteger(notBytes1);
                    state.StackPush((~notB1).ToByteArray());
                    break;
                case Opcode.Byte:
                    var byteBytes1 = state.StackPop();
                    var byteBytes2 = state.StackPop();

                    var pos = new BigInteger(byteBytes1);
                    var word = PadTo32Bytes(byteBytes2);

                    var result = pos < 32 ? new[] {word[(int) pos]} : new byte[0];

                    state.StackPush(result);
                    state.Step();
                    break;
                case Opcode.Sha3:
                    break;
                case Opcode.Address:
                    break;
                case Opcode.Balance:
                    break;
                case Opcode.Origin:
                    break;
                case Opcode.Caller:
                    break;
                case Opcode.Callvalue:
                    break;
                case Opcode.Calldataload:
                    break;
                case Opcode.Calldatasize:
                    break;
                case Opcode.Calldatacopy:
                    break;
                case Opcode.Codesize:
                    break;
                case Opcode.Codecopy:
                    break;
                case Opcode.Gasprice:
                    break;
                case Opcode.Extcodesize:
                    break;
                case Opcode.Extcodecopy:
                    break;
                case Opcode.Blockhash:
                    break;
                case Opcode.Coinbase:
                    break;
                case Opcode.Timestamp:
                    break;
                case Opcode.Number:
                    break;
                case Opcode.Difficulty:
                    break;
                case Opcode.Gaslimit:
                    break;
                case Opcode.Pop:
                    state.StackPop();
                    state.Step();
                    break;
                case Opcode.Mload:
                    break;
                case Opcode.Mstore:
                    break;
                case Opcode.Mstore8:
                    break;
                case Opcode.Sload:
                    break;
                case Opcode.Sstore:
                    break;
                case Opcode.Jump:
                    break;
                case Opcode.Jumpi:
                    break;
                case Opcode.Getpc:
                    break;
                case Opcode.Msize:
                    break;
                case Opcode.Gas:
                    break;
                case Opcode.Jumpdest:
                    break;
                case Opcode.Push1:
                case Opcode.Push2:
                case Opcode.Push3:
                case Opcode.Push4:
                case Opcode.Push5:
                case Opcode.Push6:
                case Opcode.Push7:
                case Opcode.Push8:
                case Opcode.Push9:
                case Opcode.Push10:
                case Opcode.Push11:
                case Opcode.Push12:
                case Opcode.Push13:
                case Opcode.Push14:
                case Opcode.Push15:
                case Opcode.Push16:
                case Opcode.Push17:
                case Opcode.Push18:
                case Opcode.Push19:
                case Opcode.Push20:
                case Opcode.Push21:
                case Opcode.Push22:
                case Opcode.Push23:
                case Opcode.Push24:
                case Opcode.Push25:
                case Opcode.Push26:
                case Opcode.Push27:
                case Opcode.Push28:
                case Opcode.Push29:
                case Opcode.Push30:
                case Opcode.Push31:
                case Opcode.Push32:
                    state.Step();
                    var pushNumber = opcode - (int) Opcode.Push1 + 1;
                    var data = state.Sweep(pushNumber);

                    state.StackPush(data);
                    break;
                case Opcode.Dup1:
                case Opcode.Dup2:
                case Opcode.Dup3:
                case Opcode.Dup4:
                case Opcode.Dup5:
                case Opcode.Dup6:
                case Opcode.Dup7:
                case Opcode.Dup8:
                case Opcode.Dup9:
                case Opcode.Dup10:
                case Opcode.Dup11:
                case Opcode.Dup12:
                case Opcode.Dup13:
                case Opcode.Dup14:
                case Opcode.Dup15:
                case Opcode.Dup16:
                    break;
                case Opcode.Swap1:
                case Opcode.Swap2:
                case Opcode.Swap3:
                case Opcode.Swap4:
                case Opcode.Swap5:
                case Opcode.Swap6:
                case Opcode.Swap7:
                case Opcode.Swap8:
                case Opcode.Swap9:
                case Opcode.Swap10:
                case Opcode.Swap11:
                case Opcode.Swap12:
                case Opcode.Swap13:
                case Opcode.Swap14:
                case Opcode.Swap15:
                case Opcode.Swap16:
                    break;
                case Opcode.Log0:
                    break;
                case Opcode.Log1:
                    break;
                case Opcode.Log2:
                    break;
                case Opcode.Log3:
                    break;
                case Opcode.Log4:
                    break;
                case Opcode.Create:
                    break;
                case Opcode.Call:
                    break;
                case Opcode.Callcode:
                    break;
                case Opcode.Return:
                    break;
                case Opcode.Delegatecall:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public byte[] PadTo32Bytes(byte[] bytesToPad)
        {
            var ret = new byte[32];

            for (var i = 0; i < ret.Length; i++)
                ret[i] = 0;
            Array.Copy(bytesToPad, 0, ret, 32 - bytesToPad.Length, bytesToPad.Length);

            return ret;
        }


        public OpcodeDesc GetInstructionInfo(byte opcode)
        {
            return OpcodeDesc.Opcodes[(Opcode) opcode];
        }
    }
}