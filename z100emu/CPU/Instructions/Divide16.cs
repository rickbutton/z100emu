using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace z100emu.CPU.Instructions
{
    public class Divide16 : IInstruction
    {
        public void Dispatch(Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            var value1 = ((uint)cpu.GetRegister(Register.DX) << 16) | cpu.GetRegister(Register.AX);
            uint value2 = InstructionHelper.GetInstructionValue(cpu, instruction.Flag, instruction.SegmentPrefix, instruction.Argument1, instruction.Argument1Value, instruction.Argument1Displacement);

            if (value2 == 0)
            {
                cpu.Interrupt(0);
                return;
            }

            var quotient = value1 / value2;
            if (quotient > 0xFFFF)
            {
                cpu.Interrupt(0);
                return;
            }

            var remainder = value1 % value2;
            cpu.SetRegister(Register.AX, (ushort)quotient);
            cpu.SetRegister(Register.DX, (ushort)remainder);
            
        }
    }
}
