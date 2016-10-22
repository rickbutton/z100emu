using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace z100emu.CPU.Instructions
{
    class CallNearRelative : IInstruction
    {
        public void Dispatch(Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            Debug.Assert(instruction.Argument1 == OpCodeManager.ARG_CONSTANT);
            cpu.Push(cpu.GetRegister(Register.IP));
            cpu.SetRegister(Register.IP, (ushort)(cpu.GetRegister(Register.IP) + instruction.Argument1Value));
        }
    }
}
