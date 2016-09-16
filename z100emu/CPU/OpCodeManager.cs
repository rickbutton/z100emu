#region License
// // The MIT License (MIT)
// // 
// // Copyright (c) 2016 Rick Button
// // 
// // Permission is hereby granted, free of charge, to any person obtaining a copy
// // of this software and associated documentation files (the "Software"), to deal
// // in the Software without restriction, including without limitation the rights
// // to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// // copies of the Software, and to permit persons to whom the Software is
// // furnished to do so, subject to the following conditions:
// // 
// // The above copyright notice and this permission notice shall be included in all
// // copies or substantial portions of the Software.
// // 
// // THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// // IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// // FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// // AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// // LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// // OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// // SOFTWARE.
#endregion

using System;
using System.Diagnostics;
using JetBrains.Annotations;

namespace z100emu.CPU
{
    internal static class OpCodeManager
    {
        public enum InstructionType
        {
            Invalid,

            Add,
            Push,
            Pop,
            Or,
            Adc,
            Sbb,
            And,
            Subtract,
            Xor,
            Compare,
            Prefix,
            Daa,
            Das,
            Aaa,
            Aas,
            Increment,
            Decrement,
            JumpRelative,
            Jump,
            JumpFar,
            Group,
            Test,
            Xchg,
            Move,
            Lea,
            Cbw,
            Cwd,
            CallNearRelative,
            CallNear,
            CallFar,
            Wait,
            Sahf,
            Lahf,
            Movs,
            Cmps,
            Stos,
            Lods,
            Scas,
            ReturnNear,
            ReturnFar,
            Les,
            Lds,
            Int,
            Into,
            ReturnInterrupt,
            Aam,
            Aad,
            Xlat,
            Loopnz,
            Loopz,
            Loop,
            Clc,
            Stc,
            Jcxz,
            In,
            Out,
            Hlt,
            Cmc,
            Cli,
            Sti,
            Cld,
            Std,
            JO,
            JNO,
            JB,
            JNB,
            JZ,
            JNZ,
            JBE,
            JA,
            JS,
            JNS,
            JPE,
            JPO,
            JL,
            JGE,
            JLE,
            JG,
            Sar,
            Shr,
            Shl,
            Rcr,
            Rcl,
            Ror,
            Rol,
            Not,
            Negate,
            Multiply,
            SignedMultiply,
            Divide,
            SignedDivide,
            EmulatorSpecial
        }

        [Flags]
        public enum OpCodeFlag : byte
        {
            None = 0,
            Size8 = 1 << 0,
            HasRM = 1 << 1,
            Signed = 1 << 2
        }

        private struct ClockDef
        {
            public int BaseClocks;
            public int NoJumpClocks;
            public int Transfers;
            public bool EA;

            public ClockDef(int baseClock, int noJumpClocks, int transfers, bool ea)
            {
                BaseClocks = baseClock;
                NoJumpClocks = noJumpClocks;
                Transfers = transfers;
                EA = ea;
            }

            public ClockDef(int baseClock, int transfers, bool ea)
            {
                BaseClocks = baseClock;
                NoJumpClocks = baseClock;
                Transfers = transfers;
                EA = ea;
            }

            public ClockDef(int baseClock)
            {
                BaseClocks = baseClock;
                NoJumpClocks = baseClock;
                Transfers = 0;
                EA = false;
            }
        }

        private struct OpCode
        {
            public readonly InstructionType Type;
            public readonly OpCodeFlag Flag;
            public readonly int Argument1Type;
            public readonly int Argument2Type;

            public readonly Func<Instruction, ClockDef> ClockFunc;

            public OpCode(InstructionType type, OpCodeFlag flag, int argument1Type, int argument2Type, int baseClocks, int noJumpClocks, int transfers, bool ea)
            {
                Type = type;
                Flag = flag;
                Argument1Type = argument1Type;
                Argument2Type = argument2Type;
                ClockFunc = (isnt) =>
                {
                    var c = new ClockDef();
                    c.BaseClocks = baseClocks;
                    c.NoJumpClocks = noJumpClocks;
                    c.Transfers = transfers;
                    c.EA = ea;
                    return c;
                };
            }

            public OpCode(InstructionType type, OpCodeFlag flag, int argument1Type, int argument2Type, Func<Instruction, ClockDef> clockFunc)
            {
                Type = type;
                Flag = flag;
                Argument1Type = argument1Type;
                Argument2Type = argument2Type;
                ClockFunc = clockFunc;
            }

            public OpCode(InstructionType type, OpCodeFlag flag, int argument1Type, int argument2Type, int baseClocks, int transfers, bool ea) : 
                this(type, flag, argument1Type, argument2Type, baseClocks, baseClocks, transfers, ea) { }

            //public OpCode(InstructionType type, OpCodeFlag flag, int argument1Type, int argument2Type, int baseClocks) :
            //                this(type, flag, argument1Type, argument2Type, baseClocks, baseClocks, baseClocks, false) { }
        }

        private static bool ArgumentIsRegister(int argument)
        {
            return argument == ARG_BYTE_REGISTER || (argument >= 0 && argument <= 12);
        }

        private static bool ArgumentIsSegment(int argument)
        {
            return argument == ARG_CS || argument == ARG_SS || argument == ARG_ES || argument == ARG_DS;
        }

        private static bool ArgumentIsMemory(int argument)
        {
            return argument == ARG_MEMORY || argument == ARG_DEREFERENCE;
        }

        private static ClockDef ArithmeticClockDef(Instruction i)
        {
            if (ArgumentIsRegister(i.Argument1) && ArgumentIsRegister(i.Argument2)) { return new ClockDef(3); }
            else if (ArgumentIsRegister(i.Argument1) && ArgumentIsMemory(i.Argument2)) { return new ClockDef(5, 1, true); }
            else if (ArgumentIsMemory(i.Argument1) && ArgumentIsRegister(i.Argument2)) { return new ClockDef(8, 2, true); }
            else if (ArgumentIsRegister(i.Argument1) && i.Argument2 == ARG_CONSTANT) { return new ClockDef(4); }
            else if (ArgumentIsMemory(i.Argument1) && i.Argument2 == ARG_CONSTANT) { return new ClockDef(9, 2, true); }
            throw new NotImplementedException();
        }

        private static ClockDef CompareClockDef(Instruction i)
        {
            if (ArgumentIsRegister(i.Argument1) && ArgumentIsRegister(i.Argument2)) { return new ClockDef(3); }
            else if (ArgumentIsRegister(i.Argument1) && ArgumentIsMemory(i.Argument2)) { return new ClockDef(5, 1, true); }
            else if (ArgumentIsMemory(i.Argument1) && ArgumentIsRegister(i.Argument2)) { return new ClockDef(5, 1, true); }
            else if (ArgumentIsRegister(i.Argument1) && i.Argument2 == ARG_CONSTANT) { return new ClockDef(4); }
            else if (ArgumentIsMemory(i.Argument1) && i.Argument2 == ARG_CONSTANT) { return new ClockDef(6, 1, true); }
            throw new NotImplementedException();
        }

        private static ClockDef TestClockDef(Instruction i)
        {
            if (ArgumentIsRegister(i.Argument1) && ArgumentIsRegister(i.Argument2)) { return new ClockDef(3); }
            else if (ArgumentIsRegister(i.Argument1) && ArgumentIsMemory(i.Argument2)) { return new ClockDef(5, 1, true); }
            else if (ArgumentIsRegister(i.Argument1) && i.Argument2 == ARG_CONSTANT) { return new ClockDef(5); }
            else if (ArgumentIsMemory(i.Argument1) && i.Argument2 == ARG_CONSTANT) { return new ClockDef(11, 0, true); }
            throw new NotImplementedException();
        }

        private static ClockDef XchgClockDef(Instruction i)
        {
            if (ArgumentIsMemory(i.Argument1) && ArgumentIsRegister(i.Argument2)) { return new ClockDef(9, 2, true); }
            if (ArgumentIsRegister(i.Argument1) && ArgumentIsRegister(i.Argument2)) { return new ClockDef(4); }
            if (ArgumentIsRegister(i.Argument1) && ArgumentIsMemory(i.Argument2)) { return new ClockDef(9, 2, true); }
            throw new NotImplementedException();
        }

        private static ClockDef MoveClockDef(Instruction i)
        {
            if (i.Argument1 == ARG_MEMORY && i.Argument2 == ARG_AX) { return new ClockDef(6, 1, true); }
            else if (i.Argument1 == ARG_AX && i.Argument2 == ARG_MEMORY) { return new ClockDef(6, 1, true); }
            else if (ArgumentIsRegister(i.Argument1) && ArgumentIsRegister(i.Argument2)) { return new ClockDef(2); }
            else if (ArgumentIsRegister(i.Argument1) && (i.Argument2 == ARG_MEMORY || i.Argument2 == ARG_DEREFERENCE)) { return new ClockDef(4, 1, true); }
            else if ((i.Argument1 == ARG_MEMORY || i.Argument1 == ARG_DEREFERENCE) && ArgumentIsRegister(i.Argument2)) { return new ClockDef(5, 1, true); }
            else if (ArgumentIsRegister(i.Argument1) && i.Argument2 == ARG_CONSTANT) { return new ClockDef(4); }
            else if (ArgumentIsMemory(i.Argument1) && i.Argument2 == ARG_CONSTANT) { return new ClockDef(6, 1, true); }
            throw new NotImplementedException();
        }

