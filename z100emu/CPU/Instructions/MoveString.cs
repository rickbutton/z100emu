using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace z100emu.CPU.Instructions
{
    public class MoveString : IInstruction
    {
        public void Dispatch(Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            var segment = Register.DS;
            if (instruction.SegmentPrefix != Register.Invalid)
                segment = instruction.SegmentPrefix;

            var sourceAddress = InstructionHelper.SegmentToAddress(cpu.GetRegister(segment), cpu.GetRegister(Register.SI));
            var destAddress = InstructionHelper.SegmentToAddress(cpu.GetRegister(Register.ES), cpu.GetRegister(Register.DI));
            byte size;
            if (instruction.Flag.Has(OpCodeManager.OpCodeFlag.Size8))
            {
                var value = cpu.ReadU8(sourceAddress);
                cpu.WriteU8(destAddress, value);
                size = 1;
            }
            else
            {
                var value = cpu.ReadU16(sourceAddress);
                cpu.WriteU16(destAddress, value);
                size = 2;
            }

            if (!cpu.GetFlags().Has(FlagsRegister.Direction))
            {
                cpu.IncRegister(Register.DI, size);
                cpu.IncRegister(Register.SI, size);
            }
            else
            {
                cpu.DecRegister(Register.DI, size);
                cpu.DecRegister(Register.SI, size);
            }
        }
    }
}
