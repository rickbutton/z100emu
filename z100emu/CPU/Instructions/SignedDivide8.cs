using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace z100emu.CPU.Instructions
{
    public class SignedDivide8 : IInstruction
    {
        public void Dispatch(Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            int value1 = (short)cpu.GetRegister(Register.AX);
            int value2 = (sbyte)InstructionHelper.GetInstructionValue(cpu, instruction.Flag, instruction.SegmentPrefix, instruction.Argument1, instruction.Argument1Value, instruction.Argument1Displacement);

            if ((uint)value1 == 0x8000 || value2 == 0)
            {
                cpu.Interrupt(0);
                return;
            }

            var quotient = value1 / value2;
            var remainder = value1 % value2;

            if ((quotient & 0xFF) != quotient)
            {
                cpu.Interrupt(0);
                return;
            }

            cpu.SetRegisterU8(Register.AL, (byte)quotient);
            cpu.SetRegisterU8(Register.AH, (byte)remainder);
        }
    }
}