        private static ClockDef ShiftRotateClockDef(Instruction i)
        {
            if (ArgumentIsRegister(i.Argument1) && i.Argument2 == ARG_CONSTANT && i.Argument2Value == 1) { return new ClockDef(2); }
            if (ArgumentIsRegister(i.Argument1) && i.Argument2 == ARG_BYTE_REGISTER && i.Argument2Value == ARG_CL) { return new ClockDef(24); }
            if (ArgumentIsMemory(i.Argument1) && i.Argument2 == ARG_CONSTANT && i.Argument2Value == 1) { return new ClockDef(7, 2, true); }
            if (ArgumentIsMemory(i.Argument1) && i.Argument2 == ARG_BYTE_REGISTER && i.Argument2Value == ARG_CL) { return new ClockDef(28, 2, true); }
            throw new NotImplementedException();
        }

        private static ClockDef NotClockDef(Instruction i)
        {
            if (ArgumentIsRegister(i.Argument1)) { return new ClockDef(3); }
            if (ArgumentIsMemory(i.Argument1)) { return new ClockDef(8, 2, true); }
            throw new NotImplementedException();
        }

        private static ClockDef MultiplyClockDef(Instruction i)
        {
            if (ArgumentIsRegister(i.Argument1) && i.Flag.Has(OpCodeFlag.Size8)) { return new ClockDef(77); }
            if (ArgumentIsRegister(i.Argument1) && !i.Flag.Has(OpCodeFlag.Size8)) { return new ClockDef(118); }
            if (ArgumentIsMemory(i.Argument1)   && i.Flag.Has(OpCodeFlag.Size8)) { return new ClockDef(79, 1, true); }
            if (ArgumentIsMemory(i.Argument1)   && !i.Flag.Has(OpCodeFlag.Size8)) { return new ClockDef(135, 1, true); }
            throw new NotImplementedException();
        }

        private static ClockDef SignedMultiplyClockDef(Instruction i)
        {
            if (ArgumentIsRegister(i.Argument1) && i.Flag.Has(OpCodeFlag.Size8)) { return new ClockDef(98); }
            if (ArgumentIsRegister(i.Argument1) && !i.Flag.Has(OpCodeFlag.Size8)) { return new ClockDef(154); }
            if (ArgumentIsMemory(i.Argument1)   && i.Flag.Has(OpCodeFlag.Size8)) { return new ClockDef(100, 1, true); }
            if (ArgumentIsMemory(i.Argument1)   && !i.Flag.Has(OpCodeFlag.Size8)) { return new ClockDef(156, 1, true); }
            throw new NotImplementedException();
        }

        private static ClockDef DivideClockDef(Instruction i)
        {
            if (ArgumentIsRegister(i.Argument1) && i.Flag.Has(OpCodeFlag.Size8)) { return new ClockDef(90); }
            if (ArgumentIsRegister(i.Argument1) && !i.Flag.Has(OpCodeFlag.Size8)) { return new ClockDef(162); }
            if (ArgumentIsMemory(i.Argument1)   && i.Flag.Has(OpCodeFlag.Size8)) { return new ClockDef(92, 1, true); }
            if (ArgumentIsMemory(i.Argument1)   && !i.Flag.Has(OpCodeFlag.Size8)) { return new ClockDef(164, 1, true); }
            throw new NotImplementedException();
        }

        private static ClockDef SignedDivideClockDef(Instruction i)
        {
            if (ArgumentIsRegister(i.Argument1) && i.Flag.Has(OpCodeFlag.Size8)) { return new ClockDef(112); }
            if (ArgumentIsRegister(i.Argument1) && !i.Flag.Has(OpCodeFlag.Size8)) { return new ClockDef(184); }
            if (ArgumentIsMemory(i.Argument1)   && i.Flag.Has(OpCodeFlag.Size8)) { return new ClockDef(114, 1, true); }
            if (ArgumentIsMemory(i.Argument1)   && !i.Flag.Has(OpCodeFlag.Size8)) { return new ClockDef(186, 1, true); }
            throw new NotImplementedException();
        }

        private static ClockDef IncDefClockDef(Instruction i)
        {
            if (ArgumentIsRegister(i.Argument1) && i.Flag.Has(OpCodeFlag.Size8)) { return new ClockDef(3); }
            if (ArgumentIsRegister(i.Argument1) && !i.Flag.Has(OpCodeFlag.Size8)) { return new ClockDef(2); }
            if (ArgumentIsMemory(i.Argument1)) { return new ClockDef(7, 2, true); }
            throw new NotImplementedException();
        }

        private static ClockDef CallClockDef(Instruction i)
        {
            if (i.Type == InstructionType.CallNear || i.Type == InstructionType.CallNearRelative) { return new ClockDef(13, 2, true);}
            if (i.Type == InstructionType.CallFar) { return new ClockDef(21, 4, true);}
            throw new NotImplementedException();
        }

        private static ClockDef JumpClockDef(Instruction i)
        {
            if (i.Type == InstructionType.Jump) { return new ClockDef(18, 0, true); }
            if (i.Type == InstructionType.JumpFar) { return new ClockDef(24, 0, true); }
            throw new NotImplementedException();
        }

        private static ClockDef PushClockDef(Instruction i)
        {
            if (i.Type == InstructionType.Push) { return new ClockDef(24, 0, true);}
            throw new NotImplementedException();
        }

        private static ClockDef GroupClockDef(Instruction i)
        {
            if (i.Type == InstructionType.Add || i.Type == InstructionType.Or || i.Type == InstructionType.Adc ||
                i.Type == InstructionType.Sbb || i.Type == InstructionType.And || i.Type == InstructionType.Subtract ||
                i.Type == InstructionType.Xor)
                return ArithmeticClockDef(i);
            else if (i.Type == InstructionType.Compare)
                return CompareClockDef(i);
            else if (i.Type == InstructionType.Shl || i.Type == InstructionType.Shr || i.Type == InstructionType.Rol || i.Type == InstructionType.Ror ||
                i.Type == InstructionType.Rcl || i.Type == InstructionType.Rcr || i.Type == InstructionType.Sar)
                return ShiftRotateClockDef(i);
            else if (i.Type == InstructionType.Test)
                return TestClockDef(i);
            else if (i.Type == InstructionType.Not || i.Type == InstructionType.Negate)
                return NotClockDef(i);
            else if (i.Type == InstructionType.Multiply)
                return MultiplyClockDef(i);
            else if (i.Type == InstructionType.SignedMultiply)
                return SignedMultiplyClockDef(i);
            else if (i.Type == InstructionType.Divide)
                return DivideClockDef(i);
            else if (i.Type == InstructionType.SignedMultiply)
                return SignedDivideClockDef(i);
            else if (i.Type == InstructionType.Increment || i.Type == InstructionType.Decrement)
                return IncDefClockDef(i);
            else if (i.Type == InstructionType.CallNear || i.Type == InstructionType.CallFar)
                return CallClockDef(i);
            else if (i.Type == InstructionType.Jump || i.Type == InstructionType.JumpFar)
                return JumpClockDef(i);
            else if (i.Type == InstructionType.Push)
                return PushClockDef(i);

            throw new NotImplementedException();
        }

