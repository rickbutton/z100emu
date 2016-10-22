using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace z100emu.CPU.Instructions
{
    public class SignedDivide16 : IInstruction
    {
        public void Dispatch(Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            var value1 = (int)(((uint)cpu.GetRegister(Register.DX) << 16) | cpu.GetRegister(Register.AX));
            int value2 = (short)InstructionHelper.GetInstructionValue(cpu, instruction.Flag, instruction.SegmentPrefix, instruction.Argument1, instruction.Argument1Value, instruction.Argument1Displacement);

            if ((uint)value1 == 0x80000000 || value2 == 0)
            {
                cpu.Interrupt(0);
                return;
            }

            var quotient = value1 / value2;
            var remainder = value1 % value2;

            if ((quotient & 0xFFFF) != quotient)
            {
                cpu.Interrupt(0);
                return;
            }

            cpu.SetRegister(Register.AX, (ushort)quotient);
            cpu.SetRegister(Register.DX, (ushort)remainder);
        }
    }
}
