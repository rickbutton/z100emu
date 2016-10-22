using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace z100emu.CPU.Instructions
{
    public class Arithmetic : IInstruction
    {
        public void Dispatch(Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            var value1 = InstructionHelper.GetInstructionValue(cpu, instruction.Flag, instruction.SegmentPrefix, instruction.Argument1, instruction.Argument1Value, instruction.Argument1Displacement);
            var value2 = InstructionHelper.GetInstructionValue(cpu, instruction.Flag, instruction.SegmentPrefix, instruction.Argument2, instruction.Argument2Value, instruction.Argument2Displacement);

            if (instruction.Flag.Has(OpCodeManager.OpCodeFlag.Size8))
            {
                value1 &= 0xFF;
                value2 &= 0xFF;
            }

            int result;
            bool carry;
            switch (instruction.Type)
            {
                case OpCodeManager.InstructionType.Adc:
                    result = value1 + value2 + (cpu.GetFlags().Has(FlagsRegister.Carry) ? 1 : 0);
                    InstructionHelper.CalculateAddFlags(cpu, instruction.Flag, value1, value2, result);
                    break;

                case OpCodeManager.InstructionType.Add:
                    result = value1 + value2;
                    InstructionHelper.CalculateAddFlags(cpu, instruction.Flag, value1, value2, result);
                    break;

                case OpCodeManager.InstructionType.And:
                case OpCodeManager.InstructionType.Test:
                    result = value1 & value2;
                    InstructionHelper.CalculateBitwiseFlags(cpu, instruction.Flag, value1, value2, result);
                    break;

                case OpCodeManager.InstructionType.Compare:
                case OpCodeManager.InstructionType.Subtract:
                    result = value1 - value2;
                    InstructionHelper.CalculateSubFlags(cpu, instruction.Flag, value1, value2, result);
                    break;

                case OpCodeManager.InstructionType.Or:
                    result = value1 | value2;
                    InstructionHelper.CalculateBitwiseFlags(cpu, instruction.Flag, value1, value2, result);
                    break;

                case OpCodeManager.InstructionType.Rcl:
                    if (instruction.Flag.Has(OpCodeManager.OpCodeFlag.Size8))
                    {
                        const int mask = 0x1FF;
                        var shift = (value2 & 0x1F) % 9;

                        result = (byte)value1;
                        if (cpu.Flags.Has(FlagsRegister.Carry))
                            result |= 0x100;
                        result = (byte)(result << shift) | (byte)(result >> (-shift & mask));
                        if ((result & 0x100) != 0)
                            cpu.Flags |= FlagsRegister.Carry;
                        else cpu.Flags &= ~FlagsRegister.Carry;

                        if (value2 == 1)
                        {
                            if (((result & 0x100) != 0) ^ ((result & 0x80) != 0))
                                cpu.Flags |= FlagsRegister.Overflow;
                            else cpu.Flags &= ~FlagsRegister.Overflow;
                        }
                    }
                    else
                    {
                        const int mask = 0x1FFFF;
                        var shift = (value2 & 0x1F) % 17;

                        result = value1;
                        if (cpu.Flags.Has(FlagsRegister.Carry))
                            result |= 0x10000;
                        result = (ushort)(result << shift) | (ushort)(result >> (-shift & mask));
                        if ((result & 0x10000) != 0)
                            cpu.Flags |= FlagsRegister.Carry;
                        else cpu.Flags &= ~FlagsRegister.Carry;

                        if (value2 == 1)
                        {
                            if (((result & 0x10000) != 0) ^ ((result & 0x8000) != 0))
                                cpu.Flags |= FlagsRegister.Overflow;
                            else cpu.Flags &= ~FlagsRegister.Overflow;
                        }
                    }
                    InstructionHelper.CalculateBitwiseFlags(cpu, instruction.Flag, value1, value2, result);
                    break;

                case OpCodeManager.InstructionType.Rcr:
                    if (instruction.Flag.Has(OpCodeManager.OpCodeFlag.Size8))
                    {
                        const int mask = 0x1FF;
                        var shift = (value2 & 0x1F) % 9;

                        result = (byte)value1;
                        if (cpu.Flags.Has(FlagsRegister.Carry))
                            result |= 0x100;

                        if ((result & 0x1) != 0)
                            cpu.Flags |= FlagsRegister.Carry;
                        else cpu.Flags &= ~FlagsRegister.Carry;

                        result = (byte)(result >> shift) | (byte)(result << (-shift & mask));
                        
                        if (value2 == 1)
                        {
                            if (((result & 0x80) != 0) ^ ((result & 0x40) != 0))
                                cpu.Flags |= FlagsRegister.Overflow;
                            else cpu.Flags &= ~FlagsRegister.Overflow;
                        }
                    }
                    else
                    {
                        const int mask = 0x1FFFF;
                        var shift = (value2 & 0x1F) % 17;

                        result = value1;
                        if (cpu.Flags.Has(FlagsRegister.Carry))
                            result |= 0x10000;

                        if ((result & 0x1) != 0)
                            cpu.Flags |= FlagsRegister.Carry;
                        else cpu.Flags &= ~FlagsRegister.Carry;

                        result = (ushort)(result >> shift) | (ushort)(result << (-shift & mask));

                        if (value2 == 1)
                        {
                            if (((result & 0x8000) != 0) ^ ((result & 0x4000) != 0))
                                cpu.Flags |= FlagsRegister.Overflow;
                            else cpu.Flags &= ~FlagsRegister.Overflow;
                        }
                    }
                    //cpu.CalculateBitwiseFlags(instruction.Flag, value1, value2, result);
                    break;

                case OpCodeManager.InstructionType.Rol:
                    if (instruction.Flag.Has(OpCodeManager.OpCodeFlag.Size8))
                    {
                        const int mask = 0xFF;
                        var shift = value2 & mask;
                        result = (byte)(value1 << shift) | (byte)(value1 >> (-shift & mask));
                    }
                    else
                    {
                        const int mask = 0xFFFF;
                        var shift = value2 & mask;
                        result = (ushort)(value1 << shift) | (ushort)(value1 >> (-shift & mask));
                    }
                    InstructionHelper.CalculateBitwiseFlags(cpu, instruction.Flag, value1, value2, result);
                    break;

                case OpCodeManager.InstructionType.Ror:
                    if (instruction.Flag.Has(OpCodeManager.OpCodeFlag.Size8))
                    {
                        const int mask = 0xFF;
                        var shift = value2 & mask;
                        result = (byte)(value1 >> shift) | (byte)(value1 << (-shift & mask));
                    }
                    else
                    {
                        const int mask = 0xFFFF;
                        var shift = value2 & mask;
                        result = (ushort)(value1 >> shift) | (ushort)(value1 << (-shift & mask));
                    }
                    InstructionHelper.CalculateBitwiseFlags(cpu, instruction.Flag, value1, value2, result);
                    break;

                case OpCodeManager.InstructionType.Sbb:
                    result = value1 - (value2 + (cpu.GetFlags().Has(FlagsRegister.Carry) ? 1 : 0));
                    InstructionHelper.CalculateSubFlags(cpu, instruction.Flag, value1, value2, result);
                    break;

                case OpCodeManager.InstructionType.Shl:
                    bool overflow;
                    result = value1 << (value2 & 0x1F);
                    InstructionHelper.CalculateBitwiseFlags(cpu, instruction.Flag, value1, (ushort)(value2 & 0x1F), result);
                    if (instruction.Flag.Has(OpCodeManager.OpCodeFlag.Size8))
                    {
                        carry = (result & 0x100) != 0;
                        overflow = (result & 0x80) != 0;
                    }
                    else
                    {
                        carry = (result & 0x10000) != 0;
                        overflow = (result & 0x8000) != 0;
                    }
                    if (carry)
                        cpu.Flags |= FlagsRegister.Carry;
                    if ((value2 & 0x1F) == 1 && overflow ^ carry)
                        cpu.Flags |= FlagsRegister.Overflow;
                    break;

                case OpCodeManager.InstructionType.Sar:
                    if (instruction.Flag.Has(OpCodeManager.OpCodeFlag.Size8))
                    {
                        carry = (((sbyte)value1 >> ((value2 & 0x1F) - 1)) & 1) != 0;
                        result = (sbyte)value1 >> (value2 & 0x1F);
                    }
                    else
                    {
                        carry = (((short)value1 >> ((value2 & 0x1F) - 1)) & 1) != 0;
                        result = (short)value1 >> (value2 & 0x1F);
                    }
                    InstructionHelper.CalculateBitwiseFlags(cpu, instruction.Flag, value1, (ushort)(value2 & 0x1F), result);
                    if (carry)
                        cpu.Flags |= FlagsRegister.Carry;
                    break;

                case OpCodeManager.InstructionType.Shr:
                    carry = ((value1 >> ((value2 & 0x1F) - 1)) & 1) != 0;
                    result = value1 >> (value2 & 0x1F);
                    InstructionHelper.CalculateBitwiseFlags(cpu, instruction.Flag, value1, (ushort)(value2 & 0x1F), result);
                    if (carry)
                        cpu.Flags |= FlagsRegister.Carry;
                    if ((value2 & 0x1F) == 1)
                    {
                        if (instruction.Flag.Has(OpCodeManager.OpCodeFlag.Size8))
                        {
                            if ((value1 & 0x80) != 0)
                                cpu.Flags |= FlagsRegister.Overflow;
                        }
                        else
                        {
                            if ((value1 & 0x8000) != 0)
                                cpu.Flags |= FlagsRegister.Overflow;
                        }
                    }
                    break;

                case OpCodeManager.InstructionType.Xor:
                    result = value1 ^ value2;
                    InstructionHelper.CalculateBitwiseFlags(cpu, instruction.Flag, value1, value2, result);
                    break;

                default:
                    throw new NotImplementedException();
            }

            var truncResult = instruction.Flag.Has(OpCodeManager.OpCodeFlag.Size8) ? (byte)result : (ushort)result;

            if (instruction.Type != OpCodeManager.InstructionType.Compare && instruction.Type != OpCodeManager.InstructionType.Test)
            {
                switch (instruction.Argument1)
                {
                    case (int)Register.AX:
                    case (int)Register.CX:
                    case (int)Register.DX:
                    case (int)Register.BX:
                    case (int)Register.SP:
                    case (int)Register.BP:
                    case (int)Register.SI:
                    case (int)Register.DI:
                    case (int)Register.IP:
                    case (int)Register.CS:
                    case (int)Register.DS:
                    case (int)Register.ES:
                    case (int)Register.SS:
                        cpu.SetRegister((Register)instruction.Argument1, truncResult);
                        break;

                    case OpCodeManager.ARG_BYTE_REGISTER:
                        cpu.SetRegisterU8((Register)instruction.Argument1Value, (byte)truncResult);
                        break;

                    case OpCodeManager.ARG_DEREFERENCE:
                    case OpCodeManager.ARG_MEMORY:
                        var address = InstructionHelper.GetInstructionRealAddress(cpu, instruction.SegmentPrefix, instruction.Argument1, instruction.Argument1Value, instruction.Argument1Displacement);
                        if (instruction.Flag.Has(OpCodeManager.OpCodeFlag.Size8))
                            cpu.WriteU8(address, (byte)truncResult);
                        else cpu.WriteU16(address, truncResult);
                        break;

                    default:
                        throw new NotImplementedException();
                }
            }
        }
    }
}
