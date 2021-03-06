﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace z100emu.CPU.Instructions
{
    public class JBE : IInstruction
    {
        public void Dispatch(Cpu8086 cpu, OpCodeManager.Instruction instruction)
        {
            var flags = cpu.GetFlags();
            if (flags.Has(FlagsRegister.Carry) || flags.Has(FlagsRegister.Zero))
                new JumpRelative().Dispatch(cpu, instruction);
        }
    }
}
