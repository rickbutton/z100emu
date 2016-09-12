#region License
// The MIT License (MIT)
// 
// Copyright (c) 2016 Rick Button
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
#endregion

using System;
using System.Diagnostics;
using JetBrains.Annotations;
using z100emu.Core;
using z100emu.Peripheral;
using z100emu.Ram;

namespace z100emu.CPU
{
    [PublicAPI]
    public sealed class Cpu8086 : ICpu, IInstructionFetcher
    {
        public enum Register : uint
        {
            AX = 0,
            CX = 1,
            DX = 2,
            BX = 3,

            SP = 4,
            BP = 5,
            SI = 6,
            DI = 7,

            ES = 8,
            CS = 9,
            SS = 10,
            DS = 11,

            IP = 12,

            AL = 0x80000000 | 0,
            CL = 0x80000000 | 1,
            DL = 0x80000000 | 2,
            BL = 0x80000000 | 3,
            AH = 0x80000000 | 4,
            CH = 0x80000000 | 5,
            DH = 0x80000000 | 6,
            BH = 0x80000000 | 7,

            FLAGS = 0x80000000 | 0xFF,

            Invalid = 0xFFFFFFFF
        }

        [Flags]
        public enum FlagsRegister : ushort
        {
            Carry = 1 << 0,
            Parity = 1 << 2,
            Auxiliary = 1 << 4,
            Zero = 1 << 6,
            Sign = 1 << 7,
            Trap = 1 << 8,
            Interrupt = 1 << 9,
            Direction = 1 << 10,
            Overflow = 1 << 11
        }

        private delegate void InstructionDispatch([NotNull] Cpu8086 cpu, OpCodeManager.Instruction instruction);

        private static readonly bool[] parityLookup =
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

        private static readonly InstructionDispatch[] dispatches =
        {
            DispatchInvalid,

            DispatchArithmetic,
            DispatchPush,
            DispatchPop,
            DispatchArithmetic,
            DispatchArithmetic,
            DispatchArithmetic,
            DispatchArithmetic,
            DispatchArithmetic,
            DispatchArithmetic,
            DispatchArithmetic,
            DispatchInvalid,
            DispatchDaa,
            DispatchDas,
            DispatchAaa,
            DispatchAas,
            DispatchUnaryArithmetic,
            DispatchUnaryArithmetic,
            DispatchJumpRelative,
            DispatchJump,
            DispatchFarJump,
            DispatchInvalid,
            DispatchArithmetic,
            DispatchExchange,
            DispatchMove,
            DispatchLea,
            DispatchCbw,
            DispatchCwd,
            DispatchCallNearRelative,
            DispatchCallNear,
            DispatchCallFar,
            DispatchWait,
            DispatchSahf,
            DispatchLahf,
            DispatchStringOperation,
            DispatchStringOperation,
            DispatchStringOperation,
            DispatchStringOperation,
            DispatchStringOperation,
            DispatchReturnNear,
            DispatchReturnFar,
            (cpu, instruction) => DispatchLoadFarPointer(cpu, instruction, Register.ES),
            (cpu, instruction) => DispatchLoadFarPointer(cpu, instruction, Register.DS),
            DispatchInterrupt,
            DispatchInto,
            DispatchReturnInterrupt,
            DispatchAam,
            DispatchAad,
            DispatchXlat,
            DispatchLoopNotZero,
            DispatchLoopZero,
            DispatchLoop,
            DispatchClc,
            DispatchStc,
            DispatchJcxz,
            DispatchIn,
            DispatchOut,
            DispatchHalt,
            DispatchCmc,
            DispatchCli,
            DispatchSti,
            DispatchCld,
            DispatchStd,
            DispatchJumpIfOverflow,
            DispatchJumpIfNotOverflow,
            DispatchJumpIfCarry,
            DispatchJumpIfNotCarry,
            DispatchJumpIfZero,
            DispatchJumpIfNotZero,
            DispatchJBE,
            DispatchJA,
            DispatchJS,
            DispatchJNS,
            DispatchJPE,
            DispatchJPO,
            DispatchJL,
            DispatchJGE,
            DispatchJLE,
            DispatchJG,
            DispatchArithmetic,
            DispatchArithmetic,
            DispatchArithmetic,
            DispatchArithmetic,
            DispatchArithmetic,
            DispatchArithmetic,
            DispatchArithmetic,
            DispatchUnaryArithmetic,
            DispatchUnaryArithmetic,
            DispatchMultiply,
            DispatchMultiply,
            DispatchDivide,
            DispatchSignedDivide,
            //DispatchEmulatorSpecial
        };

        private const FlagsRegister FLAGS_MASK = FlagsRegister.Carry |
                                                 FlagsRegister.Parity |
                                                 FlagsRegister.Auxiliary |
                                                 FlagsRegister.Zero |
                                                 FlagsRegister.Sign |
                                                 FlagsRegister.Trap |
                                                 FlagsRegister.Interrupt |
                                                 FlagsRegister.Direction |
                                                 FlagsRegister.Overflow;

        [NotNull] private readonly ushort[] registers = new ushort[13];
        private DateTime lastBiosTime = DateTime.Now;
        private FlagsRegister flags;

        private IRam _ram;
        private IPortDevice[] _portDevices;

        private Intel8259 _pic;

        public Cpu8086(IRam ram, Intel8259 pic)
        {
            _ram = ram;
            _portDevices = new IPortDevice[256];
            _pic = pic;

            SetRegister(Register.CS, 0xF000);
            SetRegister(Register.IP, 0xFFF0);
        }

        public int ProcessSingleInstruction(bool debug = false)
        {
            var i = _pic.GetNextInterrupt();
            if (i != null)
            {
                Interrupt((byte)i.Value);
            }

            var instruction = OpCodeManager.Decode(this);

            if (debug)
            {
                string instructionText = $"{GetRegister(Register.CS):X4}:{GetRegister(Register.IP):X4} ";
                instructionText += OutputInstruction(instruction);
                Console.Out.WriteLine(instructionText);
            }

            dispatches[(int)instruction.Type](this, instruction);

            return instruction.Clocks;
        }