        private static readonly OpCode[] opCodes =
        {
            /*0x00*/ new OpCode(InstructionType.Add,			    OpCodeFlag.Size8 | OpCodeFlag.HasRM,    ARG_EB,     ARG_GB,      ArithmeticClockDef),
            /*0x01*/ new OpCode(InstructionType.Add,			    OpCodeFlag.HasRM,                       ARG_EW,     ARG_GW,      ArithmeticClockDef),
            /*0x02*/ new OpCode(InstructionType.Add,			    OpCodeFlag.Size8 | OpCodeFlag.HasRM,    ARG_GB,     ARG_EB,      ArithmeticClockDef),
            /*0x03*/ new OpCode(InstructionType.Add,			    OpCodeFlag.HasRM,                       ARG_GW,     ARG_EW,      ArithmeticClockDef),
            /*0x04*/ new OpCode(InstructionType.Add,			    OpCodeFlag.Size8,                       ARG_AL,     ARG_IB,      4,    0,    false),
            /*0x05*/ new OpCode(InstructionType.Add,			    OpCodeFlag.None,                        ARG_AX,     ARG_IW,      4,    0,    false),
            /*0x06*/ new OpCode(InstructionType.Push,			    OpCodeFlag.None,                        ARG_ES,     ARG_NONE,    6,    1,    false),
            /*0x07*/ new OpCode(InstructionType.Pop,			    OpCodeFlag.None,                        ARG_ES,     ARG_NONE,    4,    1,    false),
            /*0x08*/ new OpCode(InstructionType.Or,			        OpCodeFlag.Size8 | OpCodeFlag.HasRM,    ARG_EB,     ARG_GB,      ArithmeticClockDef),
            /*0x09*/ new OpCode(InstructionType.Or,			        OpCodeFlag.HasRM,                       ARG_EW,     ARG_GW,      ArithmeticClockDef),
            /*0x0A*/ new OpCode(InstructionType.Or,			        OpCodeFlag.Size8 | OpCodeFlag.HasRM,    ARG_GB,     ARG_EB,      ArithmeticClockDef),
            /*0x0B*/ new OpCode(InstructionType.Or,			        OpCodeFlag.HasRM,                       ARG_GW,     ARG_EW,      ArithmeticClockDef),
            /*0x0C*/ new OpCode(InstructionType.Or,			        OpCodeFlag.Size8,                       ARG_AL,     ARG_IB,      4,    0,    false),
            /*0x0D*/ new OpCode(InstructionType.Or,			        OpCodeFlag.None,                        ARG_AX,     ARG_IW,      4,    0,    false),
            /*0x0E*/ new OpCode(InstructionType.Push,			    OpCodeFlag.None,                        ARG_CS,     ARG_NONE,    6,    1,    false),
            /*0x0F*/ new OpCode(InstructionType.EmulatorSpecial,	OpCodeFlag.None,                        ARG_IB,     ARG_NONE,    0,    0,    false),
            /*0x10*/ new OpCode(InstructionType.Adc,			    OpCodeFlag.Size8 | OpCodeFlag.HasRM,    ARG_EB,     ARG_GB,      ArithmeticClockDef),
            /*0x11*/ new OpCode(InstructionType.Adc,			    OpCodeFlag.HasRM,                       ARG_EW,     ARG_GW,      ArithmeticClockDef),
            /*0x12*/ new OpCode(InstructionType.Adc,			    OpCodeFlag.Size8 | OpCodeFlag.HasRM,    ARG_GB,     ARG_EB,      ArithmeticClockDef),
            /*0x13*/ new OpCode(InstructionType.Adc,			    OpCodeFlag.HasRM,                       ARG_GW,     ARG_EW,      ArithmeticClockDef),
            /*0x14*/ new OpCode(InstructionType.Adc,			    OpCodeFlag.Size8,                       ARG_AL,     ARG_IB,      4,    0,    false),
            /*0x15*/ new OpCode(InstructionType.Adc,			    OpCodeFlag.None,                        ARG_AX,     ARG_IW,      4,    0,    false),
            /*0x16*/ new OpCode(InstructionType.Push,			    OpCodeFlag.None,                        ARG_SS,     ARG_NONE,    6,    1,    false),
            /*0x17*/ new OpCode(InstructionType.Pop,			    OpCodeFlag.None,                        ARG_SS,     ARG_NONE,    4,    1,    false),
            /*0x18*/ new OpCode(InstructionType.Sbb,			    OpCodeFlag.Size8 | OpCodeFlag.HasRM,    ARG_EB,     ARG_GB,      ArithmeticClockDef),
            /*0x19*/ new OpCode(InstructionType.Sbb,			    OpCodeFlag.HasRM,                       ARG_EW,     ARG_GW,      ArithmeticClockDef),
            /*0x1A*/ new OpCode(InstructionType.Sbb,			    OpCodeFlag.Size8 | OpCodeFlag.HasRM,    ARG_GB,     ARG_EB,      ArithmeticClockDef),
            /*0x1B*/ new OpCode(InstructionType.Sbb,			    OpCodeFlag.HasRM,                       ARG_GW,     ARG_EW,      ArithmeticClockDef),
            /*0x1C*/ new OpCode(InstructionType.Sbb,			    OpCodeFlag.Size8,                       ARG_AL,     ARG_IB,      4,    0,    false),
            /*0x1D*/ new OpCode(InstructionType.Sbb,			    OpCodeFlag.None,                        ARG_AX,     ARG_IW,      4,    0,    false),
            /*0x1E*/ new OpCode(InstructionType.Push,			    OpCodeFlag.None,                        ARG_DS,     ARG_NONE,    6,    1,    false),
            /*0x1F*/ new OpCode(InstructionType.Pop,			    OpCodeFlag.None,                        ARG_DS,     ARG_NONE,    4,    1,    false),
            /*0x20*/ new OpCode(InstructionType.And,			    OpCodeFlag.Size8 | OpCodeFlag.HasRM,	ARG_EB,     ARG_GB,      ArithmeticClockDef),
            /*0x21*/ new OpCode(InstructionType.And,			    OpCodeFlag.HasRM,					    ARG_EW,     ARG_GW,      ArithmeticClockDef),
            /*0x22*/ new OpCode(InstructionType.And,			    OpCodeFlag.Size8 | OpCodeFlag.HasRM,	ARG_GB,     ARG_EB,      ArithmeticClockDef),
            /*0x23*/ new OpCode(InstructionType.And,			    OpCodeFlag.HasRM,					    ARG_GW,     ARG_EW,      ArithmeticClockDef),
            /*0x24*/ new OpCode(InstructionType.And,			    OpCodeFlag.Size8,					    ARG_AL,     ARG_IB,      4,    0,    false),
            /*0x25*/ new OpCode(InstructionType.And,			    OpCodeFlag.None,					    ARG_AX,     ARG_IW,      4,    0,    false),
            /*0x26*/ new OpCode(InstructionType.Prefix,			    OpCodeFlag.None,					    ARG_NONE,   ARG_NONE,    0,    0,    false),
            /*0x27*/ new OpCode(InstructionType.Daa,			    OpCodeFlag.None,					    ARG_NONE,   ARG_NONE,    4,    0,    false),
            /*0x28*/ new OpCode(InstructionType.Subtract,			OpCodeFlag.Size8 | OpCodeFlag.HasRM,	ARG_EB,     ARG_GB,      ArithmeticClockDef),
            /*0x29*/ new OpCode(InstructionType.Subtract,			OpCodeFlag.HasRM,					    ARG_EW,     ARG_GW,      ArithmeticClockDef),
            /*0x2A*/ new OpCode(InstructionType.Subtract,			OpCodeFlag.Size8 | OpCodeFlag.HasRM,	ARG_GB,     ARG_EB,      ArithmeticClockDef),
            /*0x2B*/ new OpCode(InstructionType.Subtract,			OpCodeFlag.HasRM,					    ARG_GW,     ARG_EW,      ArithmeticClockDef),
            /*0x2C*/ new OpCode(InstructionType.Subtract,			OpCodeFlag.Size8,					    ARG_AL,     ARG_IB,      4,    0,    false),
            /*0x2D*/ new OpCode(InstructionType.Subtract,			OpCodeFlag.None,					    ARG_AX,     ARG_IW,      4,    0,    false),
            /*0x2E*/ new OpCode(InstructionType.Prefix,			    OpCodeFlag.None,					    ARG_NONE,   ARG_NONE,    0,    0,    false),
            /*0x2F*/ new OpCode(InstructionType.Das,			    OpCodeFlag.None,					    ARG_NONE,   ARG_NONE,    4,    0,    false),
            /*0x30*/ new OpCode(InstructionType.Xor,			    OpCodeFlag.Size8 | OpCodeFlag.HasRM,	ARG_EB,     ARG_GB,      ArithmeticClockDef),
            /*0x31*/ new OpCode(InstructionType.Xor,			    OpCodeFlag.HasRM,					    ARG_EW,     ARG_GW,      ArithmeticClockDef),
            /*0x32*/ new OpCode(InstructionType.Xor,			    OpCodeFlag.Size8 | OpCodeFlag.HasRM,	ARG_GB,     ARG_EB,      ArithmeticClockDef),
            /*0x33*/ new OpCode(InstructionType.Xor,			    OpCodeFlag.HasRM,					    ARG_GW,     ARG_EW,      ArithmeticClockDef),
            /*0x34*/ new OpCode(InstructionType.Xor,			    OpCodeFlag.Size8,					    ARG_AL,     ARG_IB,      4,    0,    false),
            /*0x35*/ new OpCode(InstructionType.Xor,			    OpCodeFlag.None,					    ARG_AX,     ARG_IW,      4,    0,    false),
            /*0x36*/ new OpCode(InstructionType.Prefix,			    OpCodeFlag.None,					    ARG_NONE,   ARG_NONE,    0,    0,    false),
            /*0x37*/ new OpCode(InstructionType.Aaa,			    OpCodeFlag.None,					    ARG_NONE,   ARG_NONE,    4,    0,    false),
            /*0x38*/ new OpCode(InstructionType.Compare,			OpCodeFlag.Size8 | OpCodeFlag.HasRM,	ARG_EB,     ARG_GB,      CompareClockDef),
            /*0x39*/ new OpCode(InstructionType.Compare,			OpCodeFlag.HasRM,					    ARG_EW,     ARG_GW,      CompareClockDef),
            /*0x3A*/ new OpCode(InstructionType.Compare,			OpCodeFlag.Size8 | OpCodeFlag.HasRM,	ARG_GB,     ARG_EB,      CompareClockDef),
            /*0x3B*/ new OpCode(InstructionType.Compare,			OpCodeFlag.HasRM,					    ARG_GW,     ARG_EW,      CompareClockDef),
            /*0x3C*/ new OpCode(InstructionType.Compare,			OpCodeFlag.Size8,					    ARG_AL,     ARG_IB,      4,    0,    false),
            /*0x3D*/ new OpCode(InstructionType.Compare,			OpCodeFlag.None,					    ARG_AX,     ARG_IW,      4,    0,    false),
            /*0x3E*/ new OpCode(InstructionType.Prefix,			    OpCodeFlag.None,					    ARG_NONE,   ARG_NONE,    0,    0,    false),
            /*0x3F*/ new OpCode(InstructionType.Aas,			    OpCodeFlag.None,					    ARG_NONE,   ARG_NONE,    4,    0,    false),
            /*0x40*/ new OpCode(InstructionType.Increment,			OpCodeFlag.None,					    ARG_AX,     ARG_NONE,    2,    0,    false),
            /*0x41*/ new OpCode(InstructionType.Increment,			OpCodeFlag.None,					    ARG_CX,     ARG_NONE,    2,    0,    false),
            /*0x42*/ new OpCode(InstructionType.Increment,			OpCodeFlag.None,					    ARG_DX,     ARG_NONE,    2,    0,    false),
            /*0x43*/ new OpCode(InstructionType.Increment,			OpCodeFlag.None,					    ARG_BX,     ARG_NONE,    2,    0,    false),
            /*0x44*/ new OpCode(InstructionType.Increment,			OpCodeFlag.None,					    ARG_SP,     ARG_NONE,    2,    0,    false),
            /*0x45*/ new OpCode(InstructionType.Increment,			OpCodeFlag.None,					    ARG_BP,     ARG_NONE,    2,    0,    false),
            /*0x46*/ new OpCode(InstructionType.Increment,			OpCodeFlag.None,					    ARG_SI,     ARG_NONE,    2,    0,    false),
            /*0x47*/ new OpCode(InstructionType.Increment,			OpCodeFlag.None,					    ARG_DI,     ARG_NONE,    2,    0,    false),
            /*0x48*/ new OpCode(InstructionType.Decrement,			OpCodeFlag.None,					    ARG_AX,     ARG_NONE,    3,    0,    false),
            /*0x49*/ new OpCode(InstructionType.Decrement,			OpCodeFlag.None,					    ARG_CX,     ARG_NONE,    3,    0,    false),
            /*0x4A*/ new OpCode(InstructionType.Decrement,			OpCodeFlag.None,					    ARG_DX,     ARG_NONE,    3,    0,    false),
            /*0x4B*/ new OpCode(InstructionType.Decrement,			OpCodeFlag.None,					    ARG_BX,     ARG_NONE,    3,    0,    false),
            /*0x4C*/ new OpCode(InstructionType.Decrement,			OpCodeFlag.None,					    ARG_SP,     ARG_NONE,    3,    0,    false),
            /*0x4D*/ new OpCode(InstructionType.Decrement,			OpCodeFlag.None,					    ARG_BP,     ARG_NONE,    3,    0,    false),
            /*0x4E*/ new OpCode(InstructionType.Decrement,			OpCodeFlag.None,					    ARG_SI,     ARG_NONE,    3,    0,    false),
            /*0x4F*/ new OpCode(InstructionType.Decrement,			OpCodeFlag.None,					    ARG_DI,     ARG_NONE,    3,    0,    false),
            /*0x50*/ new OpCode(InstructionType.Push,			    OpCodeFlag.None,					    ARG_AX,     ARG_NONE,    7,    1,    false),
            /*0x51*/ new OpCode(InstructionType.Push,			    OpCodeFlag.None,					    ARG_CX,     ARG_NONE,    7,    1,    false),
            /*0x52*/ new OpCode(InstructionType.Push,			    OpCodeFlag.None,					    ARG_DX,     ARG_NONE,    7,    1,    false),
            /*0x53*/ new OpCode(InstructionType.Push,			    OpCodeFlag.None,					    ARG_BX,     ARG_NONE,    7,    1,    false),
            /*0x54*/ new OpCode(InstructionType.Push,			    OpCodeFlag.None,					    ARG_SP,     ARG_NONE,    7,    1,    false),
            /*0x55*/ new OpCode(InstructionType.Push,			    OpCodeFlag.None,					    ARG_BP,     ARG_NONE,    7,    1,    false),
            /*0x56*/ new OpCode(InstructionType.Push,			    OpCodeFlag.None,					    ARG_SI,     ARG_NONE,    7,    1,    false),
            /*0x57*/ new OpCode(InstructionType.Push,			    OpCodeFlag.None,					    ARG_DI,     ARG_NONE,    7,    1,    false),
            /*0x58*/ new OpCode(InstructionType.Pop,			    OpCodeFlag.None,					    ARG_AX,     ARG_NONE,    4,    1,    false),
            /*0x59*/ new OpCode(InstructionType.Pop,			    OpCodeFlag.None,					    ARG_CX,     ARG_NONE,    4,    1,    false),
            /*0x5A*/ new OpCode(InstructionType.Pop,			    OpCodeFlag.None,					    ARG_DX,     ARG_NONE,    4,    1,    false),
            /*0x5B*/ new OpCode(InstructionType.Pop,			    OpCodeFlag.None,					    ARG_BX,     ARG_NONE,    4,    1,    false),
            /*0x5C*/ new OpCode(InstructionType.Pop,			    OpCodeFlag.None,					    ARG_SP,     ARG_NONE,    4,    1,    false),
            /*0x5D*/ new OpCode(InstructionType.Pop,			    OpCodeFlag.None,					    ARG_BP,     ARG_NONE,    4,    1,    false),
            /*0x5E*/ new OpCode(InstructionType.Pop,			    OpCodeFlag.None,					    ARG_SI,     ARG_NONE,    4,    1,    false),
            /*0x5F*/ new OpCode(InstructionType.Pop,			    OpCodeFlag.None,					    ARG_DI,     ARG_NONE,    4,    1,    false),
            /*0x60*/ new OpCode(InstructionType.Invalid,			OpCodeFlag.None,					    ARG_NONE,   ARG_NONE,    0,    0,    false),
            /*0x61*/ new OpCode(InstructionType.Invalid,			OpCodeFlag.None,					    ARG_NONE,   ARG_NONE,    0,    0,    false),
            /*0x62*/ new OpCode(InstructionType.Invalid,			OpCodeFlag.None,					    ARG_NONE,   ARG_NONE,    0,    0,    false),
            /*0x63*/ new OpCode(InstructionType.Invalid,			OpCodeFlag.None,					    ARG_NONE,   ARG_NONE,    0,    0,    false),
            /*0x64*/ new OpCode(InstructionType.Invalid,			OpCodeFlag.None,					    ARG_NONE,   ARG_NONE,    0,    0,    false),
            /*0x65*/ new OpCode(InstructionType.Invalid,			OpCodeFlag.None,					    ARG_NONE,   ARG_NONE,    0,    0,    false),
            /*0x66*/ new OpCode(InstructionType.Invalid,			OpCodeFlag.None,					    ARG_NONE,   ARG_NONE,    0,    0,    false),
            /*0x67*/ new OpCode(InstructionType.Invalid,			OpCodeFlag.None,					    ARG_NONE,   ARG_NONE,    0,    0,    false),
            /*0x68*/ new OpCode(InstructionType.Invalid,			OpCodeFlag.None,					    ARG_NONE,   ARG_NONE,    0,    0,    false),
            /*0x69*/ new OpCode(InstructionType.Invalid,			OpCodeFlag.None,					    ARG_NONE,   ARG_NONE,    0,    0,    false),
            /*0x6A*/ new OpCode(InstructionType.Invalid,			OpCodeFlag.None,					    ARG_NONE,   ARG_NONE,    0,    0,    false),
            /*0x6B*/ new OpCode(InstructionType.Invalid,			OpCodeFlag.None,					    ARG_NONE,   ARG_NONE,    0,    0,    false),
            /*0x6C*/ new OpCode(InstructionType.Invalid,			OpCodeFlag.None,					    ARG_NONE,   ARG_NONE,    0,    0,    false),
            /*0x6D*/ new OpCode(InstructionType.Invalid,			OpCodeFlag.None,					    ARG_NONE,   ARG_NONE,    0,    0,    false),
            /*0x6E*/ new OpCode(InstructionType.Invalid,			OpCodeFlag.None,					    ARG_NONE,   ARG_NONE,    0,    0,    false),
            /*0x6F*/ new OpCode(InstructionType.Invalid,			OpCodeFlag.None,					    ARG_NONE,   ARG_NONE,    0,    0,    false),
            /*0x70*/ new OpCode(InstructionType.JO,			        OpCodeFlag.Size8,					    ARG_JB,     ARG_NONE,    16,   4,    0,    false),
            /*0x71*/ new OpCode(InstructionType.JNO,			    OpCodeFlag.Size8,					    ARG_JB,     ARG_NONE,    16,   4,    0,    false),
            /*0x72*/ new OpCode(InstructionType.JB,			        OpCodeFlag.Size8,					    ARG_JB,     ARG_NONE,    16,   4,    0,    false),
            /*0x73*/ new OpCode(InstructionType.JNB,			    OpCodeFlag.Size8,					    ARG_JB,     ARG_NONE,    16,   4,    0,    false),
            /*0x74*/ new OpCode(InstructionType.JZ,			        OpCodeFlag.Size8,					    ARG_JB,     ARG_NONE,    16,   4,    0,    false),
            /*0x75*/ new OpCode(InstructionType.JNZ,			    OpCodeFlag.Size8,					    ARG_JB,     ARG_NONE,    16,   4,    0,    false),
            /*0x76*/ new OpCode(InstructionType.JBE,			    OpCodeFlag.Size8,					    ARG_JB,     ARG_NONE,    16,   4,    0,    false),
            /*0x77*/ new OpCode(InstructionType.JA,			        OpCodeFlag.Size8,					    ARG_JB,     ARG_NONE,    16,   4,    0,    false),
            /*0x78*/ new OpCode(InstructionType.JS,			        OpCodeFlag.Size8,					    ARG_JB,     ARG_NONE,    16,   4,    0,    false),
            /*0x79*/ new OpCode(InstructionType.JNS,		        OpCodeFlag.Size8,					    ARG_JB,     ARG_NONE,    16,   4,    0,    false),
            /*0x7A*/ new OpCode(InstructionType.JPE,		        OpCodeFlag.Size8,					    ARG_JB,     ARG_NONE,    16,   4,    0,    false),
            /*0x7B*/ new OpCode(InstructionType.JPO,		        OpCodeFlag.Size8,					    ARG_JB,     ARG_NONE,    16,   4,    0,    false),
            /*0x7C*/ new OpCode(InstructionType.JL,			        OpCodeFlag.Size8,					    ARG_JB,     ARG_NONE,    16,   4,    0,    false),
            /*0x7D*/ new OpCode(InstructionType.JGE,		        OpCodeFlag.Size8,					    ARG_JB,     ARG_NONE,    16,   4,    0,    false),
            /*0x7E*/ new OpCode(InstructionType.JLE,			    OpCodeFlag.Size8,					    ARG_JB,     ARG_NONE,    16,   4,    0,    false),
            /*0x7F*/ new OpCode(InstructionType.JG,			        OpCodeFlag.Size8,					    ARG_JB,     ARG_NONE,    16,   4,    0,    false),
            /*0x80*/ new OpCode(InstructionType.Group,			    OpCodeFlag.Size8 | OpCodeFlag.HasRM,	ARG_EB,     ARG_IB,      GroupClockDef),
            /*0x81*/ new OpCode(InstructionType.Group,			    OpCodeFlag.HasRM,					    ARG_EW,     ARG_IW,      GroupClockDef),
            /*0x82*/ new OpCode(InstructionType.Group,			    OpCodeFlag.Size8 | OpCodeFlag.HasRM,	ARG_EB,     ARG_IB,      GroupClockDef),
            /*0x83*/ new OpCode(InstructionType.Group,			    OpCodeFlag.HasRM | OpCodeFlag.Signed,	ARG_EW,     ARG_IB,      GroupClockDef),
            /*0x84*/ new OpCode(InstructionType.Test,			    OpCodeFlag.Size8 | OpCodeFlag.HasRM,	ARG_GB,     ARG_EB,      TestClockDef),
            /*0x85*/ new OpCode(InstructionType.Test,			    OpCodeFlag.HasRM,					    ARG_GW,     ARG_EW,      TestClockDef),
            /*0x86*/ new OpCode(InstructionType.Xchg,			    OpCodeFlag.Size8 | OpCodeFlag.HasRM,	ARG_GB,     ARG_EB,      XchgClockDef),
            /*0x87*/ new OpCode(InstructionType.Xchg,			    OpCodeFlag.HasRM,					    ARG_GW,     ARG_EW,      XchgClockDef),
            /*0x88*/ new OpCode(InstructionType.Move,			    OpCodeFlag.Size8 | OpCodeFlag.HasRM,	ARG_EB,     ARG_GB,      MoveClockDef),
            /*0x89*/ new OpCode(InstructionType.Move,			    OpCodeFlag.HasRM,					    ARG_EW,     ARG_GW,      MoveClockDef),
            /*0x8A*/ new OpCode(InstructionType.Move,			    OpCodeFlag.Size8 | OpCodeFlag.HasRM,	ARG_GB,     ARG_EB,      MoveClockDef),
            /*0x8B*/ new OpCode(InstructionType.Move,			    OpCodeFlag.HasRM,					    ARG_GW,     ARG_EW,      MoveClockDef),
            /*0x8C*/ new OpCode(InstructionType.Move,			    OpCodeFlag.HasRM,					    ARG_EW,     ARG_S,       MoveClockDef),
            /*0x8D*/ new OpCode(InstructionType.Lea,			    OpCodeFlag.HasRM,					    ARG_GW,     ARG_M,       2,    0,    true),
            /*0x8E*/ new OpCode(InstructionType.Move,			    OpCodeFlag.HasRM,					    ARG_S,      ARG_EW,      MoveClockDef),
            /*0x8F*/ new OpCode(InstructionType.Pop,			    OpCodeFlag.HasRM,					    ARG_EW,     ARG_NONE,    9,    2,    true),
            /*0x90*/ new OpCode(InstructionType.Xchg,			    OpCodeFlag.None,                        ARG_AX,     ARG_AX,      3,    0,    false),
            /*0x91*/ new OpCode(InstructionType.Xchg,			    OpCodeFlag.None,					    ARG_CX,     ARG_AX,      4,    0,    false),
            /*0x92*/ new OpCode(InstructionType.Xchg,			    OpCodeFlag.None,					    ARG_DX,     ARG_AX,      4,    0,    false),
            /*0x93*/ new OpCode(InstructionType.Xchg,			    OpCodeFlag.None,					    ARG_BX,     ARG_AX,      4,    0,    false),
            /*0x94*/ new OpCode(InstructionType.Xchg,			    OpCodeFlag.None,					    ARG_SP,     ARG_AX,      4,    0,    false),
            /*0x95*/ new OpCode(InstructionType.Xchg,			    OpCodeFlag.None,					    ARG_BP,     ARG_AX,      4,    0,    false),
            /*0x96*/ new OpCode(InstructionType.Xchg,			    OpCodeFlag.None,					    ARG_SI,     ARG_AX,      4,    0,    false),
            /*0x97*/ new OpCode(InstructionType.Xchg,			    OpCodeFlag.None,					    ARG_DI,     ARG_AX,      4,    0,    false),
            /*0x98*/ new OpCode(InstructionType.Cbw,			    OpCodeFlag.None,					    ARG_NONE,   ARG_NONE,    2,    0,    false),
            /*0x99*/ new OpCode(InstructionType.Cwd,			    OpCodeFlag.None,					    ARG_NONE,   ARG_NONE,    5,    0,    false),
            /*0x9A*/ new OpCode(InstructionType.CallFar,			OpCodeFlag.None,					    ARG_A,      ARG_NONE,    20,   2,    false),
            /*0x9B*/ new OpCode(InstructionType.Wait,			    OpCodeFlag.None,					    ARG_NONE,   ARG_NONE,    8,    0,    false),
            /*0x9C*/ new OpCode(InstructionType.Push,			    OpCodeFlag.None,                        ARG_FLAGS,  ARG_NONE,    6,    1,    false),
            /*0x9D*/ new OpCode(InstructionType.Pop,			    OpCodeFlag.None,					    ARG_FLAGS,  ARG_NONE,    4,    1,    false),
            /*0x9E*/ new OpCode(InstructionType.Sahf,			    OpCodeFlag.None,					    ARG_NONE,   ARG_NONE,    4,    0,    false),
            /*0x9F*/ new OpCode(InstructionType.Lahf,			    OpCodeFlag.None,					    ARG_NONE,   ARG_NONE,    4,    0,    false),
            /*0xA0*/ new OpCode(InstructionType.Move,			    OpCodeFlag.None,					    ARG_AL,     ARG_OB,      MoveClockDef),
            /*0xA1*/ new OpCode(InstructionType.Move,			    OpCodeFlag.None,					    ARG_AX,     ARG_OW,      MoveClockDef),
            /*0xA2*/ new OpCode(InstructionType.Move,			    OpCodeFlag.Size8,					    ARG_OB,     ARG_AL,      MoveClockDef),
            /*0xA3*/ new OpCode(InstructionType.Move,			    OpCodeFlag.None,					    ARG_OW,     ARG_AX,      MoveClockDef),
            /*0xA4*/ new OpCode(InstructionType.Movs,			    OpCodeFlag.Size8,					    ARG_NONE,   ARG_NONE,    10,   2,    false),
            /*0xA5*/ new OpCode(InstructionType.Movs,			    OpCodeFlag.None,					    ARG_NONE,   ARG_NONE,    10,   2,    false),
            /*0xA6*/ new OpCode(InstructionType.Cmps,			    OpCodeFlag.Size8,					    ARG_NONE,   ARG_NONE,    14,   2,    false),
            /*0xA7*/ new OpCode(InstructionType.Cmps,			    OpCodeFlag.None,					    ARG_NONE,   ARG_NONE,    14,   2,    false),
            /*0xA8*/ new OpCode(InstructionType.Test,			    OpCodeFlag.Size8,					    ARG_AL,     ARG_IB,      4,    0,    false),
            /*0xA9*/ new OpCode(InstructionType.Test,			    OpCodeFlag.None,					    ARG_AX,     ARG_IW,      4,    0,    false),
            /*0xAA*/ new OpCode(InstructionType.Stos,			    OpCodeFlag.Size8,					    ARG_NONE,   ARG_NONE,    7,    1,    false),
            /*0xAB*/ new OpCode(InstructionType.Stos,			    OpCodeFlag.None,					    ARG_NONE,   ARG_NONE,    7,    1,    false),
            /*0xAC*/ new OpCode(InstructionType.Lods,			    OpCodeFlag.Size8,					    ARG_NONE,   ARG_NONE,    8,    1,    false),
            /*0xAD*/ new OpCode(InstructionType.Lods,			    OpCodeFlag.None,					    ARG_NONE,   ARG_NONE,    8,    1,    false),
            /*0xAE*/ new OpCode(InstructionType.Scas,			    OpCodeFlag.Size8,					    ARG_NONE,   ARG_NONE,    11,   1,    false),
            /*0xAF*/ new OpCode(InstructionType.Scas,			    OpCodeFlag.None,					    ARG_NONE,   ARG_NONE,    11,   1,    false),
            /*0xB0*/ new OpCode(InstructionType.Move,			    OpCodeFlag.Size8,					    ARG_AL,     ARG_IB,      4,    0,    false),
            /*0xB1*/ new OpCode(InstructionType.Move,			    OpCodeFlag.Size8,					    ARG_CL,     ARG_IB,      4,    0,    false),
            /*0xB2*/ new OpCode(InstructionType.Move,			    OpCodeFlag.Size8,					    ARG_DL,     ARG_IB,      4,    0,    false),
            /*0xB3*/ new OpCode(InstructionType.Move,			    OpCodeFlag.Size8,					    ARG_BL,     ARG_IB,      4,    0,    false),
            /*0xB4*/ new OpCode(InstructionType.Move,			    OpCodeFlag.Size8,					    ARG_AH,     ARG_IB,      4,    0,    false),
            /*0xB5*/ new OpCode(InstructionType.Move,			    OpCodeFlag.Size8,					    ARG_CH,     ARG_IB,      4,    0,    false),
            /*0xB6*/ new OpCode(InstructionType.Move,			    OpCodeFlag.Size8,					    ARG_DH,     ARG_IB,      4,    0,    false),
            /*0xB7*/ new OpCode(InstructionType.Move,			    OpCodeFlag.Size8,					    ARG_BH,     ARG_IB,      4,    0,    false),
            /*0xB8*/ new OpCode(InstructionType.Move,			    OpCodeFlag.None,					    ARG_AX,     ARG_IW,      4,    0,    false),
            /*0xB9*/ new OpCode(InstructionType.Move,			    OpCodeFlag.None,					    ARG_CX,     ARG_IW,      4,    0,    false),
            /*0xBA*/ new OpCode(InstructionType.Move,			    OpCodeFlag.None,					    ARG_DX,     ARG_IW,      4,    0,    false),
            /*0xBB*/ new OpCode(InstructionType.Move,			    OpCodeFlag.None,					    ARG_BX,     ARG_IW,      4,    0,    false),
            /*0xBC*/ new OpCode(InstructionType.Move,			    OpCodeFlag.None,					    ARG_SP,     ARG_IW,      4,    0,    false),
            /*0xBD*/ new OpCode(InstructionType.Move,			    OpCodeFlag.None,					    ARG_BP,     ARG_IW,      4,    0,    false),
            /*0xBE*/ new OpCode(InstructionType.Move,			    OpCodeFlag.None,					    ARG_SI,     ARG_IW,      4,    0,    false),
            /*0xBF*/ new OpCode(InstructionType.Move,			    OpCodeFlag.None,					    ARG_DI,     ARG_IW,      4,    0,    false),
            /*0xC0*/ new OpCode(InstructionType.Group,			    OpCodeFlag.Size8 | OpCodeFlag.HasRM,	ARG_EB,     ARG_IB,      GroupClockDef),
            /*0xC1*/ new OpCode(InstructionType.Group,			    OpCodeFlag.HasRM,					    ARG_EW,     ARG_IB,      GroupClockDef),
            /*0xC2*/ new OpCode(InstructionType.ReturnNear,			OpCodeFlag.None,					    ARG_IW,     ARG_NONE,    16,   1,    false),
            /*0xC3*/ new OpCode(InstructionType.ReturnNear,			OpCodeFlag.None,					    ARG_NONE,   ARG_NONE,    12,   1,    false),
            /*0xC4*/ new OpCode(InstructionType.Les,			    OpCodeFlag.HasRM,					    ARG_GW,     ARG_M,       8,    2,    true),
            /*0xC5*/ new OpCode(InstructionType.Lds,			    OpCodeFlag.HasRM,					    ARG_GW,     ARG_M,       8,    2,    true),
            /*0xC6*/ new OpCode(InstructionType.Move,			    OpCodeFlag.Size8 | OpCodeFlag.HasRM,	ARG_EB,     ARG_IB,      MoveClockDef),
            /*0xC7*/ new OpCode(InstructionType.Move,			    OpCodeFlag.HasRM,					    ARG_EW,     ARG_IW,      MoveClockDef),
            /*0xC8*/ new OpCode(InstructionType.Invalid,			OpCodeFlag.None,					    ARG_NONE,   ARG_NONE,    0,    0,    false),
            /*0xC9*/ new OpCode(InstructionType.Invalid,			OpCodeFlag.None,					    ARG_NONE,   ARG_NONE,    0,    0,    false),
            /*0xCA*/ new OpCode(InstructionType.ReturnFar,			OpCodeFlag.None,					    ARG_IW,     ARG_NONE,    15,   2,    false),
            /*0xCB*/ new OpCode(InstructionType.ReturnFar,			OpCodeFlag.None,					    ARG_NONE,   ARG_NONE,    16,   2,    false),
            /*0xCC*/ new OpCode(InstructionType.Int,			    OpCodeFlag.None,					    ARG_3,      ARG_NONE,    32,   5,    false),
            /*0xCD*/ new OpCode(InstructionType.Int,			    OpCodeFlag.Size8,					    ARG_IB,     ARG_NONE,    31,   5,    false),
            /*0xCE*/ new OpCode(InstructionType.Into,			    OpCodeFlag.None,					    ARG_NONE,   ARG_NONE,    33,   4,    5,    false),
            /*0xCF*/ new OpCode(InstructionType.ReturnInterrupt,	OpCodeFlag.None,					    ARG_NONE,   ARG_NONE,    20,   3,    false),
            /*0xD0*/ new OpCode(InstructionType.Group,			    OpCodeFlag.Size8 | OpCodeFlag.HasRM,	ARG_EB,     ARG_1,       GroupClockDef),
            /*0xD1*/ new OpCode(InstructionType.Group,			    OpCodeFlag.HasRM,					    ARG_EW,     ARG_1,       GroupClockDef),
            /*0xD2*/ new OpCode(InstructionType.Group,			    OpCodeFlag.Size8 | OpCodeFlag.HasRM,	ARG_EB,     ARG_CL,      GroupClockDef),
            /*0xD3*/ new OpCode(InstructionType.Group,			    OpCodeFlag.HasRM,					    ARG_EW,     ARG_CL,      GroupClockDef),
            /*0xD4*/ new OpCode(InstructionType.Aam,			    OpCodeFlag.Size8,					    ARG_IB,     ARG_NONE,    83,    0,    false),
            /*0xD5*/ new OpCode(InstructionType.Aad,			    OpCodeFlag.Size8,					    ARG_IB,     ARG_NONE,    60,    0,    false),
            /*0xD6*/ new OpCode(InstructionType.Invalid,			OpCodeFlag.None,					    ARG_NONE,   ARG_NONE,    0,     0,    false),
            /*0xD7*/ new OpCode(InstructionType.Xlat,			    OpCodeFlag.None,					    ARG_NONE,   ARG_NONE,    11,    0,    false),
            /*0xD8*/ new OpCode(InstructionType.Invalid,			OpCodeFlag.None,					    ARG_NONE,   ARG_NONE,    0,    0,    false),
            /*0xD9*/ new OpCode(InstructionType.Invalid,			OpCodeFlag.None,					    ARG_NONE,   ARG_NONE,    0,    0,    false),
            /*0xDA*/ new OpCode(InstructionType.Invalid,			OpCodeFlag.None,					    ARG_NONE,   ARG_NONE,    0,    0,    false),
            /*0xDB*/ new OpCode(InstructionType.Invalid,			OpCodeFlag.None,					    ARG_NONE,   ARG_NONE,    0,    0,    false),
            /*0xDC*/ new OpCode(InstructionType.Invalid,			OpCodeFlag.None,					    ARG_NONE,   ARG_NONE,    0,    0,    false),
            /*0xDD*/ new OpCode(InstructionType.Invalid,			OpCodeFlag.Size8,					    ARG_NONE,   ARG_NONE,    0,    0,    false),
            /*0xDE*/ new OpCode(InstructionType.Invalid,			OpCodeFlag.None,					    ARG_NONE,   ARG_NONE,    0,    0,    false),
            /*0xDF*/ new OpCode(InstructionType.Invalid,			OpCodeFlag.None,					    ARG_NONE,   ARG_NONE,    0,    0,    false),
            /*0xE0*/ new OpCode(InstructionType.Loopnz,			    OpCodeFlag.Size8,					    ARG_JB,     ARG_NONE,    19,   5,    0,    false),
            /*0xE1*/ new OpCode(InstructionType.Loopz,			    OpCodeFlag.Size8,					    ARG_JB,     ARG_NONE,    18,   6,    0,    false),
            /*0xE2*/ new OpCode(InstructionType.Loop,			    OpCodeFlag.Size8,					    ARG_JB,     ARG_NONE,    17,   5,    0,    false),
            /*0xE3*/ new OpCode(InstructionType.Jcxz,			    OpCodeFlag.Size8,					    ARG_JB,     ARG_NONE,    18,   6,    0,    false),
            /*0xE4*/ new OpCode(InstructionType.In,			        OpCodeFlag.Size8,					    ARG_AL,     ARG_IB,      6,    1,    false),
            /*0xE5*/ new OpCode(InstructionType.In,			        OpCodeFlag.None,					    ARG_AX,     ARG_IB,      6,    1,    false),
            /*0xE6*/ new OpCode(InstructionType.Out,			    OpCodeFlag.Size8,					    ARG_IB,     ARG_AL,      6,    1,    false),
            /*0xE7*/ new OpCode(InstructionType.Out,			    OpCodeFlag.None,					    ARG_IB,     ARG_AX,      6,    1,    false),
            /*0xE8*/ new OpCode(InstructionType.CallNearRelative,	OpCodeFlag.None,					    ARG_JW,     ARG_NONE,    15,   1,    false),
            /*0xE9*/ new OpCode(InstructionType.JumpRelative,	    OpCodeFlag.None,					    ARG_JW,     ARG_NONE,    15,   0,    false),
            /*0xEA*/ new OpCode(InstructionType.JumpFar,			OpCodeFlag.None,					    ARG_A,      ARG_NONE,    24,   0,    false),
            /*0xEB*/ new OpCode(InstructionType.JumpRelative,	    OpCodeFlag.Size8,					    ARG_JB,     ARG_NONE,    15,   0,    false),
            /*0xEC*/ new OpCode(InstructionType.In,			        OpCodeFlag.Size8,					    ARG_AL,     ARG_DX,      4,    1,    false),
            /*0xED*/ new OpCode(InstructionType.In,			        OpCodeFlag.None,					    ARG_AX,     ARG_DX,      4,    1,    false),
            /*0xEE*/ new OpCode(InstructionType.Out,			    OpCodeFlag.Size8,					    ARG_DX,     ARG_AL,      4,    1,    false),
            /*0xEF*/ new OpCode(InstructionType.Out,			    OpCodeFlag.None,					    ARG_DX,     ARG_AX,      4,    1,    false),
            /*0xF0*/ new OpCode(InstructionType.Prefix,			    OpCodeFlag.None,					    ARG_NONE,   ARG_NONE,    0,    0,    false),
            /*0xF1*/ new OpCode(InstructionType.Invalid,			OpCodeFlag.None,					    ARG_NONE,   ARG_NONE,    0,    0,    false),
            /*0xF2*/ new OpCode(InstructionType.Prefix,			    OpCodeFlag.None,					    ARG_NONE,   ARG_NONE,    0,    0,    false),
            /*0xF3*/ new OpCode(InstructionType.Prefix,			    OpCodeFlag.None,					    ARG_NONE,   ARG_NONE,    0,    0,    false),
            /*0xF4*/ new OpCode(InstructionType.Hlt,			    OpCodeFlag.None,					    ARG_NONE,   ARG_NONE,    2,    0,    false),
            /*0xF5*/ new OpCode(InstructionType.Cmc,			    OpCodeFlag.None,					    ARG_NONE,   ARG_NONE,    2,    0,    false),
            /*0xF6*/ new OpCode(InstructionType.Group,			    OpCodeFlag.Size8 | OpCodeFlag.HasRM,    ARG_EB,     ARG_NONE,      GroupClockDef),
            /*0xF7*/ new OpCode(InstructionType.Group,			    OpCodeFlag.HasRM,					    ARG_EW,     ARG_NONE,      GroupClockDef),
            /*0xF8*/ new OpCode(InstructionType.Clc,			    OpCodeFlag.None,					    ARG_NONE,   ARG_NONE,    2,    0,    false),
            /*0xF9*/ new OpCode(InstructionType.Stc,			    OpCodeFlag.None,					    ARG_NONE,   ARG_NONE,    2,    0,    false),
            /*0xFA*/ new OpCode(InstructionType.Cli,			    OpCodeFlag.None,					    ARG_NONE,   ARG_NONE,    2,    0,    false),
            /*0xFB*/ new OpCode(InstructionType.Sti,			    OpCodeFlag.None,					    ARG_NONE,   ARG_NONE,    2,    0,    false),
            /*0xFC*/ new OpCode(InstructionType.Cld,			    OpCodeFlag.None,					    ARG_NONE,   ARG_NONE,    2,    0,    false),
            /*0xFD*/ new OpCode(InstructionType.Std,			    OpCodeFlag.None,					    ARG_NONE,   ARG_NONE,    2,    0,    false),
            /*0xFE*/ new OpCode(InstructionType.Group,              OpCodeFlag.Size8 | OpCodeFlag.HasRM,	ARG_EB,     ARG_NONE,      GroupClockDef),
            /*0xFF*/ new OpCode(InstructionType.Group,			    OpCodeFlag.HasRM,					    ARG_EW,     ARG_NONE,      GroupClockDef)
        };

