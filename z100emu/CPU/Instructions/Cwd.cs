using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace z100emu.CPU.Instructions
{
    public class Cwd : IInstruction
    {
        public void Dispatch(Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            cpu.SetRegister(Register.DX, (cpu.GetRegister(Register.AX) & 0x8000) != 0 ? (ushort)0xFFFF : (ushort)0);
        }
    }
}
