using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace z100emu.CPU.Instructions
{
    public class CompareString : IInstruction
    {
        public void Dispatch(Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            var segment = Register.DS;
            if (instruction.SegmentPrefix != Register.Invalid)
                segment = instruction.SegmentPrefix;

            ushort value1;
            ushort value2;
            byte size;
            if (instruction.Flag.Has(OpCodeManager.OpCodeFlag.Size8))
            {
                value1 = cpu.ReadU8(InstructionHelper.SegmentToAddress(cpu.GetRegister(segment), cpu.GetRegister(Register.SI)));
                value2 = cpu.ReadU8(InstructionHelper.SegmentToAddress(cpu.GetRegister(Register.ES), cpu.GetRegister(Register.DI)));
                size = 1;
            }
            else
            {
                value1 = cpu.ReadU16(InstructionHelper.SegmentToAddress(cpu.GetRegister(segment), cpu.GetRegister(Register.SI)));
                value2 = cpu.ReadU16(InstructionHelper.SegmentToAddress(cpu.GetRegister(Register.ES), cpu.GetRegister(Register.DI)));
                size = 2;
            }
            var result = value1 - value2;

            InstructionHelper.CalculateSubFlags(cpu, instruction.Flag, value1, value2, result);

            if (!cpu.GetFlags().Has(FlagsRegister.Direction))
            {
                cpu.SetRegister(Register.DI, (ushort)(cpu.GetRegister(Register.DI) + size));
                cpu.SetRegister(Register.SI, (ushort)(cpu.GetRegister(Register.SI) + size));
            }
            else
            {
                cpu.SetRegister(Register.DI, (ushort)(cpu.GetRegister(Register.DI) - size));
                cpu.SetRegister(Register.SI, (ushort)(cpu.GetRegister(Register.SI) - size));
            }
        }
    }
}