        private static readonly InstructionType[] opcodeExtension80 =
        {
            InstructionType.Add, InstructionType.Or, InstructionType.Adc, InstructionType.Sbb, InstructionType.And, InstructionType.Subtract, InstructionType.Xor, InstructionType.Compare
        };

        private static readonly InstructionType[] opcodeExtensionC0 =
        {
            InstructionType.Invalid, InstructionType.Invalid, InstructionType.Invalid, InstructionType.Invalid, InstructionType.Shl, InstructionType.Shr, InstructionType.Invalid, InstructionType.Invalid
        };

        private static readonly InstructionType[] opcodeExtensionC1 =
        {
            InstructionType.Invalid, InstructionType.Invalid, InstructionType.Invalid, InstructionType.Invalid, InstructionType.Shl, InstructionType.Shr, InstructionType.Invalid, InstructionType.Invalid
        };

        private static readonly InstructionType[] opcodeExtensionD0 =
        {
            InstructionType.Rol, InstructionType.Ror, InstructionType.Rcl, InstructionType.Rcr, InstructionType.Shl, InstructionType.Shr, InstructionType.Invalid, InstructionType.Sar
        };

        private static readonly InstructionType[] opcodeExtensionF6 =
        {
            InstructionType.Test, InstructionType.Invalid, InstructionType.Not, InstructionType.Negate, InstructionType.Multiply, InstructionType.SignedMultiply, InstructionType.Divide, InstructionType.SignedDivide
        };

