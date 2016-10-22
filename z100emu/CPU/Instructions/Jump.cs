using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace z100emu.CPU.Instructions
{
    public class Jump : IInstruction
    {
        public void Dispatch(Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            var value = InstructionHelper.GetInstructionValue(cpu, instruction.Flag, instruction.SegmentPrefix, instruction.Argument1, instruction.Argument1Value, instruction.Argument1Displacement);
            cpu.SetRegister(Register.IP, value);
        }
    }
}
