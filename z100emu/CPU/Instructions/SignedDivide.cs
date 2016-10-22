using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace z100emu.CPU.Instructions
{
    public class SignedDivide : IInstruction
    {
        public void Dispatch(Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            if (instruction.Flag.Has(OpCodeManager.OpCodeFlag.Size8))
                new SignedDivide8().Dispatch(cpu, instruction);
            else new SignedDivide16().Dispatch(cpu, instruction);
        }
    }
}