        private static readonly InstructionType[] opcodeExtensionFe =
        {
            InstructionType.Increment, InstructionType.Decrement, InstructionType.Invalid, InstructionType.Invalid, InstructionType.Invalid, InstructionType.Invalid, InstructionType.Invalid, InstructionType.Invalid
        };

        private static readonly InstructionType[] opcodeExtensionFf =
        {
            InstructionType.Increment, InstructionType.Decrement, InstructionType.CallNear, InstructionType.CallFar, InstructionType.Jump, InstructionType.JumpFar, InstructionType.Push, InstructionType.Invalid
        };

        private const int CLK_COMPLEX = -1;

        private const int ARG_AX = (int)Cpu8086.Register.AX;
        private const int ARG_CX = (int)Cpu8086.Register.CX;
        private const int ARG_DX = (int)Cpu8086.Register.DX;
        private const int ARG_BX = (int)Cpu8086.Register.BX;

        private const int ARG_SP = (int)Cpu8086.Register.SP;
        private const int ARG_BP = (int)Cpu8086.Register.BP;
        private const int ARG_SI = (int)Cpu8086.Register.SI;
        private const int ARG_DI = (int)Cpu8086.Register.DI;

        private const int ARG_ES = (int)Cpu8086.Register.ES;
        private const int ARG_CS = (int)Cpu8086.Register.CS;
        private const int ARG_SS = (int)Cpu8086.Register.SS;
        private const int ARG_DS = (int)Cpu8086.Register.DS;

