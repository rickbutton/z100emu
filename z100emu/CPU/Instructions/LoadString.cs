using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace z100emu.CPU.Instructions
{
    public class LoadString : IInstruction
    {
        public void Dispatch(Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            var prefix = instruction.SegmentPrefix;
            if (prefix == Register.Invalid) prefix = Register.DS;
            var sourceAddress = InstructionHelper.SegmentToAddress(cpu.GetRegister(prefix), cpu.GetRegister(Register.SI));

            byte size;
            if (instruction.Flag.Has(OpCodeManager.OpCodeFlag.Size8))
            {
                cpu.SetRegisterU8(Register.AL, cpu.ReadU8(sourceAddress));
                size = 1;
            }
            else
            {
                cpu.SetRegister(Register.AX, cpu.ReadU16(sourceAddress));
                size = 2;
            }

            if (!cpu.GetFlags().Has(FlagsRegister.Direction))
                cpu.SetRegister(Register.SI, (ushort)(cpu.GetRegister(Register.SI) + size));
            else
                cpu.SetRegister(Register.SI, (ushort)(cpu.GetRegister(Register.SI) - size));
        }
    }
}
