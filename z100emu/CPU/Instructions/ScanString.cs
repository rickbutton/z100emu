using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace z100emu.CPU.Instructions
{
    public class ScanString : IInstruction
    {
        public void Dispatch(Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            ushort value1;
            ushort value2;
            byte size;
            if (instruction.Flag.Has(OpCodeManager.OpCodeFlag.Size8))
            {
                value1 = cpu.GetRegisterU8(Register.AL);
                value2 = cpu.ReadU8(InstructionHelper.SegmentToAddress(cpu.GetRegister(Register.ES), cpu.GetRegister(Register.DI)));
                size = 1;
            }
            else
            {
                value1 = cpu.GetRegister(Register.AX);
                value2 = cpu.ReadU16(InstructionHelper.SegmentToAddress(cpu.GetRegister(Register.ES), cpu.GetRegister(Register.DI)));
                size = 2;
            }
            var result = value1 - value2;

            InstructionHelper.CalculateSubFlags(cpu, instruction.Flag, value1, value2, result);

            if (!cpu.GetFlags().Has(FlagsRegister.Direction))
                cpu.IncRegister(Register.DI, size);
            else
                cpu.DecRegister(Register.DI, size);
        }
    }
}