        private const int ARG_IP = (int)Cpu8086.Register.IP;
        private const int ARG_FLAGS = unchecked((int)Cpu8086.Register.FLAGS);

        private const int ARG_AL = unchecked((int)Cpu8086.Register.AL);
        private const int ARG_CL = unchecked((int)Cpu8086.Register.CL);
        private const int ARG_DL = unchecked((int)Cpu8086.Register.DL);
        private const int ARG_BL = unchecked((int)Cpu8086.Register.BL);
        private const int ARG_AH = unchecked((int)Cpu8086.Register.AH);
        private const int ARG_CH = unchecked((int)Cpu8086.Register.CH);
        private const int ARG_DH = unchecked((int)Cpu8086.Register.DH);
        private const int ARG_BH = unchecked((int)Cpu8086.Register.BH);

        private const int ARG_A = 0xFFF0;
        private const int ARG_EB = 0xFFF1;
        private const int ARG_EW = 0xFFF2;
        private const int ARG_GB = 0xFFF3;
        private const int ARG_GW = 0xFFF4;
        private const int ARG_IB = 0xFFF5;
        private const int ARG_IW = 0xFFF6;
        private const int ARG_JB = 0xFFF7;
        private const int ARG_JW = 0xFFF8;
        private const int ARG_M = 0xFFF9;
        private const int ARG_OB = 0xFFFA;
        private const int ARG_OW = 0xFFFB;
        private const int ARG_S = 0xFFFC;
        private const int ARG_1 = 0xFFFD;
        private const int ARG_3 = 0xFFFE;
        public const int ARG_BYTE_REGISTER = -6;
        public const int ARG_DEREFERENCE = -5;
        public const int ARG_FAR_MEMORY = -4;
        public const int ARG_MEMORY = -3;
        public const int ARG_CONSTANT = -2;
        public const int ARG_NONE = -1;

