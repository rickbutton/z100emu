using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace z100emu.CPU.Instructions
{
    class Move : IInstruction
    {
        public void Dispatch(Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            var value = InstructionHelper.GetInstructionValue(cpu, instruction.Flag, instruction.SegmentPrefix, instruction.Argument2, instruction.Argument2Value, instruction.Argument2Displacement);

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
                    cpu.SetRegister((Register)instruction.Argument1, value);
                    break;

                case OpCodeManager.ARG_BYTE_REGISTER:
                    cpu.SetRegisterU8((Register)instruction.Argument1Value, (byte)value);
                    break;

                case OpCodeManager.ARG_DEREFERENCE:
                case OpCodeManager.ARG_MEMORY:
                    var address = InstructionHelper.GetInstructionRealAddress(cpu, instruction.SegmentPrefix, instruction.Argument1, instruction.Argument1Value, instruction.Argument1Displacement);
                    if (instruction.Flag.Has(OpCodeManager.OpCodeFlag.Size8))
                        cpu.WriteU8(address, (byte)value);
                    else cpu.WriteU16(address, value);
                    break;

                default:
                    throw new NotImplementedException();
            }
        }
    }
}
