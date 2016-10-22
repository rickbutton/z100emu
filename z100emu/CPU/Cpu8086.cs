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
using System.Threading;
using JetBrains.Annotations;
using z100emu.Core;
using z100emu.CPU;
using z100emu.CPU.Instructions;
using z100emu.Peripheral;
using z100emu.Ram;

namespace z100emu.CPU
{
    public delegate void DebugLineEventHandler(string line);

    [PublicAPI]
    public partial class Cpu8086 : ICpu
    {
        private static readonly IInstruction[] instructions =
        {
            new Invalid(),

            new Arithmetic(),
            new Push(),
            new Pop(),
            new Arithmetic(),
            new Arithmetic(),
            new Arithmetic(),
            new Arithmetic(),
            new Arithmetic(),
            new Arithmetic(),
            new Arithmetic(),
            new Invalid(),
            new Daa(),
            new Das(),
            new Aaa(),
            new Aas(),
            new UnaryArithmetic(),
            new UnaryArithmetic(),
            new JumpRelative(),
            new Jump(),
            new FarJump(),
            new Invalid(),
            new Arithmetic(),
            new Exchange(),
            new Move(),
            new Lea(),
            new Cbw(),
            new Cwd(),
            new CallNearRelative(),
            new CallNear(),
            new CallFar(),
            new Wait(),
            new Sahf(),
            new Lahf(),
            new StringOperation(),
            new StringOperation(),
            new StringOperation(),
            new StringOperation(),
            new StringOperation(),
            new ReturnNear(),
            new ReturnFar(),
            new LoadFarPointer(Register.ES), 
            new LoadFarPointer(Register.DS), 
            new Interrupt(),
            new Into(),
            new ReturnInterrupt(),
            new Aam(),
            new Aad(),
            new Xlat(),
            new LoopNotZero(),
            new LoopZero(),
            new Loop(),
            new Clc(),
            new Stc(),
            new Jcxz(),
            new In(),
            new Out(),
            new Halt(),
            new Cmc(),
            new Cli(),
            new Sti(),
            new Cld(),
            new Std(),
            new JumpIfOverflow(),
            new JumpIfNotOverflow(),
            new JumpIfCarry(),
            new JumpIfNotCarry(),
            new JumpIfZero(),
            new JumpIfNotZero(),
            new JBE(),
            new JA(),
            new JS(),
            new JNS(),
            new JPE(),
            new JPO(),
            new JL(),
            new JGE(),
            new JLE(),
            new JG(),
            new Arithmetic(),
            new Arithmetic(),
            new Arithmetic(),
            new Arithmetic(),
            new Arithmetic(),
            new Arithmetic(),
            new Arithmetic(),
            new UnaryArithmetic(),
            new UnaryArithmetic(),
            new Multiply(),
            new Multiply(),
            new Divide(),
            new SignedDivide(),
            new Escape(),
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

        public FlagsRegister Flags;

        private IRam _ram;
        private IPortDevice[] _portDevices;

        private Intel8259 _pic;

        public DebugLineEventHandler DebugLineEmitted { get; set; }

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
                Interrupt((byte)i.Value, false);
            }

            var fetcher = new Fetcher8086(this);
            var instruction = OpCodeManager.Decode(fetcher);
            IncRegister(Register.IP, fetcher.GetBytesFetched());

            if (instruction.Type == OpCodeManager.InstructionType.Invalid)
                return 0;

            if (debug)
            {
                string instructionText = $"{GetRegister(Register.CS):X4}:{GetRegister(Register.IP):X4} ";
                instructionText += InstructionStringHelper.OutputInstruction(instruction);
                DebugLineEmitted?.Invoke(instructionText);
                Console.WriteLine(instructionText);
            }

            instructions[(int)instruction.Type].Dispatch(this, instruction);

            return instruction.Clocks;
        }

        internal void Push(ushort value)
        {
            registers[(int)Register.SP] -= 2;
            WriteU16(InstructionHelper.SegmentToAddress(GetRegister(Register.SS), GetRegister(Register.SP)), value);
        }

        internal ushort Pop()
        {
            var value = ReadU16(InstructionHelper.SegmentToAddress(GetRegister(Register.SS), GetRegister(Register.SP)));
            registers[(int)Register.SP] += 2;
            return value;
        }

        public void Interrupt(byte interrupt)
        {
            Interrupt(interrupt, false);
        }
        
        public void Interrupt(byte interrupt, bool soft)
        {
            if (!(GetFlags().HasFlag(FlagsRegister.Interrupt) || soft))
                return;

            int size = 4;
            int intBase;

            if (interrupt != 6)
                Console.WriteLine(interrupt.ToString("X"));
            if (interrupt > 15)
            {
                intBase = 0;
            }
            else
            {
                size = _pic.GetInterruptVectorSize(interrupt);
                intBase = _pic.GetInterruptVectorBase(interrupt);
                _pic.AckInterrupt();
            }

            var newIP = ReadU16((uint) ((intBase + interrupt)*size));
            var newCS = ReadU16((uint) (((intBase + interrupt)*size) + 2));

            Push(GetRegister(Register.FLAGS));

            if (interrupt > 15)
                SetFlags(GetFlags() & ~FlagsRegister.Interrupt);

            Push(GetRegister(Register.CS));
            Push(GetRegister(Register.IP));
            SetRegister(Register.IP, newIP);
            SetRegister(Register.CS, newCS);

        }

        public void IncRegister(Register register, int i = 1)
        {
            SetRegister(register, (ushort)(GetRegister(register) + i)); 
        }

        public void DecRegister(Register register, int i = 1)
        {
            SetRegister(register, (ushort)(GetRegister(register) - i)); 
        }

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
                    return (ushort)((ushort)Flags | 0xF002);

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
                        Flags = (FlagsRegister)value & FLAGS_MASK;
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
            if (port > _portDevices.Length - 1)
                return null;
            else
                return _portDevices[port];
        }
    }
}