        public struct Instruction
        {
            public Cpu8086.Register SegmentPrefix;
            public byte OpcodePrefix;
            public InstructionType Type;
            public OpCodeFlag Flag;

            public int Argument1;
            public int Argument1Value;
            public int Argument1Displacement;
            public int Argument2;
            public int Argument2Value;
            public int Argument2Displacement;

            public int Clocks;
        }

        public static Instruction Decode([NotNull] IInstructionFetcher fetcher)
        {
            Instruction instruction = new Instruction();

            var opcode = fetcher.FetchU8();
            instruction.Type = opCodes[opcode].Type;
            instruction.SegmentPrefix = Cpu8086.Register.Invalid;
            instruction.OpcodePrefix = 0;

            while (instruction.Type == InstructionType.Prefix)
            {
                switch (opcode)
                {
                    case 0x26:
                        instruction.SegmentPrefix = Cpu8086.Register.ES;
                        break;
                    case 0x2E:
                        instruction.SegmentPrefix = Cpu8086.Register.CS;
                        break;
                    case 0x36:
                        instruction.SegmentPrefix = Cpu8086.Register.SS;
                        break;
                    case 0x3E:
                        instruction.SegmentPrefix = Cpu8086.Register.DS;
                        break;
                    case 0xF0:
                    case 0xF2:
                    case 0xF3:
                        instruction.OpcodePrefix = opcode;
                        break;
                    default:
                        throw new NotImplementedException();
                }

                opcode = fetcher.FetchU8();
                instruction.Type = opCodes[opcode].Type;
            }
            if (instruction.Type == InstructionType.EmulatorSpecial)
            {
                var opcode2 = fetcher.FetchU8();
                Debug.Assert(opcode2 == 0x0F);
            }
            Debug.Assert(instruction.Type != InstructionType.Invalid);

            var argument1Type = opCodes[opcode].Argument1Type;
            var argument2Type = opCodes[opcode].Argument2Type;

            instruction.Flag = opCodes[opcode].Flag;
            byte rm = 0xFF;
            if (instruction.Flag.Has(OpCodeFlag.HasRM))
                rm = fetcher.FetchU8();

            if (instruction.Type == InstructionType.Group)
            {
                instruction.Type = ConvertFromGroup(opcode, rm);
                Debug.Assert(instruction.Type != InstructionType.Invalid);
                var reg = (byte)((rm >> 3) & 7);
                if (opcode == 0xF6 && reg == 0)
                    argument2Type = ARG_IB;
                else if (opcode == 0xF7 && reg == 0)
                    argument2Type = ARG_IW;
                else if (opcode == 0xFF && (reg == 3 || reg == 5))
                    argument1Type = ARG_M;
            }

            ParseArgument(fetcher, instruction.Flag, out instruction.Argument1, out instruction.Argument1Value, out instruction.Argument1Displacement, argument1Type, rm);
            ParseArgument(fetcher, instruction.Flag, out instruction.Argument2, out instruction.Argument2Value, out instruction.Argument2Displacement, argument2Type, rm);

            var clockDef = opCodes[opcode].ClockFunc(instruction);

            var clocks = clockDef.BaseClocks;
            if (clocks == CLK_COMPLEX)
                clocks = 50;

            if (instruction.Flag.Has(OpCodeFlag.Size8))
            {
                clocks += 4*clockDef.Transfers;
            }
            else
            {
                clocks += 8*clockDef.Transfers;
            }
            if (clockDef.EA)
            {
                if (instruction.Argument1Displacement != ARG_NONE)
                    clocks += 12;
                if (instruction.Argument2Displacement != ARG_NONE)
                    clocks += 12;
            }
            instruction.Clocks = clocks;

            return instruction;
        }

