using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace z100emu.CPU.Instructions
{
    public class Xlat : IInstruction
    {
        public void Dispatch(Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            var address = InstructionHelper.SegmentToAddress(cpu.GetRegister(Register.DS), (ushort)(cpu.GetRegister(Register.BX) + cpu.GetRegisterU8(Register.AL)));
            cpu.SetRegisterU8(Register.AL, cpu.ReadU8(address));
        }
    }
}
