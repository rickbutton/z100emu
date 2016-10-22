using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace z100emu.CPU.Instructions
{
    public class UnaryArithmetic : IInstruction
    {
        public void Dispatch(Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            var value = InstructionHelper.GetInstructionValue(cpu, instruction.Flag, instruction.SegmentPrefix, instruction.Argument1, instruction.Argument1Value, instruction.Argument1Displacement);
            int result;

            switch (instruction.Type)
            {
                case OpCodeManager.InstructionType.Decrement:
                    result = value - 1;
                    InstructionHelper.CalculateDecFlags(cpu, instruction.Flag, value, 1, result);
                    break;
                case OpCodeManager.InstructionType.Increment:
                    result = value + 1;
                    InstructionHelper.CalculateIncFlags(cpu, instruction.Flag, value, 1, result);
                    break;
                case OpCodeManager.InstructionType.Negate:
                    result = ~value + 1;
                    InstructionHelper.CalculateSubFlags(cpu, instruction.Flag, 0, value, result);
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
                    cpu.SetRegister((Register)instruction.Argument1, (ushort)result);
                    break;

                case OpCodeManager.ARG_BYTE_REGISTER:
                    cpu.SetRegisterU8((Register)instruction.Argument1Value, (byte)result);
                    break;

                case OpCodeManager.ARG_DEREFERENCE:
                case OpCodeManager.ARG_MEMORY:
                    var address = InstructionHelper.GetInstructionRealAddress(cpu, instruction.SegmentPrefix, instruction.Argument1, instruction.Argument1Value, instruction.Argument1Displacement);
                    if (instruction.Flag.Has(OpCodeManager.OpCodeFlag.Size8))
                        cpu.WriteU8(address, (byte)result);
                    else cpu.WriteU16(address, (ushort)result);
                    break;

                default:
                    throw new NotImplementedException();
            }
        }
    }
}