        private static void ParseArgument([NotNull] IInstructionFetcher fetcher, OpCodeFlag flag, out int argument, out int argumentValue, out int argumentDisplacement, int argumentType, byte modrm)
        {
            var mod = (byte)((modrm >> 6) & 7);
            var reg = (byte)((modrm >> 3) & 7);
            var rm = (byte)(modrm & 7);

            switch (argumentType)
            {
                case ARG_AX:
                case ARG_CX:
                case ARG_DX:
                case ARG_BX:
                case ARG_SP:
                case ARG_BP:
                case ARG_SI:
                case ARG_DI:
                case ARG_IP:
                case ARG_CS:
                case ARG_DS:
                case ARG_ES:
                case ARG_SS:
                case ARG_FLAGS:
                    argument = argumentType;
                    argumentValue = ARG_NONE;
                    argumentDisplacement = ARG_NONE;
                    break;

                case ARG_AL:
                case ARG_CL:
                case ARG_DL:
                case ARG_BL:
                case ARG_AH:
                case ARG_CH:
                case ARG_DH:
                case ARG_BH:
                    argument = ARG_BYTE_REGISTER;
                    argumentValue = argumentType;
                    argumentDisplacement = ARG_NONE;
                    break;

                case ARG_1:
                    argument = ARG_CONSTANT;
                    argumentValue = 1;
                    argumentDisplacement = ARG_NONE;
                    break;

                case ARG_3:
                    argument = ARG_CONSTANT;
                    argumentValue = 3;
                    argumentDisplacement = ARG_NONE;
                    break;

                case ARG_IB:
                    argument = ARG_CONSTANT;
                    argumentValue = fetcher.FetchU8();
                    argumentDisplacement = ARG_NONE;
                    if (flag.Has(OpCodeFlag.Signed))
                        argumentValue = (sbyte)(byte)argumentValue;
                    break;
                case ARG_IW:
                    argument = ARG_CONSTANT;
                    argumentValue = fetcher.FetchU16();
                    argumentDisplacement = ARG_NONE;
                    break;

                case ARG_JB:
                    argument = ARG_CONSTANT;
                    argumentValue = (sbyte)fetcher.FetchU8();
                    argumentDisplacement = ARG_NONE;
                    break;
                case ARG_JW:
                    argument = ARG_CONSTANT;
                    argumentValue = fetcher.FetchU16();
                    argumentDisplacement = ARG_NONE;
                    break;

                case ARG_S:
                    Debug.Assert(reg < 4);
                    argument = reg + ARG_ES;
                    argumentValue = ARG_NONE;
                    argumentDisplacement = ARG_NONE;
                    break;

                case ARG_GB:
                    argument = ARG_BYTE_REGISTER;
                    argumentValue = ARG_AL + reg;
                    argumentDisplacement = ARG_NONE;
                    break;
                case ARG_GW:
                    argument = reg;
                    argumentValue = ARG_NONE;
                    argumentDisplacement = ARG_NONE;
                    break;

                case ARG_OB:
                case ARG_OW:
                    argument = ARG_MEMORY;
                    argumentValue = fetcher.FetchU16();
                    argumentDisplacement = ARG_NONE;
                    break;

                case ARG_A:
                    argument = ARG_FAR_MEMORY;
                    var address = fetcher.FetchU16();
                    var segment = fetcher.FetchU16();
                    argumentValue = (int)(((uint)segment << 16) | address);
                    argumentDisplacement = ARG_NONE;
                    break;

                case ARG_EB:
                case ARG_EW:
                case ARG_M:
                    switch (mod)
                    {
                        case 0:
                            if (rm == 6)
                            {
                                argument = ARG_MEMORY;
                                argumentValue = fetcher.FetchU16();
                                argumentDisplacement = ARG_NONE;
                            }
                            else
                            {
                                argument = ARG_DEREFERENCE;
                                argumentValue = rm;
                                argumentDisplacement = 0;
                            }
                            break;
                        case 1:
                            argument = ARG_DEREFERENCE;
                            argumentValue = rm;
                            argumentDisplacement = (sbyte)fetcher.FetchU8();
                            break;
                        case 2:
                            argument = ARG_DEREFERENCE;
                            argumentValue = rm;
                            argumentDisplacement = fetcher.FetchU16();
                            break;
                        case 3:
                            Debug.Assert(argumentType != ARG_M);
                            if (argumentType == ARG_EB)
                            {
                                argument = ARG_BYTE_REGISTER;
                                argumentValue = ARG_AL + rm;
                            }
                            else
                            {
                                argument = rm;
                                argumentValue = ARG_NONE;
                            }
                            argumentDisplacement = ARG_NONE;
                            break;
                        default:
                            throw new NotImplementedException();
                    }
                    break;

                case ARG_NONE:
                    argument = ARG_NONE;
                    argumentValue = ARG_NONE;
                    argumentDisplacement = ARG_NONE;
                    break;

                default:
                    throw new NotImplementedException();
            }
        }
        [Pure]
        private static InstructionType ConvertFromGroup(byte opcode, byte modrm)
        {
            var reg = (byte)((modrm >> 3) & 7);
            switch (opcode)
            {
                case 0x80:
                case 0x81:
                case 0x82:
                case 0x83:
                    return opcodeExtension80[reg];
                case 0xC0:
                    return opcodeExtensionC0[reg];
                case 0xC1:
                    return opcodeExtensionC1[reg];
                case 0xD0:
                case 0xD1:
                case 0xD2:
                case 0xD3:
                    return opcodeExtensionD0[reg];
                case 0xF6:
                case 0xF7:
                    return opcodeExtensionF6[reg];
                case 0xFE:
                    return opcodeExtensionFe[reg];
                case 0xFF:
                    return opcodeExtensionFf[reg];
                default:
                    throw new NotImplementedException();
            }
        }
    }
}
