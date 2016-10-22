using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace z100emu.CPU.Instructions
{
    public class Aam : IInstruction
    {
        public void Dispatch(Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            var al = cpu.GetRegister(Register.AL);
            cpu.SetRegister(Register.AH, (byte)(al/10));
            cpu.SetRegister(Register.AL, (byte)(al%10));
            InstructionHelper.CalculateBitwiseFlags(cpu, instruction.Flag, al, 0, cpu.GetRegister(Register.AX));
        }
    }
}
