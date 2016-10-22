using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace z100emu.CPU
{
    public static class InstructionHelper
    {
        #region lookup tables
        public static readonly bool[] ParityLookup =
                {
            true, false, false, true, false, true, true, false, false, true, true, false, true, false, false, true,
            false, true, true, false, true, false, false, true, true, false, false, true, false, true, true, false,
            false, true, true, false, true, false, false, true, true, false, false, true, false, true, true, false,
            true, false, false, true, false, true, true, false, false, true, true, false, true, false, false, true,
            false, true, true, false, true, false, false, true, true, false, false, true, false, true, true, false,
            true, false, false, true, false, true, true, false, false, true, true, false, true, false, false, true,
            true, false, false, true, false, true, true, false, false, true, true, false, true, false, false, true,
            false, true, true, false, true, false, false, true, true, false, false, true, false, true, true, false,
            false, true, true, false, true, false, false, true, true, false, false, true, false, true, true, false,
            true, false, false, true, false, true, true, false, false, true, true, false, true, false, false, true,
            true, false, false, true, false, true, true, false, false, true, true, false, true, false, false, true,
            false, true, true, false, true, false, false, true, true, false, false, true, false, true, true, false,
            true, false, false, true, false, true, true, false, false, true, true, false, true, false, false, true,
            false, true, true, false, true, false, false, true, true, false, false, true, false, true, true, false,
            false, true, true, false, true, false, false, true, true, false, false, true, false, true, true, false,
            true, false, false, true, false, true, true, false, false, true, true, false, true, false, false, true
        };
        #endregion

        public static ushort GetInstructionValue(Cpu8086 cpu, OpCodeManager.OpCodeFlag flag, Register segmentPrefix, int instruction, int instructionValue, int instructionDisplacement)
        {
            switch (instruction)
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
                    return cpu.GetRegister((Register)instruction);

                case OpCodeManager.ARG_BYTE_REGISTER:
                    return cpu.GetRegisterU8((Register)instructionValue);

                case OpCodeManager.ARG_DEREFERENCE:
                case OpCodeManager.ARG_MEMORY:
                    var address = GetInstructionRealAddress(cpu, segmentPrefix, instruction, instructionValue, instructionDisplacement);
                    return flag.Has(OpCodeManager.OpCodeFlag.Size8) ? cpu.ReadU8(address) : cpu.ReadU16(address);

                case OpCodeManager.ARG_CONSTANT:
                    return (ushort)instructionValue;

                default:
                    throw new NotImplementedException();
            }
        }

        public static uint GetInstructionRealAddress(Cpu8086 cpu, Register segmentPrefix, int instruction, int instructionValue, int instructionDisplacement)
        {
            switch (instruction)
            {
                case OpCodeManager.ARG_DEREFERENCE:
                    if (segmentPrefix == Register.Invalid)
                    {
                        segmentPrefix = instructionValue == 6 ? Register.SS : Register.DS; // BP needs SS segment
                    }
                    var addr = GetInstructionAddress(cpu, instruction, instructionValue, instructionDisplacement);
                    return SegmentToAddress(cpu.GetRegister(segmentPrefix), addr);
                case OpCodeManager.ARG_MEMORY:
                    if (segmentPrefix == Register.Invalid)
                    {
                        segmentPrefix = Register.DS;
                    }
                    var address = GetInstructionAddress(cpu, instruction, instructionValue, instructionDisplacement);
                    return SegmentToAddress(cpu.GetRegister(segmentPrefix), address);

                default:
                    throw new NotImplementedException();
            }
        }

        public static ushort GetInstructionAddress(Cpu8086 cpu, int instruction, int instructionValue, int instructionDisplacement)
        {
            switch (instruction)
            {
                case OpCodeManager.ARG_DEREFERENCE:
                    ushort address;
                    switch (instructionValue)
                    {
                        case 0:
                            address = (ushort)(cpu.GetRegister(Register.BX) + cpu.GetRegister(Register.SI));
                            break;
                        case 1:
                            address = (ushort)(cpu.GetRegister(Register.BX) + cpu.GetRegister(Register.DI));
                            break;
                        case 2:
                            address = (ushort)(cpu.GetRegister(Register.BP) + cpu.GetRegister(Register.SI));
                            break;
                        case 3:
                            address = (ushort)(cpu.GetRegister(Register.BP) + cpu.GetRegister(Register.DI));
                            break;
                        case 4:
                            address = cpu.GetRegister(Register.SI);
                            break;
                        case 5:
                            address = cpu.GetRegister(Register.DI);
                            break;
                        case 6:
                            address = cpu.GetRegister(Register.BP);
                            break;
                        case 7:
                            address = cpu.GetRegister(Register.BX);
                            break;
                        default:
                            throw new NotImplementedException();
                    }
                    return (ushort)(address + instructionDisplacement);

                case OpCodeManager.ARG_MEMORY:
                    return (ushort)instructionValue;

                default:
                    throw new NotImplementedException();
            }
        }

        public static uint SegmentToAddress(ushort segment, ushort offset) => (uint)((segment << 4) + offset);

        public static  void CalculateIncFlags(Cpu8086 cpu, OpCodeManager.OpCodeFlag flag, ushort value1, ushort value2, int result)
        {
            if (flag.Has(OpCodeManager.OpCodeFlag.Size8))
                CalculateIncFlags8Bit(cpu, (byte)value1, (byte)value2, result);
            else CalculateIncFlags16Bit(cpu, value1, value2, result);
        }
        public static void CalculateDecFlags(Cpu8086 cpu, OpCodeManager.OpCodeFlag flag, ushort value1, ushort value2, int result)
        {
            if (flag.Has(OpCodeManager.OpCodeFlag.Size8))
                CalculateDecFlags8Bit(cpu, (byte)value1, (byte)value2, result);
            else CalculateDecFlags16Bit(cpu, value1, value2, result);
        }
        public static void CalculateBitwiseFlags(Cpu8086 cpu, OpCodeManager.OpCodeFlag flag, ushort value1, ushort value2, int result)
        {
            if (flag.Has(OpCodeManager.OpCodeFlag.Size8))
                CalculateBitwiseFlags8Bit(cpu, (byte)value1, (byte)value2, result);
            else CalculateBitwiseFlags16Bit(cpu, value1, value2, result);
        }
        public static void CalculateAddFlags(Cpu8086 cpu, OpCodeManager.OpCodeFlag flag, ushort value1, ushort value2, int result)
        {
            if (flag.Has(OpCodeManager.OpCodeFlag.Size8))
                CalculateAddFlags8Bit(cpu, (byte)value1, (byte)value2, result);
            else CalculateAddFlags16Bit(cpu, value1, value2, result);
        }
        public static void CalculateSubFlags(Cpu8086 cpu, OpCodeManager.OpCodeFlag flag, ushort value1, ushort value2, int result)
        {
            if (flag.Has(OpCodeManager.OpCodeFlag.Size8))
                CalculateSubFlags8Bit(cpu, (byte)value1, (byte)value2, result);
            else CalculateSubFlags16Bit(cpu, value1, value2, result);
        }

        public static void CalculateIncFlags8Bit(Cpu8086 cpu, byte value1, byte value2, int result)
        {
            var truncResult = (byte)result;
            var sign = ((truncResult >> 7) & 1) == 1;
            var overflow = ((truncResult ^ value1) & (truncResult ^ value2) & 0x80) == 0x80;
            var auxiliary = ((value1 ^ value2 ^ truncResult) & 0x10) != 0;
            var zero = truncResult == 0;
            var parity = ParityLookup[(byte)(result & 0xFF)];

            cpu.Flags &= ~(FlagsRegister.Parity | FlagsRegister.Auxiliary | FlagsRegister.Zero | FlagsRegister.Sign | FlagsRegister.Overflow);
            cpu.Flags |= (parity ? FlagsRegister.Parity : 0) |
                     (auxiliary ? FlagsRegister.Auxiliary : 0) |
                     (zero ? FlagsRegister.Zero : 0) |
                     (sign ? FlagsRegister.Sign : 0) |
                     (overflow ? FlagsRegister.Overflow : 0);
        }
        public static void CalculateIncFlags16Bit(Cpu8086 cpu, ushort value1, ushort value2, int result)
        {
            var truncResult = (ushort)result;
            var sign = ((truncResult >> 15) & 1) == 1;
            var overflow = ((truncResult ^ value1) & (truncResult ^ value2) & 0x8000) == 0x8000;
            var auxiliary = ((value1 ^ value2 ^ truncResult) & 0x10) != 0;
            var zero = truncResult == 0;
            var parity = ParityLookup[(byte)(result & 0xFF)];

            cpu.Flags &= ~(FlagsRegister.Parity | FlagsRegister.Auxiliary | FlagsRegister.Zero | FlagsRegister.Sign | FlagsRegister.Overflow);
            cpu.Flags |= (parity ? FlagsRegister.Parity : 0) |
                     (auxiliary ? FlagsRegister.Auxiliary : 0) |
                     (zero ? FlagsRegister.Zero : 0) |
                     (sign ? FlagsRegister.Sign : 0) |
                     (overflow ? FlagsRegister.Overflow : 0);
        }
        public static void CalculateDecFlags8Bit(Cpu8086 cpu, byte value1, byte value2, int result)
        {
            var truncResult = (byte)result;
            var sign = ((truncResult >> 7) & 1) == 1;
            var overflow = ((truncResult ^ value1) & (value1 ^ value2) & 0x80) == 0x80;
            var auxiliary = ((value1 ^ value2 ^ truncResult) & 0x10) != 0;
            var zero = truncResult == 0;
            var parity = ParityLookup[(byte)(result & 0xFF)];

            cpu.Flags &= ~(FlagsRegister.Parity | FlagsRegister.Auxiliary | FlagsRegister.Zero | FlagsRegister.Sign | FlagsRegister.Overflow);
            cpu.Flags |= (parity ? FlagsRegister.Parity : 0) |
                     (auxiliary ? FlagsRegister.Auxiliary : 0) |
                     (zero ? FlagsRegister.Zero : 0) |
                     (sign ? FlagsRegister.Sign : 0) |
                     (overflow ? FlagsRegister.Overflow : 0);
        }
        public static void CalculateDecFlags16Bit(Cpu8086 cpu, ushort value1, ushort value2, int result)
        {
            var truncResult = (ushort)result;
            var sign = ((truncResult >> 15) & 1) == 1;
            var overflow = ((truncResult ^ value1) & (value1 ^ value2) & 0x8000) == 0x8000;
            var auxiliary = ((value1 ^ value2 ^ truncResult) & 0x10) != 0;
            var zero = truncResult == 0;
            var parity = ParityLookup[(byte)(result & 0xFF)];

            cpu.Flags &= ~(FlagsRegister.Parity | FlagsRegister.Auxiliary | FlagsRegister.Zero | FlagsRegister.Sign | FlagsRegister.Overflow);
            cpu.Flags |= (parity ? FlagsRegister.Parity : 0) |
                     (auxiliary ? FlagsRegister.Auxiliary : 0) |
                     (zero ? FlagsRegister.Zero : 0) |
                     (sign ? FlagsRegister.Sign : 0) |
                     (overflow ? FlagsRegister.Overflow : 0);
        }
        public static void CalculateBitwiseFlags8Bit(Cpu8086 cpu, byte value1, byte value2, int result)
        {
            var truncResult = (byte)result;
            var sign = ((truncResult >> 7) & 1) == 1;
            var zero = truncResult == 0;
            var parity = ParityLookup[(byte)(result & 0xFF)];

            cpu.Flags &= ~(FlagsRegister.Carry | FlagsRegister.Parity | FlagsRegister.Zero | FlagsRegister.Sign | FlagsRegister.Overflow);
            cpu.Flags |= (parity ? FlagsRegister.Parity : 0) |
                     (zero ? FlagsRegister.Zero : 0) |
                     (sign ? FlagsRegister.Sign : 0);
        }
        public static void CalculateBitwiseFlags16Bit(Cpu8086 cpu, ushort value1, ushort value2, int result)
        {
            var truncResult = (ushort)result;
            var sign = ((truncResult >> 15) & 1) == 1;
            var zero = truncResult == 0;
            var parity = ParityLookup[(byte)(result & 0xFF)];

            cpu.Flags &= ~(FlagsRegister.Carry | FlagsRegister.Parity | FlagsRegister.Zero | FlagsRegister.Sign | FlagsRegister.Overflow);
            cpu.Flags |= (parity ? FlagsRegister.Parity : 0) |
                     (zero ? FlagsRegister.Zero : 0) |
                     (sign ? FlagsRegister.Sign : 0);
        }
        public static void CalculateAddFlags8Bit(Cpu8086 cpu, byte value1, byte value2, int result)
        {
            var truncResult = (byte)result;
            var carry = (uint)result > 0xFF;
            var sign = ((truncResult >> 7) & 1) == 1;
            var overflow = ((truncResult ^ value1) & (truncResult ^ value2) & 0x80) == 0x80;
            var auxiliary = ((value1 ^ value2 ^ truncResult) & 0x10) != 0;
            var zero = truncResult == 0;
            var parity = ParityLookup[(byte)(result & 0xFF)];

            cpu.Flags &= ~(FlagsRegister.Carry | FlagsRegister.Parity | FlagsRegister.Auxiliary | FlagsRegister.Zero | FlagsRegister.Sign | FlagsRegister.Overflow);
            cpu.Flags |= (carry ? FlagsRegister.Carry : 0) |
                     (parity ? FlagsRegister.Parity : 0) |
                     (auxiliary ? FlagsRegister.Auxiliary : 0) |
                     (zero ? FlagsRegister.Zero : 0) |
                     (sign ? FlagsRegister.Sign : 0) |
                     (overflow ? FlagsRegister.Overflow : 0);
        }
        public static void CalculateAddFlags16Bit(Cpu8086 cpu, ushort value1, ushort value2, int result)
        {
            var truncResult = (ushort)result;
            var carry = (uint)result > 0xFFFF;
            var sign = ((truncResult >> 15) & 1) == 1;
            var overflow = ((truncResult ^ value1) & (truncResult ^ value2) & 0x8000) == 0x8000;
            var auxiliary = ((value1 ^ value2 ^ truncResult) & 0x10) != 0;
            var zero = truncResult == 0;
            var parity = ParityLookup[(byte)(result & 0xFF)];

            cpu.Flags &= ~(FlagsRegister.Carry | FlagsRegister.Parity | FlagsRegister.Auxiliary | FlagsRegister.Zero | FlagsRegister.Sign | FlagsRegister.Overflow);
            cpu.Flags |= (carry ? FlagsRegister.Carry : 0) |
                     (parity ? FlagsRegister.Parity : 0) |
                     (auxiliary ? FlagsRegister.Auxiliary : 0) |
                     (zero ? FlagsRegister.Zero : 0) |
                     (sign ? FlagsRegister.Sign : 0) |
                     (overflow ? FlagsRegister.Overflow : 0);
        }
        public static void CalculateSubFlags8Bit(Cpu8086 cpu, byte value1, byte value2, int result)
        {
            var truncResult = (byte)result;
            var carry = (uint)result > 0xFF;
            var sign = ((truncResult >> 7) & 1) == 1;
            var overflow = ((truncResult ^ value1) & (value1 ^ value2) & 0x80) == 0x80;
            var auxiliary = ((value1 ^ value2 ^ truncResult) & 0x10) != 0;
            var zero = truncResult == 0;
            var parity = ParityLookup[(byte)(result & 0xFF)];

            cpu.Flags &= ~(FlagsRegister.Carry | FlagsRegister.Parity | FlagsRegister.Auxiliary | FlagsRegister.Zero | FlagsRegister.Sign | FlagsRegister.Overflow);
            cpu.Flags |= (carry ? FlagsRegister.Carry : 0) |
                     (parity ? FlagsRegister.Parity : 0) |
                     (auxiliary ? FlagsRegister.Auxiliary : 0) |
                     (zero ? FlagsRegister.Zero : 0) |
                     (sign ? FlagsRegister.Sign : 0) |
                     (overflow ? FlagsRegister.Overflow : 0);
        }
        public static void CalculateSubFlags16Bit(Cpu8086 cpu, ushort value1, ushort value2, int result)
        {
            var truncResult = (ushort)result;
            var carry = (uint)result > 0xFFFF;
            var sign = ((truncResult >> 15) & 1) == 1;
            var overflow = ((truncResult ^ value1) & (value1 ^ value2) & 0x8000) == 0x8000;
            var auxiliary = ((value1 ^ value2 ^ truncResult) & 0x10) != 0;
            var zero = truncResult == 0;
            var parity = ParityLookup[(byte)(result & 0xFF)];

            cpu.Flags &= ~(FlagsRegister.Carry | FlagsRegister.Parity | FlagsRegister.Auxiliary | FlagsRegister.Zero | FlagsRegister.Sign | FlagsRegister.Overflow);
            cpu.Flags |= (carry ? FlagsRegister.Carry : 0) |
                     (parity ? FlagsRegister.Parity : 0) |
                     (auxiliary ? FlagsRegister.Auxiliary : 0) |
                     (zero ? FlagsRegister.Zero : 0) |
                     (sign ? FlagsRegister.Sign : 0) |
                     (overflow ? FlagsRegister.Overflow : 0);
        }
    }
}
