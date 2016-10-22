using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace z100emu.CPU.Instructions
{
    public class JumpRelative : IInstruction
    {
        public void Dispatch(Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            Debug.Assert(instruction.Argument1 == OpCodeManager.ARG_CONSTANT);
            cpu.IncRegister(Register.IP, instruction.Argument1Value);
        }
    }
}
