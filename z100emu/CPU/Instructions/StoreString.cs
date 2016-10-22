using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace z100emu.CPU.Instructions
{
    public class StoreString : IInstruction
    {
        public void Dispatch(Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            byte size;
            if (instruction.Flag.Has(OpCodeManager.OpCodeFlag.Size8))
            {
                cpu.WriteU8(InstructionHelper.SegmentToAddress(cpu.GetRegister(Register.ES), cpu.GetRegister(Register.DI)), (byte)(cpu.GetRegister(Register.AX) & 0xFF));
                size = 1;
            }
            else
            {
                cpu.WriteU16(InstructionHelper.SegmentToAddress(cpu.GetRegister(Register.ES), cpu.GetRegister(Register.DI)), cpu.GetRegister(Register.AX));
                size = 2;
            }

            if (!cpu.GetFlags().Has(FlagsRegister.Direction))
                cpu.IncRegister(Register.DI, size);
            else
                cpu.DecRegister(Register.DI, size);
        }
    }
}
