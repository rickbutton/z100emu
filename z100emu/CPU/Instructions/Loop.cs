using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace z100emu.CPU.Instructions
{
    public class Loop : IInstruction
    {
        public void Dispatch(Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            cpu.DecRegister(Register.CX);
            var counter = cpu.GetRegister(Register.CX);
            if (counter != 0)
                new JumpRelative().Dispatch(cpu, instruction);
        }
    }
}
