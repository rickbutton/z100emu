using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace z100emu.CPU.Instructions
{
    public class ReturnFar : IInstruction
    {
        public void Dispatch(Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            cpu.SetRegister(Register.IP, cpu.Pop());
            cpu.SetRegister(Register.CS, cpu.Pop());
            if (instruction.Argument1 == OpCodeManager.ARG_CONSTANT)
                cpu.SetRegister(Register.SP, (ushort)(cpu.GetRegister(Register.SP) + instruction.Argument1Value));
            else Debug.Assert(instruction.Argument1 == OpCodeManager.ARG_NONE);
        }
    }
}
