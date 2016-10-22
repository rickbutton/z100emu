using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace z100emu.CPU.Instructions
{
    public class Aas : IInstruction
    {
        public void Dispatch(Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            var al = cpu.GetRegisterU8(Register.AL);
            var flags = cpu.GetFlags();

            if ((al & 0xF) > 9 || flags.Has(FlagsRegister.Auxiliary))
            {
                var ah = cpu.GetRegisterU8(Register.AH);

                al = (byte)((al - 6) & 0x0F);
                ah--;

                cpu.SetRegisterU8(Register.AH, ah);
                flags |= FlagsRegister.Carry | FlagsRegister.Auxiliary;
            }
            else
            {
                al &= 0x0F;
                flags &= ~(FlagsRegister.Carry | FlagsRegister.Auxiliary);
            }

            flags &= ~(FlagsRegister.Parity | FlagsRegister.Zero | FlagsRegister.Sign);
            flags |= (InstructionHelper.ParityLookup[al] ? FlagsRegister.Parity : 0) |
                     (al == 0 ? FlagsRegister.Zero : 0) |
                     ((al & 0x80) != 0 ? FlagsRegister.Sign : 0);

            cpu.SetFlags(flags);
            cpu.SetRegisterU8(Register.AL, al);
        }
    }
}
