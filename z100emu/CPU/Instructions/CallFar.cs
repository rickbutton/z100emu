using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace z100emu.CPU.Instructions
{
    public class CallFar : IInstruction
    {
        public void Dispatch(Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            cpu.Push(cpu.GetRegister(Register.CS));
            cpu.Push(cpu.GetRegister(Register.IP));
            new FarJump().Dispatch(cpu, instruction);
        }
    }
}
