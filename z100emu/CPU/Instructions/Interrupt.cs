using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace z100emu.CPU.Instructions
{
    public class Interrupt : IInstruction
    {
        public void Dispatch(Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            Debug.Assert(instruction.Argument1 == OpCodeManager.ARG_CONSTANT);
            cpu.Interrupt((byte)instruction.Argument1Value, true);
        }
    }
}
