using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace z100emu.CPU.Instructions
{
    public class Divide : IInstruction
    {
        public void Dispatch(Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            if (instruction.Flag.Has(OpCodeManager.OpCodeFlag.Size8))
                new Divide8().Dispatch(cpu, instruction);
            else new Divide16().Dispatch(cpu, instruction);
        }
    }
}
