using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace z100emu.CPU.Instructions
{
    public class Das : IInstruction
    {
        public void Dispatch(Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            var al = cpu.GetRegisterU8(Register.AL);
            var oldAl = al;
            var flags = cpu.GetFlags();
            var oldCarry = flags.Has(FlagsRegister.Carry);
            flags &= ~FlagsRegister.Carry;

            if ((al & 0xF) > 9 || flags.Has(FlagsRegister.Auxiliary))
            {
                al -= 6;
                if (oldCarry || (al > oldAl))
                    flags |= FlagsRegister.Carry;
                else flags &= ~FlagsRegister.Carry;
                flags |= FlagsRegister.Auxiliary;
            }
            else flags &= ~FlagsRegister.Auxiliary;

            if (oldAl > 0x99 || oldCarry)
            {
                al -= 0x60;
                flags |= FlagsRegister.Carry;
            }
            else flags &= ~FlagsRegister.Carry;

            flags &= ~(FlagsRegister.Parity | FlagsRegister.Zero | FlagsRegister.Sign);
            flags |= (InstructionHelper.ParityLookup[al] ? FlagsRegister.Parity : 0) |
                     (al == 0 ? FlagsRegister.Zero : 0) |
                     ((al & 0x80) != 0 ? FlagsRegister.Sign : 0);

            cpu.SetFlags(flags);
            cpu.SetRegister(Register.AL, al);
        }
    }
}
