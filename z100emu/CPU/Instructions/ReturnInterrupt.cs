using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace z100emu.CPU.Instructions
{
    public class ReturnInterrupt : IInstruction
    {
        public void Dispatch(Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            cpu.SetRegister(Register.IP, cpu.Pop());
            cpu.SetRegister(Register.CS, cpu.Pop());
            cpu.SetRegister(Register.FLAGS, cpu.Pop());
            //cpu._interruptStack.Pop();
        }
    }
}