        private ushort ProcessExchangeSecond(OpCodeManager.Instruction instruction, ushort value)
        {
            ushort tmp;
            switch (instruction.Argument2)
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
                    tmp = registers[instruction.Argument2];
                    registers[instruction.Argument2] = value;
                    break;

                case OpCodeManager.ARG_BYTE_REGISTER:
                    tmp = GetRegisterU8((Register)instruction.Argument2Value);
                    SetRegisterU8((Register)instruction.Argument2Value, (byte)value);
                    break;

                case OpCodeManager.ARG_DEREFERENCE:
                case OpCodeManager.ARG_MEMORY:
                    var address = GetInstructionRealAddress(instruction.SegmentPrefix, instruction.Argument2, instruction.Argument2Value, instruction.Argument2Displacement);
                    tmp = ReadU16(address);
                    WriteU16(address, value);
                    break;

                default:
                    throw new NotImplementedException();
            }
            return tmp;
        }
        private void Push(ushort value)
        {
            registers[(int)Register.SP] -= 2;
            WriteU16(SegmentToAddress(GetRegister(Register.SS), GetRegister(Register.SP)), value);
        }
        private ushort Pop()
        {
            var value = ReadU16(SegmentToAddress(GetRegister(Register.SS), GetRegister(Register.SP)));
            registers[(int)Register.SP] += 2;
            return value;
        }
        private uint GetInstructionRealAddress(Register segmentPrefix, int instruction, int instructionValue, int instructionDisplacement)
        {
            switch (instruction)
            {
                case OpCodeManager.ARG_DEREFERENCE:
                case OpCodeManager.ARG_MEMORY:
                    if (segmentPrefix == Register.Invalid)
                    {
                        segmentPrefix = instructionValue == 6 ? Register.SS : Register.DS; // BP needs SS segment
                    }
                    var address = GetInstructionAddress(instruction, instructionValue, instructionDisplacement);
                    return SegmentToAddress(GetRegister(segmentPrefix), address);

                default:
                    throw new NotImplementedException();
            }
        }
        private ushort GetInstructionAddress(int instruction, int instructionValue, int instructionDisplacement)
        {
            switch (instruction)
            {
                case OpCodeManager.ARG_DEREFERENCE:
                    ushort address;
                    switch (instructionValue)
                    {
                        case 0:
                            address = (ushort)(GetRegister(Register.BX) + GetRegister(Register.SI));
                            break;
                        case 1:
                            address = (ushort)(GetRegister(Register.BX) + GetRegister(Register.DI));
                            break;
                        case 2:
                            address = (ushort)(GetRegister(Register.BP) + GetRegister(Register.SI));
                            break;
                        case 3:
                            address = (ushort)(GetRegister(Register.BP) + GetRegister(Register.DI));
                            break;
                        case 4:
                            address = GetRegister(Register.SI);
                            break;
                        case 5:
                            address = GetRegister(Register.DI);
                            break;
                        case 6:
                            address = GetRegister(Register.BP);
                            break;
                        case 7:
                            address = GetRegister(Register.BX);
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
        private ushort GetInstructionValue(OpCodeManager.OpCodeFlag flag, Register segmentPrefix, int instruction, int instructionValue, int instructionDisplacement)
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
                    return GetRegister((Register)instruction);

                case OpCodeManager.ARG_BYTE_REGISTER:
                    return GetRegisterU8((Register)instructionValue);

                case OpCodeManager.ARG_DEREFERENCE:
                case OpCodeManager.ARG_MEMORY:
                    var address = GetInstructionRealAddress(segmentPrefix, instruction, instructionValue, instructionDisplacement);
                    return flag.Has(OpCodeManager.OpCodeFlag.Size8) ? ReadU8(address) : ReadU16(address);

                case OpCodeManager.ARG_CONSTANT:
                    return (ushort)instructionValue;

                default:
                    throw new NotImplementedException();
            }
        }

        public void Interrupt(byte interrupt)
        {
            if (!flags.Has(FlagsRegister.Interrupt))
                return;

            int size = _pic.GetInterruptVectorSize(interrupt);
            int intBase = _pic.GetInterruptVectorBase(interrupt);

            var newIP = ReadU16((uint) ((intBase + interrupt)*size));
            var newCS = ReadU16((uint) (((intBase + interrupt)*size) + 2));

            Push(GetRegister(Register.FLAGS));
            Push(GetRegister(Register.CS));
            Push(GetRegister(Register.IP));
            SetRegister(Register.IP, newIP);
            SetRegister(Register.CS, newCS);

            _pic.AckInterrupt(interrupt);
        }

        private void CalculateIncFlags(OpCodeManager.OpCodeFlag flag, ushort value1, ushort value2, int result)
        {
            if (flag.Has(OpCodeManager.OpCodeFlag.Size8))
                CalculateIncFlags8Bit((byte)value1, (byte)value2, result);
            else CalculateIncFlags16Bit(value1, value2, result);
        }
        private void CalculateDecFlags(OpCodeManager.OpCodeFlag flag, ushort value1, ushort value2, int result)
        {
            if (flag.Has(OpCodeManager.OpCodeFlag.Size8))
                CalculateDecFlags8Bit((byte)value1, (byte)value2, result);
            else CalculateDecFlags16Bit(value1, value2, result);
        }
        private void CalculateBitwiseFlags(OpCodeManager.OpCodeFlag flag, ushort value1, ushort value2, int result)
        {
            if (flag.Has(OpCodeManager.OpCodeFlag.Size8))
                CalculateBitwiseFlags8Bit((byte)value1, (byte)value2, result);
            else CalculateBitwiseFlags16Bit(value1, value2, result);
        }
        private void CalculateAddFlags(OpCodeManager.OpCodeFlag flag, ushort value1, ushort value2, int result)
        {
            if (flag.Has(OpCodeManager.OpCodeFlag.Size8))
                CalculateAddFlags8Bit((byte)value1, (byte)value2, result);
            else CalculateAddFlags16Bit(value1, value2, result);
        }
        private void CalculateSubFlags(OpCodeManager.OpCodeFlag flag, ushort value1, ushort value2, int result)
        {
            if (flag.Has(OpCodeManager.OpCodeFlag.Size8))
                CalculateSubFlags8Bit((byte)value1, (byte)value2, result);
            else CalculateSubFlags16Bit(value1, value2, result);
        }

        private void CalculateIncFlags8Bit(byte value1, byte value2, int result)
        {
            var truncResult = (byte)result;
            var sign = ((truncResult >> 7) & 1) == 1;
            var overflow = ((truncResult ^ value1) & (truncResult ^ value2) & 0x80) == 0x80;
            var auxiliary = ((value1 ^ value2 ^ truncResult) & 0x10) != 0;
            var zero = truncResult == 0;
            var parity = parityLookup[(byte)(result & 0xFF)];

            flags &= ~(FlagsRegister.Parity | FlagsRegister.Auxiliary | FlagsRegister.Zero | FlagsRegister.Sign | FlagsRegister.Overflow);
            flags |= (parity ? FlagsRegister.Parity : 0) |
                     (auxiliary ? FlagsRegister.Auxiliary : 0) |
                     (zero ? FlagsRegister.Zero : 0) |
                     (sign ? FlagsRegister.Sign : 0) |
                     (overflow ? FlagsRegister.Overflow : 0);
        }
        private void CalculateIncFlags16Bit(ushort value1, ushort value2, int result)
        {
            var truncResult = (ushort)result;
            var sign = ((truncResult >> 15) & 1) == 1;
            var overflow = ((truncResult ^ value1) & (truncResult ^ value2) & 0x8000) == 0x8000;
            var auxiliary = ((value1 ^ value2 ^ truncResult) & 0x10) != 0;
            var zero = truncResult == 0;
            var parity = parityLookup[(byte)(result & 0xFF)];

            flags &= ~(FlagsRegister.Parity | FlagsRegister.Auxiliary | FlagsRegister.Zero | FlagsRegister.Sign | FlagsRegister.Overflow);
            flags |= (parity ? FlagsRegister.Parity : 0) |
                     (auxiliary ? FlagsRegister.Auxiliary : 0) |
                     (zero ? FlagsRegister.Zero : 0) |
                     (sign ? FlagsRegister.Sign : 0) |
                     (overflow ? FlagsRegister.Overflow : 0);
        }
        private void CalculateDecFlags8Bit(byte value1, byte value2, int result)
        {
            var truncResult = (byte)result;
            var sign = ((truncResult >> 7) & 1) == 1;
            var overflow = ((truncResult ^ value1) & (value1 ^ value2) & 0x80) == 0x80;
            var auxiliary = ((value1 ^ value2 ^ truncResult) & 0x10) != 0;
            var zero = truncResult == 0;
            var parity = parityLookup[(byte)(result & 0xFF)];

            flags &= ~(FlagsRegister.Parity | FlagsRegister.Auxiliary | FlagsRegister.Zero | FlagsRegister.Sign | FlagsRegister.Overflow);
            flags |= (parity ? FlagsRegister.Parity : 0) |
                     (auxiliary ? FlagsRegister.Auxiliary : 0) |
                     (zero ? FlagsRegister.Zero : 0) |
                     (sign ? FlagsRegister.Sign : 0) |
                     (overflow ? FlagsRegister.Overflow : 0);
        }
        private void CalculateDecFlags16Bit(ushort value1, ushort value2, int result)
        {
            var truncResult = (ushort)result;
            var sign = ((truncResult >> 15) & 1) == 1;
            var overflow = ((truncResult ^ value1) & (value1 ^ value2) & 0x8000) == 0x8000;
            var auxiliary = ((value1 ^ value2 ^ truncResult) & 0x10) != 0;
            var zero = truncResult == 0;
            var parity = parityLookup[(byte)(result & 0xFF)];

            flags &= ~(FlagsRegister.Parity | FlagsRegister.Auxiliary | FlagsRegister.Zero | FlagsRegister.Sign | FlagsRegister.Overflow);
            flags |= (parity ? FlagsRegister.Parity : 0) |
                     (auxiliary ? FlagsRegister.Auxiliary : 0) |
                     (zero ? FlagsRegister.Zero : 0) |
                     (sign ? FlagsRegister.Sign : 0) |
                     (overflow ? FlagsRegister.Overflow : 0);
        }
        private void CalculateBitwiseFlags8Bit(byte value1, byte value2, int result)
        {
            var truncResult = (byte)result;
            var sign = ((truncResult >> 7) & 1) == 1;
            var zero = truncResult == 0;
            var parity = parityLookup[(byte)(result & 0xFF)];

            flags &= ~(FlagsRegister.Carry | FlagsRegister.Parity | FlagsRegister.Zero | FlagsRegister.Sign | FlagsRegister.Overflow);
            flags |= (parity ? FlagsRegister.Parity : 0) |
                     (zero ? FlagsRegister.Zero : 0) |
                     (sign ? FlagsRegister.Sign : 0);
        }
        private void CalculateBitwiseFlags16Bit(ushort value1, ushort value2, int result)
        {
            var truncResult = (ushort)result;
            var sign = ((truncResult >> 15) & 1) == 1;
            var zero = truncResult == 0;
            var parity = parityLookup[(byte)(result & 0xFF)];

            flags &= ~(FlagsRegister.Carry | FlagsRegister.Parity | FlagsRegister.Zero | FlagsRegister.Sign | FlagsRegister.Overflow);
            flags |= (parity ? FlagsRegister.Parity : 0) |
                     (zero ? FlagsRegister.Zero : 0) |
                     (sign ? FlagsRegister.Sign : 0);
        }
        private void CalculateAddFlags8Bit(byte value1, byte value2, int result)
        {
            var truncResult = (byte)result;
            var carry = (uint)result > 0xFF;
            var sign = ((truncResult >> 7) & 1) == 1;
            var overflow = ((truncResult ^ value1) & (truncResult ^ value2) & 0x80) == 0x80;
            var auxiliary = ((value1 ^ value2 ^ truncResult) & 0x10) != 0;
            var zero = truncResult == 0;
            var parity = parityLookup[(byte)(result & 0xFF)];

            flags &= ~(FlagsRegister.Carry | FlagsRegister.Parity | FlagsRegister.Auxiliary | FlagsRegister.Zero | FlagsRegister.Sign | FlagsRegister.Overflow);
            flags |= (carry ? FlagsRegister.Carry : 0) |
                     (parity ? FlagsRegister.Parity : 0) |
                     (auxiliary ? FlagsRegister.Auxiliary : 0) |
                     (zero ? FlagsRegister.Zero : 0) |
                     (sign ? FlagsRegister.Sign : 0) |
                     (overflow ? FlagsRegister.Overflow : 0);
        }
        private void CalculateAddFlags16Bit(ushort value1, ushort value2, int result)
        {
            var truncResult = (ushort)result;
            var carry = (uint)result > 0xFFFF;
            var sign = ((truncResult >> 15) & 1) == 1;
            var overflow = ((truncResult ^ value1) & (truncResult ^ value2) & 0x8000) == 0x8000;
            var auxiliary = ((value1 ^ value2 ^ truncResult) & 0x10) != 0;
            var zero = truncResult == 0;
            var parity = parityLookup[(byte)(result & 0xFF)];

            flags &= ~(FlagsRegister.Carry | FlagsRegister.Parity | FlagsRegister.Auxiliary | FlagsRegister.Zero | FlagsRegister.Sign | FlagsRegister.Overflow);
            flags |= (carry ? FlagsRegister.Carry : 0) |
                     (parity ? FlagsRegister.Parity : 0) |
                     (auxiliary ? FlagsRegister.Auxiliary : 0) |
                     (zero ? FlagsRegister.Zero : 0) |
                     (sign ? FlagsRegister.Sign : 0) |
                     (overflow ? FlagsRegister.Overflow : 0);
        }
        private void CalculateSubFlags8Bit(byte value1, byte value2, int result)
        {
            var truncResult = (byte)result;
            var carry = (uint)result > 0xFF;
            var sign = ((truncResult >> 7) & 1) == 1;
            var overflow = ((truncResult ^ value1) & (value1 ^ value2) & 0x80) == 0x80;
            var auxiliary = ((value1 ^ value2 ^ truncResult) & 0x10) != 0;
            var zero = truncResult == 0;
            var parity = parityLookup[(byte)(result & 0xFF)];

            flags &= ~(FlagsRegister.Carry | FlagsRegister.Parity | FlagsRegister.Auxiliary | FlagsRegister.Zero | FlagsRegister.Sign | FlagsRegister.Overflow);
            flags |= (carry ? FlagsRegister.Carry : 0) |
                     (parity ? FlagsRegister.Parity : 0) |
                     (auxiliary ? FlagsRegister.Auxiliary : 0) |
                     (zero ? FlagsRegister.Zero : 0) |
                     (sign ? FlagsRegister.Sign : 0) |
                     (overflow ? FlagsRegister.Overflow : 0);
        }
        private void CalculateSubFlags16Bit(ushort value1, ushort value2, int result)
        {
            var truncResult = (ushort)result;
            var carry = (uint)result > 0xFFFF;
            var sign = ((truncResult >> 15) & 1) == 1;
            var overflow = ((truncResult ^ value1) & (value1 ^ value2) & 0x8000) == 0x8000;
            var auxiliary = ((value1 ^ value2 ^ truncResult) & 0x10) != 0;
            var zero = truncResult == 0;
            var parity = parityLookup[(byte)(result & 0xFF)];

            flags &= ~(FlagsRegister.Carry | FlagsRegister.Parity | FlagsRegister.Auxiliary | FlagsRegister.Zero | FlagsRegister.Sign | FlagsRegister.Overflow);
            flags |= (carry ? FlagsRegister.Carry : 0) |
                     (parity ? FlagsRegister.Parity : 0) |
                     (auxiliary ? FlagsRegister.Auxiliary : 0) |
                     (zero ? FlagsRegister.Zero : 0) |
                     (sign ? FlagsRegister.Sign : 0) |
                     (overflow ? FlagsRegister.Overflow : 0);
        }

        private static void DispatchInvalid([NotNull] Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            throw new NotImplementedException();
        }
        private static void DispatchMove([NotNull] Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            var value = cpu.GetInstructionValue(instruction.Flag, instruction.SegmentPrefix, instruction.Argument2, instruction.Argument2Value, instruction.Argument2Displacement);

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
                    cpu.registers[instruction.Argument1] = value;
                    break;

                case OpCodeManager.ARG_BYTE_REGISTER:
                    cpu.SetRegisterU8((Register)instruction.Argument1Value, (byte)value);
                    break;

                case OpCodeManager.ARG_DEREFERENCE:
                case OpCodeManager.ARG_MEMORY:
                    var address = cpu.GetInstructionRealAddress(instruction.SegmentPrefix, instruction.Argument1, instruction.Argument1Value, instruction.Argument1Displacement);
                    if (instruction.Flag.Has(OpCodeManager.OpCodeFlag.Size8))
                        cpu.WriteU8(address, (byte)value);
                    else cpu.WriteU16(address, value);
                    break;

                default:
                    throw new NotImplementedException();
            }
        }
        private static void DispatchArithmetic([NotNull] Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            var value1 = cpu.GetInstructionValue(instruction.Flag, instruction.SegmentPrefix, instruction.Argument1, instruction.Argument1Value, instruction.Argument1Displacement);
            var value2 = cpu.GetInstructionValue(instruction.Flag, instruction.SegmentPrefix, instruction.Argument2, instruction.Argument2Value, instruction.Argument2Displacement);

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
                    cpu.CalculateAddFlags(instruction.Flag, value1, value2, result);
                    break;

                case OpCodeManager.InstructionType.Add:
                    result = value1 + value2;
                    cpu.CalculateAddFlags(instruction.Flag, value1, value2, result);
                    break;

                case OpCodeManager.InstructionType.And:
                case OpCodeManager.InstructionType.Test:
                    result = value1 & value2;
                    cpu.CalculateBitwiseFlags(instruction.Flag, value1, value2, result);
                    break;

                case OpCodeManager.InstructionType.Compare:
                case OpCodeManager.InstructionType.Subtract:
                    result = value1 - value2;
                    cpu.CalculateSubFlags(instruction.Flag, value1, value2, result);
                    break;

                case OpCodeManager.InstructionType.Or:
                    result = value1 | value2;
                    cpu.CalculateBitwiseFlags(instruction.Flag, value1, value2, result);
                    break;

                case OpCodeManager.InstructionType.Rcl:
                    if (instruction.Flag.Has(OpCodeManager.OpCodeFlag.Size8))
                    {
                        const int mask = 0x1FF;
                        var shift = (value2 & 0x1F) % 9;

                        result = (byte)value1;
                        if (cpu.flags.Has(FlagsRegister.Carry))
                            result |= 0x100;
                        result = (byte)(result << shift) | (byte)(result >> (-shift & mask));
                        if ((result & 0x100) != 0)
                            cpu.flags |= FlagsRegister.Carry;
                        else cpu.flags &= ~FlagsRegister.Carry;

                        if (value2 == 1)
                        {
                            if (((result & 0x100) != 0) ^ ((result & 0x80) != 0))
                                cpu.flags |= FlagsRegister.Overflow;
                            else cpu.flags &= ~FlagsRegister.Overflow;
                        }
                    }
                    else
                    {
                        const int mask = 0x1FFFF;
                        var shift = (value2 & 0x1F) % 17;

                        result = value1;
                        if (cpu.flags.Has(FlagsRegister.Carry))
                            result |= 0x10000;
                        result = (ushort)(result << shift) | (ushort)(result >> (-shift & mask));
                        if ((result & 0x10000) != 0)
                            cpu.flags |= FlagsRegister.Carry;
                        else cpu.flags &= ~FlagsRegister.Carry;

                        if (value2 == 1)
                        {
                            if (((result & 0x10000) != 0) ^ ((result & 0x8000) != 0))
                                cpu.flags |= FlagsRegister.Overflow;
                            else cpu.flags &= ~FlagsRegister.Overflow;
                        }
                    }
                    cpu.CalculateBitwiseFlags(instruction.Flag, value1, value2, result);
                    break;

                case OpCodeManager.InstructionType.Rcr:
                    if (instruction.Flag.Has(OpCodeManager.OpCodeFlag.Size8))
                    {
                        const int mask = 0x1FF;
                        var shift = (value2 & 0x1F) % 9;

                        result = (byte)value1;
                        if (cpu.flags.Has(FlagsRegister.Carry))
                            result |= 0x100;

                        if ((result & 0x1) != 0)
                            cpu.flags |= FlagsRegister.Carry;
                        else cpu.flags &= ~FlagsRegister.Carry;

                        result = (byte)(result >> shift) | (byte)(result << (-shift & mask));
                        
                        if (value2 == 1)
                        {
                            if (((result & 0x80) != 0) ^ ((result & 0x40) != 0))
                                cpu.flags |= FlagsRegister.Overflow;
                            else cpu.flags &= ~FlagsRegister.Overflow;
                        }
                    }
                    else
                    {
                        const int mask = 0x1FFFF;
                        var shift = (value2 & 0x1F) % 17;

                        result = value1;
                        if (cpu.flags.Has(FlagsRegister.Carry))
                            result |= 0x10000;

                        if ((result & 0x1) != 0)
                            cpu.flags |= FlagsRegister.Carry;
                        else cpu.flags &= ~FlagsRegister.Carry;

                        result = (ushort)(result >> shift) | (ushort)(result << (-shift & mask));

                        if (value2 == 1)
                        {
                            if (((result & 0x8000) != 0) ^ ((result & 0x4000) != 0))
                                cpu.flags |= FlagsRegister.Overflow;
                            else cpu.flags &= ~FlagsRegister.Overflow;
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
                    cpu.CalculateBitwiseFlags(instruction.Flag, value1, value2, result);
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
                    cpu.CalculateBitwiseFlags(instruction.Flag, value1, value2, result);
                    break;

                case OpCodeManager.InstructionType.Sbb:
                    result = value1 - (value2 + (cpu.GetFlags().Has(FlagsRegister.Carry) ? 1 : 0));
                    cpu.CalculateSubFlags(instruction.Flag, value1, value2, result);
                    break;

                case OpCodeManager.InstructionType.Shl:
                    bool overflow;
                    result = value1 << (value2 & 0x1F);
                    cpu.CalculateBitwiseFlags(instruction.Flag, value1, (ushort)(value2 & 0x1F), result);
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
                        cpu.flags |= FlagsRegister.Carry;
                    if ((value2 & 0x1F) == 1 && overflow ^ carry)
                        cpu.flags |= FlagsRegister.Overflow;
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
                    cpu.CalculateBitwiseFlags(instruction.Flag, value1, (ushort)(value2 & 0x1F), result);
                    if (carry)
                        cpu.flags |= FlagsRegister.Carry;
                    break;

                case OpCodeManager.InstructionType.Shr:
                    carry = ((value1 >> ((value2 & 0x1F) - 1)) & 1) != 0;
                    result = value1 >> (value2 & 0x1F);
                    cpu.CalculateBitwiseFlags(instruction.Flag, value1, (ushort)(value2 & 0x1F), result);
                    if (carry)
                        cpu.flags |= FlagsRegister.Carry;
                    if ((value2 & 0x1F) == 1)
                    {
                        if (instruction.Flag.Has(OpCodeManager.OpCodeFlag.Size8))
                        {
                            if ((value1 & 0x80) != 0)
                                cpu.flags |= FlagsRegister.Overflow;
                        }
                        else
                        {
                            if ((value1 & 0x8000) != 0)
                                cpu.flags |= FlagsRegister.Overflow;
                        }
                    }
                    break;

                case OpCodeManager.InstructionType.Xor:
                    result = value1 ^ value2;
                    cpu.CalculateBitwiseFlags(instruction.Flag, value1, value2, result);
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
                        cpu.registers[instruction.Argument1] = truncResult;
                        break;

                    case OpCodeManager.ARG_BYTE_REGISTER:
                        cpu.SetRegisterU8((Register)instruction.Argument1Value, (byte)truncResult);
                        break;

                    case OpCodeManager.ARG_DEREFERENCE:
                    case OpCodeManager.ARG_MEMORY:
                        var address = cpu.GetInstructionRealAddress(instruction.SegmentPrefix, instruction.Argument1, instruction.Argument1Value, instruction.Argument1Displacement);
                        if (instruction.Flag.Has(OpCodeManager.OpCodeFlag.Size8))
                            cpu.WriteU8(address, (byte)truncResult);
                        else cpu.WriteU16(address, truncResult);
                        break;

                    default:
                        throw new NotImplementedException();
                }
            }
        }
        private static void DispatchUnaryArithmetic([NotNull] Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            var value = cpu.GetInstructionValue(instruction.Flag, instruction.SegmentPrefix, instruction.Argument1, instruction.Argument1Value, instruction.Argument1Displacement);
            int result;

            switch (instruction.Type)
            {
                case OpCodeManager.InstructionType.Decrement:
                    result = value - 1;
                    cpu.CalculateDecFlags(instruction.Flag, value, 1, result);
                    break;
                case OpCodeManager.InstructionType.Increment:
                    result = value + 1;
                    cpu.CalculateIncFlags(instruction.Flag, value, 1, result);
                    break;
                case OpCodeManager.InstructionType.Negate:
                    result = ~value + 1;
                    cpu.CalculateSubFlags(instruction.Flag, 0, value, result);
                    break;
                case OpCodeManager.InstructionType.Not:
                    result = ~value;
                    break;
                default:
                    throw new OutOfMemoryException();
            }

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
                    cpu.registers[instruction.Argument1] = (ushort)result;
                    break;

                case OpCodeManager.ARG_BYTE_REGISTER:
                    cpu.SetRegisterU8((Register)instruction.Argument1Value, (byte)result);
                    break;

                case OpCodeManager.ARG_DEREFERENCE:
                case OpCodeManager.ARG_MEMORY:
                    var address = cpu.GetInstructionRealAddress(instruction.SegmentPrefix, instruction.Argument1, instruction.Argument1Value, instruction.Argument1Displacement);
                    if (instruction.Flag.Has(OpCodeManager.OpCodeFlag.Size8))
                        cpu.WriteU8(address, (byte)result);
                    else cpu.WriteU16(address, (ushort)result);
                    break;

                default:
                    throw new NotImplementedException();
            }
        }
        private static void DispatchLea([NotNull] Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            var address = cpu.GetInstructionAddress(instruction.Argument2, instruction.Argument2Value, instruction.Argument2Displacement);

            switch ((Register)instruction.Argument1)
            {
                case Register.AX:
                case Register.CX:
                case Register.DX:
                case Register.BX:
                case Register.SP:
                case Register.BP:
                case Register.SI:
                case Register.DI:
                case Register.IP:
                case Register.CS:
                case Register.DS:
                case Register.ES:
                case Register.SS:
                    cpu.registers[instruction.Argument1] = address;
                    break;

                default:
                    throw new NotImplementedException();
            }
        }
        private static void DispatchExchange([NotNull] Cpu8086 cpu, OpCodeManager.Instruction instruction)
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
                    cpu.registers[instruction.Argument1] = cpu.ProcessExchangeSecond(instruction, cpu.registers[instruction.Argument1]);
                    break;

                case OpCodeManager.ARG_BYTE_REGISTER:
                    cpu.SetRegister((Register)instruction.Argument1Value, (byte)cpu.ProcessExchangeSecond(instruction, cpu.GetRegisterU8((Register)instruction.Argument1Value)));
                    break;

                default:
                    throw new NotImplementedException();
            }
        }
        private static void DispatchFarJump([NotNull] Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            switch (instruction.Argument1)
            {
                case OpCodeManager.ARG_FAR_MEMORY:
                    cpu.SetRegister(Register.CS, (ushort)((uint)instruction.Argument1Value >> 16));
                    cpu.SetRegister(Register.IP, (ushort)(instruction.Argument1Value & 0xFFFF));
                    break;

                case OpCodeManager.ARG_DEREFERENCE:
                case OpCodeManager.ARG_MEMORY:
                    var address = cpu.GetInstructionRealAddress(instruction.SegmentPrefix, instruction.Argument1, instruction.Argument1Value, instruction.Argument1Displacement);
                    cpu.SetRegister(Register.CS, cpu.ReadU16(address + 2));
                    cpu.SetRegister(Register.IP, cpu.ReadU16(address));
                    break;

                default:
                    throw new NotImplementedException();
            }
        }
        private static void DispatchCbw([NotNull] Cpu8086 cpu, OpCodeManager.Instruction instruction) =>
            cpu.SetRegister(Register.AX, (ushort)(sbyte)cpu.GetRegisterU8(Register.AL));
        private static void DispatchCwd([NotNull] Cpu8086 cpu, OpCodeManager.Instruction instruction) =>
            cpu.SetRegister(Register.DX, (cpu.GetRegister(Register.AX) & 0x8000) != 0 ? (ushort)0xFFFF : (ushort)0);
        private static void DispatchCallNearRelative([NotNull] Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            Debug.Assert(instruction.Argument1 == OpCodeManager.ARG_CONSTANT);
            cpu.Push(cpu.GetRegister(Register.IP));
            cpu.registers[(int)Register.IP] += (ushort)instruction.Argument1Value;
        }
        private static void DispatchCallNear([NotNull] Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            var address = cpu.GetInstructionValue(instruction.Flag, instruction.SegmentPrefix, instruction.Argument1, instruction.Argument1Value, instruction.Argument1Displacement);
            cpu.Push(cpu.GetRegister(Register.IP));
            cpu.SetRegister(Register.IP, address);
        }
        private static void DispatchCallFar([NotNull] Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            cpu.Push(cpu.GetRegister(Register.CS));
            cpu.Push(cpu.GetRegister(Register.IP));
            DispatchFarJump(cpu, instruction);
        }
        private static void DispatchWait([NotNull] Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            throw new NotImplementedException();
        }
        private static void DispatchSahf([NotNull] Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            const FlagsRegister flagsAffected = FlagsRegister.Sign | FlagsRegister.Zero | FlagsRegister.Auxiliary | FlagsRegister.Parity | FlagsRegister.Carry;
            cpu.flags &= ~flagsAffected;
            cpu.flags |= (FlagsRegister)(cpu.GetRegister(Register.AH) & (ushort)flagsAffected);
        }
        private static void DispatchLahf([NotNull] Cpu8086 cpu, OpCodeManager.Instruction instruction) =>
            cpu.SetRegister(Register.AH, (byte)cpu.GetFlags());
        private static void DispatchAas([NotNull] Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            var al = cpu.GetRegisterU8(Register.AL);
            var flags = cpu.GetFlags();

            if ((al & 0xF) > 9 || flags.Has(FlagsRegister.Auxiliary))
            {
                var ah = cpu.GetRegisterU8(Register.AH);

                al = (byte)((al - 6) & 0x0F);
                ah--;

                cpu.SetRegisterU8(Register.AH, ah);
                flags |= FlagsRegister.Carry | FlagsRegister.Auxiliary;
            }
            else
            {
                al &= 0x0F;
                flags &= ~(FlagsRegister.Carry | FlagsRegister.Auxiliary);
            }

            flags &= ~(FlagsRegister.Parity | FlagsRegister.Zero | FlagsRegister.Sign);
            flags |= (parityLookup[al] ? FlagsRegister.Parity : 0) |
                     (al == 0 ? FlagsRegister.Zero : 0) |
                     ((al & 0x80) != 0 ? FlagsRegister.Sign : 0);

            cpu.SetFlags(flags);
            cpu.SetRegisterU8(Register.AL, al);
        }
        private static void DispatchAaa([NotNull] Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            var al = cpu.GetRegisterU8(Register.AL);
            var flags = cpu.GetFlags();

            if ((al & 0xF) > 9 || flags.Has(FlagsRegister.Auxiliary))
            {
                var ah = cpu.GetRegisterU8(Register.AH);

                al = (byte)((al + 6) & 0x0F);
                ah++;

                cpu.SetRegisterU8(Register.AH, ah);
                flags |= FlagsRegister.Carry | FlagsRegister.Auxiliary;
            }
            else
            {
                al &= 0x0F;
                flags &= ~(FlagsRegister.Carry | FlagsRegister.Auxiliary);
            }

            flags &= ~(FlagsRegister.Parity | FlagsRegister.Zero | FlagsRegister.Sign);
            flags |= (parityLookup[al] ? FlagsRegister.Parity : 0) |
                     (al == 0 ? FlagsRegister.Zero : 0) |
                     ((al & 0x80) != 0 ? FlagsRegister.Sign : 0);

            cpu.SetFlags(flags);
            cpu.SetRegisterU8(Register.AL, al);
        }
        private static void DispatchDaa([NotNull] Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            var al = cpu.GetRegisterU8(Register.AL);
            var oldAl = al;
            var flags = cpu.GetFlags();
            var oldCarry = flags.Has(FlagsRegister.Carry);

            if ((al & 0xF) > 9 || flags.Has(FlagsRegister.Auxiliary))
            {
                al += 6;
                if (oldCarry || (al < oldAl))
                    flags |= FlagsRegister.Carry;
                else flags &= ~FlagsRegister.Carry;
                flags |= FlagsRegister.Auxiliary;
            }
            else flags &= ~FlagsRegister.Auxiliary;

            if (oldAl > 0x99 || oldCarry)
            {
                al += 0x60;
                flags |= FlagsRegister.Carry;
            }
            else flags &= ~FlagsRegister.Carry;

            flags &= ~(FlagsRegister.Parity | FlagsRegister.Zero | FlagsRegister.Sign);
            flags |= (parityLookup[al] ? FlagsRegister.Parity : 0) |
                     (al == 0 ? FlagsRegister.Zero : 0) |
                     ((al & 0x80) != 0 ? FlagsRegister.Sign : 0);

            cpu.SetFlags(flags);
            cpu.SetRegister(Register.AL, al);
        }
        private static void DispatchDas([NotNull] Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            var al = cpu.GetRegisterU8(Register.AL);
            var oldAl = al;
            var flags = cpu.GetFlags();
            var oldCarry = flags.Has(FlagsRegister.Carry);
            flags &= ~FlagsRegister.Carry;

            if ((al & 0xF) > 9 || flags.Has(FlagsRegister.Auxiliary))
            {
                al -= 6;
                if (oldCarry || (al > oldAl))
                    flags |= FlagsRegister.Carry;
                else flags &= ~FlagsRegister.Carry;
                flags |= FlagsRegister.Auxiliary;
            }
            else flags &= ~FlagsRegister.Auxiliary;

            if (oldAl > 0x99 || oldCarry)
            {
                al -= 0x60;
                flags |= FlagsRegister.Carry;
            }
            else flags &= ~FlagsRegister.Carry;

            flags &= ~(FlagsRegister.Parity | FlagsRegister.Zero | FlagsRegister.Sign);
            flags |= (parityLookup[al] ? FlagsRegister.Parity : 0) |
                     (al == 0 ? FlagsRegister.Zero : 0) |
                     ((al & 0x80) != 0 ? FlagsRegister.Sign : 0);

            cpu.SetFlags(flags);
            cpu.SetRegister(Register.AL, al);
        }
        private static void DispatchStringOperation([NotNull] Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            ushort counter;
            switch (instruction.OpcodePrefix)
            {
                case 0:
                    DispatchOneStringOperation(cpu, instruction);
                    break;

                case 0xF2:
                    counter = cpu.GetRegister(Register.CX);
                    if (instruction.Type == OpCodeManager.InstructionType.Cmps || instruction.Type == OpCodeManager.InstructionType.Scas)
                    {
                        while (counter != 0)
                        {
                            DispatchOneStringOperation(cpu, instruction);
                            counter--;
                            if (cpu.GetFlags().Has(FlagsRegister.Zero))
                                break;
                        }
                    }
                    else
                    {
                        while (counter != 0)
                        {
                            DispatchOneStringOperation(cpu, instruction);
                            counter--;
                        }
                    }
                    cpu.SetRegister(Register.CX, counter);
                    break;

                case 0xF3:
                    counter = cpu.GetRegister(Register.CX);
                    if (instruction.Type == OpCodeManager.InstructionType.Cmps || instruction.Type == OpCodeManager.InstructionType.Scas)
                    {
                        while (counter != 0)
                        {
                            DispatchOneStringOperation(cpu, instruction);
                            counter--;
                            if (!cpu.GetFlags().Has(FlagsRegister.Zero))
                                break;
                        }
                    }
                    else
                    {
                        while (counter != 0)
                        {
                            DispatchOneStringOperation(cpu, instruction);
                            counter--;
                        }
                    }
                    cpu.SetRegister(Register.CX, counter);
                    break;

                default:
                    throw new NotImplementedException();
            }
        }
        private static void DispatchOneStringOperation([NotNull] Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            switch (instruction.Type)
            {
                case OpCodeManager.InstructionType.Cmps:
                    DispatchCompareString(cpu, instruction);
                    break;
                case OpCodeManager.InstructionType.Lods:
                    DispatchLoadString(cpu, instruction);
                    break;
                case OpCodeManager.InstructionType.Movs:
                    DispatchMoveString(cpu, instruction);
                    break;
                case OpCodeManager.InstructionType.Stos:
                    DispatchStoreString(cpu, instruction);
                    break;
                case OpCodeManager.InstructionType.Scas:
                    DispatchScanString(cpu, instruction);
                    break;
                default:
                    throw new NotImplementedException();
            }
        }
        private static void DispatchCompareString([NotNull] Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            ushort value1;
            ushort value2;
            byte size;
            if (instruction.Flag.Has(OpCodeManager.OpCodeFlag.Size8))
            {
                value1 = cpu.ReadU8(SegmentToAddress(cpu.GetRegister(Register.DS), cpu.GetRegister(Register.SI)));
                value2 = cpu.ReadU8(SegmentToAddress(cpu.GetRegister(Register.ES), cpu.GetRegister(Register.DI)));
                size = 1;
            }
            else
            {
                value1 = cpu.ReadU16(SegmentToAddress(cpu.GetRegister(Register.DS), cpu.GetRegister(Register.SI)));
                value2 = cpu.ReadU16(SegmentToAddress(cpu.GetRegister(Register.ES), cpu.GetRegister(Register.DI)));
                size = 2;
            }
            var result = value1 - value2;

            cpu.CalculateSubFlags(instruction.Flag, value1, value2, result);

            if (!cpu.GetFlags().Has(FlagsRegister.Direction))
            {
                cpu.registers[(int)Register.DI] += size;
                cpu.registers[(int)Register.SI] += size;
            }
            else
            {
                cpu.registers[(int)Register.DI] -= size;
                cpu.registers[(int)Register.SI] -= size;
            }
        }
        private static void DispatchLoadString([NotNull] Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            var prefix = instruction.SegmentPrefix;
            if (prefix == Register.Invalid) prefix = Register.DS;
            var sourceAddress = SegmentToAddress(cpu.GetRegister(prefix), cpu.GetRegister(Register.SI));

            byte size;
            if (instruction.Flag.Has(OpCodeManager.OpCodeFlag.Size8))
            {
                cpu.SetRegisterU8(Register.AL, cpu.ReadU8(sourceAddress));
                size = 1;
            }
            else
            {
                cpu.SetRegister(Register.AX, cpu.ReadU16(sourceAddress));
                size = 2;
            }

            if (!cpu.GetFlags().Has(FlagsRegister.Direction))
                cpu.registers[(int)Register.SI] += size;
            else cpu.registers[(int)Register.SI] -= size;
        }
        private static void DispatchMoveString([NotNull] Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            var segment = Register.DS;
            if (instruction.SegmentPrefix != Register.Invalid)
                segment = instruction.SegmentPrefix;

            var sourceAddress = SegmentToAddress(cpu.GetRegister(segment), cpu.GetRegister(Register.SI));
            var destAddress = SegmentToAddress(cpu.GetRegister(Register.ES), cpu.GetRegister(Register.DI));
            byte size;
            if (instruction.Flag.Has(OpCodeManager.OpCodeFlag.Size8))
            {
                var value = cpu.ReadU8(sourceAddress);
                cpu.WriteU8(destAddress, value);
                size = 1;
            }
            else
            {
                var value = cpu.ReadU16(sourceAddress);
                cpu.WriteU16(destAddress, value);
                size = 2;
            }

            if (!cpu.GetFlags().Has(FlagsRegister.Direction))
            {
                cpu.registers[(int)Register.DI] += size;
                cpu.registers[(int)Register.SI] += size;
            }
            else
            {
                cpu.registers[(int)Register.DI] -= size;
                cpu.registers[(int)Register.SI] -= size;
            }
        }
        private static void DispatchStoreString([NotNull] Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            byte size;
            if (instruction.Flag.Has(OpCodeManager.OpCodeFlag.Size8))
            {
                cpu.WriteU8(SegmentToAddress(cpu.GetRegister(Register.ES), cpu.GetRegister(Register.DI)), (byte)(cpu.GetRegister(Register.AX) & 0xFF));
                size = 1;
            }
            else
            {
                cpu.WriteU16(SegmentToAddress(cpu.GetRegister(Register.ES), cpu.GetRegister(Register.DI)), cpu.GetRegister(Register.AX));
                size = 2;
            }

            if (!cpu.GetFlags().Has(FlagsRegister.Direction))
                cpu.registers[(int)Register.DI] += size;
            else cpu.registers[(int)Register.DI] -= size;
        }
        private static void DispatchScanString([NotNull] Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            ushort value1;
            ushort value2;
            byte size;
            if (instruction.Flag.Has(OpCodeManager.OpCodeFlag.Size8))
            {
                value1 = cpu.GetRegisterU8(Register.AL);
                value2 = cpu.ReadU8(SegmentToAddress(cpu.GetRegister(Register.ES), cpu.GetRegister(Register.DI)));
                size = 1;
            }
            else
            {
                value1 = cpu.GetRegister(Register.AX);
                value2 = cpu.ReadU16(SegmentToAddress(cpu.GetRegister(Register.ES), cpu.GetRegister(Register.DI)));
                size = 2;
            }
            var result = value1 - value2;

            cpu.CalculateSubFlags(instruction.Flag, value1, value2, result);

            if (!cpu.GetFlags().Has(FlagsRegister.Direction))
                cpu.registers[(int)Register.DI] += size;
            else cpu.registers[(int)Register.DI] -= size;
        }
        private static void DispatchReturnNear([NotNull] Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            cpu.SetRegister(Register.IP, cpu.Pop());
            if (instruction.Argument1 == OpCodeManager.ARG_CONSTANT)
                cpu.SetRegister(Register.SP, (ushort)(cpu.GetRegister(Register.SP) + instruction.Argument1Value));
            else Debug.Assert(instruction.Argument1 == OpCodeManager.ARG_NONE);
        }
        private static void DispatchReturnFar([NotNull] Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            cpu.SetRegister(Register.IP, cpu.Pop());
            cpu.SetRegister(Register.CS, cpu.Pop());
            if (instruction.Argument1 == OpCodeManager.ARG_CONSTANT)
                cpu.SetRegister(Register.SP, (ushort)(cpu.GetRegister(Register.SP) + instruction.Argument1Value));
            else Debug.Assert(instruction.Argument1 == OpCodeManager.ARG_NONE);
        }
        private static void DispatchLoadFarPointer([NotNull] Cpu8086 cpu, OpCodeManager.Instruction instruction, Register register)
        {
            var address = cpu.GetInstructionRealAddress(instruction.SegmentPrefix, instruction.Argument2, instruction.Argument2Value, instruction.Argument2Displacement);
            var memory = cpu.ReadU16(address);
            var segment = cpu.ReadU16(address + 2);

            cpu.SetRegister(register, segment);
            switch ((Register)instruction.Argument1)
            {
                case Register.AX:
                case Register.CX:
                case Register.DX:
                case Register.BX:
                case Register.SP:
                case Register.BP:
                case Register.SI:
                case Register.DI:
                case Register.IP:
                case Register.CS:
                case Register.DS:
                case Register.ES:
                case Register.SS:
                    cpu.registers[instruction.Argument1] = memory;
                    break;

                default:
                    throw new NotImplementedException();
            }
        }
        private static void DispatchInterrupt([NotNull] Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            Debug.Assert(instruction.Argument1 == OpCodeManager.ARG_CONSTANT);
            cpu.Interrupt((byte)instruction.Argument1Value);
        }
        private static void DispatchInto([NotNull] Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            throw new NotImplementedException();
        }
        private static void DispatchReturnInterrupt([NotNull] Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            cpu.SetRegister(Register.IP, cpu.Pop());
            cpu.SetRegister(Register.CS, cpu.Pop());
            cpu.SetRegister(Register.FLAGS, cpu.Pop());
            //cpu._interruptStack.Pop();
        }
        private static void DispatchAam([NotNull] Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            throw new NotImplementedException();
        }
        private static void DispatchAad([NotNull] Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            throw new NotImplementedException();
        }
        private static void DispatchXlat([NotNull] Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            var address = SegmentToAddress(cpu.GetRegister(Register.DS), (ushort)(cpu.GetRegister(Register.BX) + cpu.GetRegisterU8(Register.AL)));
            cpu.SetRegisterU8(Register.AL, cpu.ReadU8(address));
        }
        private static void DispatchLoop([NotNull] Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            var counter = --cpu.registers[(int)Register.CX];
            if (counter != 0)
                DispatchJumpRelative(cpu, instruction);
        }
        private static void DispatchLoopZero([NotNull] Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            var counter = --cpu.registers[(int)Register.CX];
            if (counter != 0 && cpu.GetFlags().Has(FlagsRegister.Zero))
                DispatchJumpRelative(cpu, instruction);
        }
        private static void DispatchLoopNotZero([NotNull] Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            var counter = --cpu.registers[(int)Register.CX];
            if (counter != 0 && !cpu.GetFlags().Has(FlagsRegister.Zero))
                DispatchJumpRelative(cpu, instruction);
        }
        private static void DispatchIn([NotNull] Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            var port = cpu.GetInstructionValue(instruction.Flag, instruction.SegmentPrefix, instruction.Argument2, instruction.Argument2Value, instruction.Argument2Displacement);
            var device = cpu.GetPortDevice(port);
            if (device == null)
                throw new InvalidOperationException($"Tried to read from port 0x{port.ToString("X")}");

            if (instruction.Flag.Has(OpCodeManager.OpCodeFlag.Size8))
            {
                cpu.SetRegister(Register.AL, device.Read(port));
                //Console.WriteLine($"IN {port.ToString("X")}:{cpu.GetRegister(Register.AL).ToString("X")}");
            }
            else
            {
                cpu.SetRegister(Register.AX, device.Read16(port));
            }
        }
        private static void DispatchOut([NotNull] Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            var port = cpu.GetInstructionValue(instruction.Flag, instruction.SegmentPrefix, instruction.Argument1, instruction.Argument1Value, instruction.Argument1Displacement);

            var device = cpu.GetPortDevice(port);
            if (device == null)
                throw new InvalidOperationException($"Tried to read from port 0x{port.ToString("X")}");

            if (instruction.Flag.Has(OpCodeManager.OpCodeFlag.Size8))
            {
                //Console.WriteLine($"OUT {port.ToString("X")}:{cpu.GetRegister(Register.AL).ToString("X")}");
                device.Write(port, (byte) cpu.GetRegister(Register.AL));
            }
            else
            {
                device.Write16(port, cpu.GetRegister(Register.AX));
            }
        }

        private static void DispatchHalt([NotNull] Cpu8086 cpu, OpCodeManager.Instruction instruction) =>
            // Goes back one instruction so it halts again if process instruction is called
            --cpu.registers[(int)Register.IP];

        private static void DispatchCmc([NotNull] Cpu8086 cpu, OpCodeManager.Instruction instruction) =>
            cpu.SetFlags(cpu.GetFlags() ^ FlagsRegister.Carry);

        private static void DispatchClc([NotNull] Cpu8086 cpu, OpCodeManager.Instruction instruction) =>
            cpu.SetFlags(cpu.GetFlags() & ~FlagsRegister.Carry);
        private static void DispatchCld([NotNull] Cpu8086 cpu, OpCodeManager.Instruction instruction) =>
            cpu.SetFlags(cpu.GetFlags() & ~FlagsRegister.Direction);
        private static void DispatchCli([NotNull] Cpu8086 cpu, OpCodeManager.Instruction instruction) =>
            cpu.SetFlags(cpu.GetFlags() & ~FlagsRegister.Interrupt);

        private static void DispatchStc([NotNull] Cpu8086 cpu, OpCodeManager.Instruction instruction) =>
            cpu.SetFlags(cpu.GetFlags() | FlagsRegister.Carry);
        private static void DispatchStd([NotNull] Cpu8086 cpu, OpCodeManager.Instruction instruction) =>
            cpu.SetFlags(cpu.GetFlags() | FlagsRegister.Direction);
        private static void DispatchSti([NotNull] Cpu8086 cpu, OpCodeManager.Instruction instruction) =>
            cpu.SetFlags(cpu.GetFlags() | FlagsRegister.Interrupt);

        private static void DispatchJumpRelative([NotNull] Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            Debug.Assert(instruction.Argument1 == OpCodeManager.ARG_CONSTANT);
            cpu.registers[(int)Register.IP] += (ushort)instruction.Argument1Value;
        }
        private static void DispatchJump([NotNull] Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            var value = cpu.GetInstructionValue(instruction.Flag, instruction.SegmentPrefix, instruction.Argument1, instruction.Argument1Value, instruction.Argument1Displacement);
            cpu.SetRegister(Register.IP, value);
        }
        private static void DispatchJumpIfOverflow([NotNull] Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            if (cpu.GetFlags().Has(FlagsRegister.Overflow))
                DispatchJumpRelative(cpu, instruction);
        }
        private static void DispatchJumpIfNotOverflow([NotNull] Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            if (!cpu.GetFlags().Has(FlagsRegister.Overflow))
                DispatchJumpRelative(cpu, instruction);
        }
        private static void DispatchJumpIfCarry([NotNull] Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            if (cpu.GetFlags().Has(FlagsRegister.Carry))
                DispatchJumpRelative(cpu, instruction);
        }
        private static void DispatchJumpIfNotCarry([NotNull] Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            if (!cpu.GetFlags().Has(FlagsRegister.Carry))
                DispatchJumpRelative(cpu, instruction);
        }
        private static void DispatchJumpIfZero([NotNull] Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            if (cpu.GetFlags().Has(FlagsRegister.Zero))
                DispatchJumpRelative(cpu, instruction);
        }
        private static void DispatchJumpIfNotZero([NotNull] Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            if (!cpu.GetFlags().Has(FlagsRegister.Zero))
                DispatchJumpRelative(cpu, instruction);
        }
        private static void DispatchJBE([NotNull] Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            var flags = cpu.GetFlags();
            if (flags.Has(FlagsRegister.Carry) || flags.Has(FlagsRegister.Zero))
                DispatchJumpRelative(cpu, instruction);
        }
        private static void DispatchJA([NotNull] Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            var flags = cpu.GetFlags();
            if (!flags.Has(FlagsRegister.Carry) && !flags.Has(FlagsRegister.Zero))
                DispatchJumpRelative(cpu, instruction);
        }
        private static void DispatchJS([NotNull] Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            if (cpu.GetFlags().Has(FlagsRegister.Sign))
                DispatchJumpRelative(cpu, instruction);
        }
        private static void DispatchJNS([NotNull] Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            if (!cpu.GetFlags().Has(FlagsRegister.Sign))
                DispatchJumpRelative(cpu, instruction);
        }
        private static void DispatchJPE([NotNull] Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            if (cpu.GetFlags().Has(FlagsRegister.Parity))
                DispatchJumpRelative(cpu, instruction);
        }
        private static void DispatchJLE([NotNull] Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            var flags = cpu.GetFlags();
            if (flags.Has(FlagsRegister.Zero) || flags.Has(FlagsRegister.Sign) != flags.Has(FlagsRegister.Overflow))
                DispatchJumpRelative(cpu, instruction);
        }
        private static void DispatchJPO([NotNull] Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            if (!cpu.GetFlags().Has(FlagsRegister.Parity))
                DispatchJumpRelative(cpu, instruction);
        }
        private static void DispatchJL([NotNull] Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            var flags = cpu.GetFlags();
            if (flags.Has(FlagsRegister.Sign) != flags.Has(FlagsRegister.Overflow))
                DispatchJumpRelative(cpu, instruction);
        }
        private static void DispatchJGE([NotNull] Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            var flags = cpu.GetFlags();
            if (flags.Has(FlagsRegister.Sign) == flags.Has(FlagsRegister.Overflow))
                DispatchJumpRelative(cpu, instruction);
        }
        private static void DispatchJG([NotNull] Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            var flags = cpu.GetFlags();
            if (!flags.Has(FlagsRegister.Zero) && flags.Has(FlagsRegister.Sign) == flags.Has(FlagsRegister.Overflow))
                DispatchJumpRelative(cpu, instruction);
        }
        private static void DispatchJcxz([NotNull] Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            if (cpu.GetRegister(Register.CX) == 0)
                DispatchJumpRelative(cpu, instruction);
        }
        private static void DispatchMultiply([NotNull] Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            var value1 = cpu.GetRegister(Register.AX);
            var value2 = cpu.GetInstructionValue(instruction.Flag, instruction.SegmentPrefix, instruction.Argument1, instruction.Argument1Value, instruction.Argument1Displacement);

            if (instruction.Flag.Has(OpCodeManager.OpCodeFlag.Size8))
            {
                value1 &= 0xFF;
                value2 &= 0xFF;
            }

            uint result;
            switch (instruction.Type)
            {
                case OpCodeManager.InstructionType.Multiply:
                    result = (uint)value1 * value2;
                    break;
                case OpCodeManager.InstructionType.SignedMultiply:
                    result = (uint)(value1 * value2);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            cpu.CalculateAddFlags(instruction.Flag, value1, value2, (int)result);

            cpu.SetRegister(Register.AX, (ushort)result);
            if (!instruction.Flag.Has(OpCodeManager.OpCodeFlag.Size8))
                cpu.SetRegister(Register.DX, (ushort)(result >> 16));
        }
        private static void DispatchDivide([NotNull] Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            if (instruction.Flag.Has(OpCodeManager.OpCodeFlag.Size8))
                DispatchDivide8(cpu, instruction);
            else DispatchDivide16(cpu, instruction);
        }
        private static void DispatchDivide8([NotNull] Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            uint value1 = cpu.GetRegister(Register.AX);
            uint value2 = (byte)cpu.GetInstructionValue(instruction.Flag, instruction.SegmentPrefix, instruction.Argument1, instruction.Argument1Value, instruction.Argument1Displacement);

            if (value2 == 0)
            {
                cpu.Interrupt(0);
                return;
            }

            var quotient = value1 / value2;
            if (quotient > 0xFF)
            {
                cpu.Interrupt(0);
                return;
            }

            var remainder = value1 % value2;
            cpu.SetRegisterU8(Register.AL, (byte)quotient);
            cpu.SetRegisterU8(Register.AH, (byte)remainder);
        }
        private static void DispatchDivide16([NotNull] Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            var value1 = ((uint)cpu.GetRegister(Register.DX) << 16) | cpu.GetRegister(Register.AX);
            uint value2 = cpu.GetInstructionValue(instruction.Flag, instruction.SegmentPrefix, instruction.Argument1, instruction.Argument1Value, instruction.Argument1Displacement);

            if (value2 == 0)
            {
                cpu.Interrupt(0);
                return;
            }

            var quotient = value1 / value2;
            if (quotient > 0xFFFF)
            {
                cpu.Interrupt(0);
                return;
            }

            var remainder = value1 % value2;
            cpu.SetRegister(Register.AX, (ushort)quotient);
            cpu.SetRegister(Register.DX, (ushort)remainder);
        }
        private static void DispatchSignedDivide([NotNull] Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            if (instruction.Flag.Has(OpCodeManager.OpCodeFlag.Size8))
                DispatchSignedDivide8(cpu, instruction);
            else DispatchSignedDivide16(cpu, instruction);
        }
        private static void DispatchSignedDivide8([NotNull] Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            int value1 = (short)cpu.GetRegister(Register.AX);
            int value2 = (sbyte)cpu.GetInstructionValue(instruction.Flag, instruction.SegmentPrefix, instruction.Argument1, instruction.Argument1Value, instruction.Argument1Displacement);

            if ((uint)value1 == 0x8000 || value2 == 0)
            {
                cpu.Interrupt(0);
                return;
            }

            var quotient = value1 / value2;
            var remainder = value1 % value2;

            if ((quotient & 0xFF) != quotient)
            {
                cpu.Interrupt(0);
                return;
            }

            cpu.SetRegisterU8(Register.AL, (byte)quotient);
            cpu.SetRegisterU8(Register.AH, (byte)remainder);
        }
        private static void DispatchSignedDivide16([NotNull] Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            var value1 = (int)(((uint)cpu.GetRegister(Register.DX) << 16) | cpu.GetRegister(Register.AX));
            int value2 = (short)cpu.GetInstructionValue(instruction.Flag, instruction.SegmentPrefix, instruction.Argument1, instruction.Argument1Value, instruction.Argument1Displacement);

            if ((uint)value1 == 0x80000000 || value2 == 0)
            {
                cpu.Interrupt(0);
                return;
            }

            var quotient = value1 / value2;
            var remainder = value1 % value2;

            if ((quotient & 0xFFFF) != quotient)
            {
                cpu.Interrupt(0);
                return;
            }

            cpu.SetRegister(Register.AX, (ushort)quotient);
            cpu.SetRegister(Register.DX, (ushort)remainder);
        }
        private static void DispatchPush([NotNull] Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            switch (instruction.Argument1)
            {
                case (int)Register.SP:
                    // 8086 has a bug where it pushes SP after it has been modified
                    // cpu.registers[(int)Register.SP] -= 2;
                    // cpu.WriteU16(SegmentToAddress(cpu.GetRegister(Register.SS), cpu.GetRegister(Register.SP)), cpu.GetRegister(Register.SP));
                    // break;
                case (int)Register.AX:
                case (int)Register.CX:
                case (int)Register.DX:
                case (int)Register.BX:
                case (int)Register.BP:
                case (int)Register.SI:
                case (int)Register.DI:
                case (int)Register.IP:
                case (int)Register.CS:
                case (int)Register.DS:
                case (int)Register.ES:
                case (int)Register.SS:
                case unchecked((int)Register.FLAGS):
                    cpu.Push(cpu.GetRegister((Register)instruction.Argument1));
                    break;

                case OpCodeManager.ARG_DEREFERENCE:
                case OpCodeManager.ARG_MEMORY:
                    var address = cpu.GetInstructionRealAddress(instruction.SegmentPrefix, instruction.Argument1, instruction.Argument1Value, instruction.Argument1Displacement);
                    cpu.Push(cpu.ReadU16(address));
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        private static void DispatchPop([NotNull] Cpu8086 cpu, OpCodeManager.Instruction instruction)
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
                //case unchecked((int)Register.FLAGS):
                    cpu.SetRegister((Register)instruction.Argument1, cpu.Pop());
                    break;
                case unchecked((int)Register.FLAGS):
                    cpu.SetRegister((Register)instruction.Argument1, cpu.Pop());
                    break;

                case OpCodeManager.ARG_DEREFERENCE:
                case OpCodeManager.ARG_MEMORY:
                    var address = cpu.GetInstructionRealAddress(instruction.SegmentPrefix, instruction.Argument1, instruction.Argument1Value, instruction.Argument1Displacement);
                    cpu.WriteU16(address, cpu.Pop());
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static void DispatchClock([NotNull] Cpu8086 cpu)
        {
            switch (cpu.GetRegisterU8(Register.AH))
            {
                case 0x00:
                    DispatchGetClock(cpu);
                    break;
                case 0x02:
                    DispatchGetRTC(cpu);
                    break;
                case 0x04:
                    DispatchGetDate(cpu);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        private static void DispatchGetClock([NotNull] Cpu8086 cpu)
        {
            var date = DateTime.Now;
            var time = date - DateTime.Today;
            var timePercentage = (double)TimeSpan.TicksPerDay / time.Ticks;
            var biosTicks = (uint)(timePercentage * 0x1800B0);

            cpu.SetRegister(Register.DX, (ushort)biosTicks);
            cpu.SetRegister(Register.CX, (ushort)(biosTicks >> 16));
            cpu.SetRegisterU8(Register.AH, (byte)(date.DayOfYear != cpu.lastBiosTime.DayOfYear ? 1 : 0));
            cpu.lastBiosTime = date;
        }
        private static void DispatchGetRTC([NotNull] Cpu8086 cpu)
        {
            var time = DateTime.Now;
            cpu.SetRegisterU8(Register.CH, (byte)time.Hour);
            cpu.SetRegisterU8(Register.CL, (byte)time.Minute);
            cpu.SetRegisterU8(Register.DH, (byte)time.Second);
            cpu.SetRegisterU8(Register.DL, (byte)(time.IsDaylightSavingTime() ? 1 : 0));
        }
        private static void DispatchGetDate([NotNull] Cpu8086 cpu)
        {
            var time = DateTime.Now;
            cpu.SetRegisterU8(Register.CH, (byte)(time.Year / 100));
            cpu.SetRegisterU8(Register.CL, (byte)(time.Year % 100));
            cpu.SetRegisterU8(Register.DH, (byte)time.Month);
            cpu.SetRegisterU8(Register.DL, (byte)time.Day);
        }

        private static string OutputInstruction(OpCodeManager.Instruction instruction)
        {
            var output = instruction.Type.ToString();
            var arg1 = OutputArgument(instruction.SegmentPrefix, instruction.Flag, instruction.Argument1, instruction.Argument1Value, instruction.Argument1Displacement);
            var arg2 = OutputArgument(instruction.SegmentPrefix, instruction.Flag, instruction.Argument2, instruction.Argument2Value, instruction.Argument2Displacement);

            if (arg1 == null)
                return output;
            return arg2 == null ? $"{output} {arg1}" : $"{output} {arg1}, {arg2}";
        }
        private static string OutputArgument(Register segmentPrefix, OpCodeManager.OpCodeFlag flag, int argument, int argumentValue, int argumentDisplacement)
        {
            if (argument == OpCodeManager.ARG_NONE)
                return null;
            switch (argument)
            {
                case (int)Register.AX:
                    return "AX";
                case (int)Register.CX:
                    return "CX";
                case (int)Register.DX:
                    return "DX";
                case (int)Register.BX:
                    return "BX";
                case (int)Register.SP:
                    return "SP";
                case (int)Register.BP:
                    return "BP";
                case (int)Register.SI:
                    return "SI";
                case (int)Register.DI:
                    return "DI";
                case (int)Register.IP:
                    return "IP";
                case (int)Register.CS:
                    return "CS";
                case (int)Register.DS:
                    return "DS";
                case (int)Register.ES:
                    return "ES";
                case (int)Register.SS:
                    return "SS";
                case unchecked((int)Register.FLAGS):
                    return "FLAGS";

                case OpCodeManager.ARG_BYTE_REGISTER:
                    switch ((Register)argumentValue)
                    {
                        case Register.AL:
                            return "AL";
                        case Register.CL:
                            return "CL";
                        case Register.DL:
                            return "DL";
                        case Register.BL:
                            return "BL";
                        case Register.AH:
                            return "AH";
                        case Register.CH:
                            return "CH";
                        case Register.DH:
                            return "DH";
                        case Register.BH:
                            return "BH";
                        default:
                            throw new NotImplementedException();
                    }
                case OpCodeManager.ARG_DEREFERENCE:
                    string value;
                    switch (argumentValue)
                    {
                        case 0:
                            value = "BX+SI";
                            break;
                        case 1:
                            value = "BX+DI";
                            break;
                        case 2:
                            value = "BP+SI";
                            break;
                        case 3:
                            value = "BP+DI";
                            break;
                        case 4:
                            value = "SI";
                            break;
                        case 5:
                            value = "DI";
                            break;
                        case 6:
                            value = "BP";
                            break;
                        case 7:
                            value = "BX";
                            break;
                        default:
                            throw new NotImplementedException();
                    }
                    switch (segmentPrefix)
                    {
                        case Register.Invalid:
                            return argumentDisplacement < 0 ? $"[{value}{argumentDisplacement}]" : $"[{value}+{argumentDisplacement}]";
                        case Register.ES:
                            return argumentDisplacement < 0 ? $"[ES:{value}{argumentDisplacement}]" : $"[ES:{value}+{argumentDisplacement}]";
                        case Register.CS:
                            return argumentDisplacement < 0 ? $"[CS:{value}{argumentDisplacement}]" : $"[CS:{value}+{argumentDisplacement}]";
                        case Register.SS:
                            return argumentDisplacement < 0 ? $"[SS:{value}{argumentDisplacement}]" : $"[SS:{value}+{argumentDisplacement}]";
                        case Register.DS:
                            return argumentDisplacement < 0 ? $"[DS:{value}{argumentDisplacement}]" : $"[DS:{value}+{argumentDisplacement}]";
                        default:
                            throw new NotImplementedException();
                    }
                case OpCodeManager.ARG_MEMORY:
                    switch (segmentPrefix)
                    {
                        case Register.Invalid:
                            return $"[{argumentValue:X4}]";
                        case Register.ES:
                            return $"[ES:{argumentValue:X4}]";
                        case Register.CS:
                            return $"[CS:{argumentValue:X4}]";
                        case Register.SS:
                            return $"[SS:{argumentValue:X4}]";
                        case Register.DS:
                            return $"[DS:{argumentValue:X4}]";
                        default:
                            throw new NotImplementedException();
                    }
                case OpCodeManager.ARG_FAR_MEMORY:
                    var segment = (uint)argumentValue >> 16;
                    var address = argumentValue & 0xFFFF;
                    return $"[{segment:X4}:{address:X4}]";
                case OpCodeManager.ARG_CONSTANT:
                    return flag.Has(OpCodeManager.OpCodeFlag.Size8) ? $"{argumentValue:X2}" : $"{argumentValue:X4}";
                default:
                    throw new NotImplementedException();
            }
        }

        private static uint SegmentToAddress(ushort segment, ushort offset) => (uint)((segment << 4) + offset);

        public ushort GetRegister(Register register)
        {
            if (((uint)register & 0x80000000) == 0)
                return registers[(int)register];
            switch (register)
            {
                case Register.AL:
                    return (byte)(GetRegister(Register.AX) & 0xFF);
                case Register.CL:
                    return (byte)(GetRegister(Register.CX) & 0xFF);
                case Register.DL:
                    return (byte)(GetRegister(Register.DX) & 0xFF);
                case Register.BL:
                    return (byte)(GetRegister(Register.BX) & 0xFF);
                case Register.AH:
                    return (byte)((GetRegister(Register.AX) >> 8) & 0xFF);
                case Register.CH:
                    return (byte)((GetRegister(Register.CX) >> 8) & 0xFF);
                case Register.DH:
                    return (byte)((GetRegister(Register.DX) >> 8) & 0xFF);
                case Register.BH:
                    return (byte)((GetRegister(Register.BX) >> 8) & 0xFF);

                case Register.FLAGS:
                    return (ushort)((ushort)flags | 0xF002);

                default:
                    throw new ArgumentOutOfRangeException(nameof(register));
            }
        }
        public byte GetRegisterU8(Register register) => (byte)GetRegister(register);
        public FlagsRegister GetFlags() => (FlagsRegister)GetRegister(Register.FLAGS);

        public void SetRegister(Register register, ushort value)
        {
            if (((uint)register & 0x80000000) == 0)
                registers[(int)register] = value;
            else
            {
                switch (register)
                {
                    case Register.AL:
                        registers[(int)Register.AX] = (ushort)((GetRegister(Register.AX) & 0xFF00) | value);
                        break;
                    case Register.CL:
                        registers[(int)Register.CX] = (ushort)((GetRegister(Register.CX) & 0xFF00) | value);
                        break;
                    case Register.DL:
                        registers[(int)Register.DX] = (ushort)((GetRegister(Register.DX) & 0xFF00) | value);
                        break;
                    case Register.BL:
                        registers[(int)Register.BX] = (ushort)((GetRegister(Register.BX) & 0xFF00) | value);
                        break;
                    case Register.AH:
                        registers[(int)Register.AX] = (ushort)((GetRegister(Register.AX) & 0x00FF) | (value << 8));
                        break;
                    case Register.CH:
                        registers[(int)Register.CX] = (ushort)((GetRegister(Register.CX) & 0x00FF) | (value << 8));
                        break;
                    case Register.DH:
                        registers[(int)Register.DX] = (ushort)((GetRegister(Register.DX) & 0x00FF) | (value << 8));
                        break;
                    case Register.BH:
                        registers[(int)Register.BX] = (ushort)((GetRegister(Register.BX) & 0x00FF) | (value << 8));
                        break;

                    case Register.FLAGS:
                        flags = (FlagsRegister)value & FLAGS_MASK;
                        break;

                    default:
                        throw new ArgumentOutOfRangeException(nameof(register));
                }
            }
        }
        public void SetRegisterU8(Register register, byte value) => SetRegister(register, value);
        public void SetFlags(FlagsRegister value) => SetRegister(Register.FLAGS, (ushort)value);

        public byte ReadU8(uint address)
        {
            return _ram[(int)address];
        }
        public ushort ReadU16(uint address) => (ushort)(ReadU8(address) | ReadU8(address + 1) << 8);
        public byte[] ReadBytes(uint address, uint size)
        {
            var buffer = new byte[size];
            for (var i = 0u; i < size; i++)
                buffer[i] = ReadU8(address + i);
            return buffer;
        }
        public void WriteU8(uint address, byte value)
        {
            _ram[(int) address] = value;
        }
        public void WriteU16(uint address, ushort value)
        {
            WriteU8(address, (byte)(value & 0xFF));
            WriteU8(address + 1, (byte)(value >> 8 & 0xFF));
        }
        public void WriteBytes(uint address, byte[] value)
        {
            for (var i = 0u; i < value.Length; i++)
                WriteU8(address + i, value[i]);
        }

        public void AttachPortDevice(IPortDevice device)
        {
            foreach (var port in device.Ports)
            {
                if (_portDevices[port] != null)
                    throw new InvalidOperationException($"Conflicting port: 0x{port.ToString("X")}");
                _portDevices[port] = device;
            }
        }

        public IPortDevice[] GetPortDevices()
        {
            return _portDevices;
        }

        public IPortDevice GetPortDevice(int port)
        {
            return _portDevices[port];
        }

        byte IInstructionFetcher.FetchU8()
        {
            var value = ReadU8(SegmentToAddress(GetRegister(Register.CS), GetRegister(Register.IP)));
            registers[(int)Register.IP] += 1;
            return value;
        }
        ushort IInstructionFetcher.FetchU16()
        {
            var value = ReadU16(SegmentToAddress(GetRegister(Register.CS), GetRegister(Register.IP)));
            registers[(int)Register.IP] += 2;
            return value;
        }
    }
}
