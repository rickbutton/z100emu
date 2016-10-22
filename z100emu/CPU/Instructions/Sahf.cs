using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace z100emu.CPU.Instructions
{
    public class Sahf : IInstruction
    {
        public void Dispatch(Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            const FlagsRegister flagsAffected = FlagsRegister.Sign | FlagsRegister.Zero | FlagsRegister.Auxiliary | FlagsRegister.Parity | FlagsRegister.Carry;
            cpu.Flags &= ~flagsAffected;
            cpu.Flags |= (FlagsRegister)(cpu.GetRegister(Register.AH) & (ushort)flagsAffected);
        }
    }
}
