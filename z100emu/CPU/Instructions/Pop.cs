using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace z100emu.CPU.Instructions
{
    public class Pop : IInstruction
    {
        public void Dispatch(Cpu8086 cpu, OpCodeManager.Instruction instruction)
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
                    var address = InstructionHelper.GetInstructionRealAddress(cpu, instruction.SegmentPrefix, instruction.Argument1, instruction.Argument1Value, instruction.Argument1Displacement);
                    cpu.WriteU16(address, cpu.Pop());
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
