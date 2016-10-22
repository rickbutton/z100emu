using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace z100emu.CPU.Instructions
{
    public class Jcxz : IInstruction
    {
        public void Dispatch(Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            if (cpu.GetRegister(Register.CX) == 0)
                new JumpRelative().Dispatch(cpu, instruction);
        }
    }
}
